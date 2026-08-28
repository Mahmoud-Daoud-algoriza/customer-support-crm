using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Modules.Customers;
using SupportCrm.Domain.Modules.Identity;
using SupportCrm.Tests.Api;

namespace SupportCrm.Tests.Customers;

/// <summary>
/// Story 04 <b>slice 4</b> — plan task 10, over the routes plan task 8 publishes.
///
/// <para>
/// These are the assertions that could not be made until an endpoint existed. Slices 1–3 proved the
/// entities, the services and the seeder; this suite proves the six things that are only true at the
/// HTTP boundary — the role gate, the two conflict slugs, the empty timeline, the size cap, AP-4's
/// <c>404</c>-not-<c>403</c>, and that <c>storagePath</c> reaches no response.
/// </para>
///
/// <para>
/// The attachment cap is lowered to <see cref="CustomerApiFixture.MaxAttachmentBytes"/> bytes and
/// the storage root pointed at a temp directory <b>through real configuration</b>, so the options
/// binding, the real <c>LocalDiskAttachmentStorage</c> and the real cap check are all exercised
/// rather than substituted.
/// </para>
/// </summary>
public sealed class CustomerAccessTests(CustomerApiFixture fixture) : IClassFixture<CustomerApiFixture>
{
    private SupportCrmApiFactory Factory => fixture.Factory;

    // ---------------------------------------------------------------- 1. The role gate

    /// <summary>
    /// <b>The intake's rule, and the whole of it:</b> <em>"a Customer cannot browse the customer
    /// directory; an Agent can"</em> — proven server-side, not assumed from the UI (T1-D).
    /// </summary>
    [Fact]
    public async Task The_directory_is_closed_to_a_customer_and_open_to_every_staff_role()
    {
        var customerUserId = await Factory.AddCustomerRoleUserAsync("gate.customer@test.local");

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await Factory.CreateClientFor(customerUserId).GetAsync(Customers)).StatusCode);

        // Anonymous is 401, not 403 — a different failure, and the contract distinguishes them.
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await Factory.CreateClient().GetAsync(Customers)).StatusCode);

        // A-4 is a hierarchy: Manager and Administrator satisfy RequireAgent too.
        foreach (var role in new[] { UserRole.Agent, UserRole.Manager, UserRole.Administrator })
        {
            var staffId = await Factory.AddStaffUserAsync(role, $"gate.{role}@test.local");

            Assert.Equal(
                HttpStatusCode.OK,
                (await Factory.CreateClientFor(staffId).GetAsync(Customers)).StatusCode);
        }
    }

    /// <summary>Every customer-scoped route carries the same gate — not just the list.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("/timeline")]
    [InlineData("/notes")]
    [InlineData("/attachments")]
    public async Task Every_customer_route_is_closed_to_a_customer(string suffix)
    {
        var customerUserId = await Factory.AddCustomerRoleUserAsync($"gate.sub{suffix.Replace('/', '-')}@test.local");
        var customerId = await SeedCustomerAsync($"gate.sub{suffix.Replace('/', '-')}");

        var response = await Factory.CreateClientFor(customerUserId)
            .GetAsync($"{Customers}/{customerId}{suffix}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------------------------------------------------------------- 2. The two conflict slugs

    [Fact]
    public async Task A_duplicate_email_on_create_is_409_with_the_customer_slug()
    {
        var (client, branchId) = await AgentAsync("dup.create");

        var first = await client.PostAsJsonAsync(Customers, NewCustomer("dup.create@x.local", branchId));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // 201 carries a Location that actually resolves (docs/api-design.md §2.2).
        var location = first.Headers.Location!.ToString();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(location)).StatusCode);

        var second = await client.PostAsJsonAsync(Customers, NewCustomer("dup.create@x.local", branchId));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("customer-email-in-use", await ProblemTypeAsync(second));
    }

    /// <summary>
    /// <b>A-19 at the HTTP boundary.</b> The two conflicts are distinct because the collisions are
    /// distinct, and a client already handles both slugs — <c>customer-email-in-use</c> from here and
    /// <c>user-already-exists</c> from registration (docs/api-design.md §5.5, PF-6).
    /// </summary>
    [Fact]
    public async Task A_duplicate_email_on_patch_is_409_and_names_which_kind_of_collision_it_was()
    {
        var (client, branchId) = await AgentAsync("dup.patch");

        var subject = await CreateAsync(client, NewCustomer("dup.patch.subject@x.local", branchId));
        await CreateAsync(client, NewCustomer("dup.patch.rival@x.local", branchId));

        // Another CUSTOMER holds it.
        var againstCustomer = await client.PatchAsJsonAsync(
            $"{Customers}/{subject}", new { email = "dup.patch.rival@x.local" });

        Assert.Equal(HttpStatusCode.Conflict, againstCustomer.StatusCode);
        Assert.Equal("customer-email-in-use", await ProblemTypeAsync(againstCustomer));

        // Another USER holds it — a staff account with no customer profile at all.
        await Factory.AddStaffUserAsync(UserRole.Manager, "dup.patch.staff@x.local");

        var againstUser = await client.PatchAsJsonAsync(
            $"{Customers}/{subject}", new { email = "dup.patch.staff@x.local" });

        Assert.Equal(HttpStatusCode.Conflict, againstUser.StatusCode);
        Assert.Equal("user-already-exists", await ProblemTypeAsync(againstUser));

        // Neither call wrote anything.
        Assert.Equal("dup.patch.subject@x.local", await CustomerEmailAsync(subject));
    }

    // ---------------------------------------------------------------- 3. The empty timeline

    /// <summary>
    /// The intake's acceptance criterion: <em>"the timeline for a customer with no tickets renders
    /// an empty state rather than an error"</em>. A well-formed empty page, not a <c>404</c> and not
    /// a <c>500</c>.
    /// </summary>
    [Fact]
    public async Task The_timeline_of_a_customer_with_no_tickets_is_an_empty_page()
    {
        var (client, branchId) = await AgentAsync("timeline.http");
        var customerId = await CreateAsync(client, NewCustomer("timeline.http@x.local", branchId));

        var response = await client.GetAsync($"{Customers}/{customerId}/timeline");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Empty(page.GetProperty("items").EnumerateArray());
        Assert.Equal(0, page.GetProperty("totalItems").GetInt32());
    }

    // ---------------------------------------------------------------- 4. The size cap

    [Fact]
    public async Task An_oversized_upload_is_413_with_the_attachment_too_large_slug()
    {
        var (client, branchId) = await AgentAsync("cap");
        var customerId = await CreateAsync(client, NewCustomer("cap@x.local", branchId));

        var oversized = new byte[CustomerApiFixture.MaxAttachmentBytes + 1];

        var response = await client.PostAsync(
            $"{Customers}/{customerId}/attachments", Multipart(oversized, "too-big.bin"));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("attachment-too-large", await ProblemTypeAsync(response));

        // Rejected before anything was written: no metadata row appears in the list.
        var list = await client.GetFromJsonAsync<JsonElement>($"{Customers}/{customerId}/attachments");
        Assert.Equal(0, list.GetProperty("totalItems").GetInt32());
    }

    /// <summary>
    /// Verification step 4 — the round trip. The bytes come back identical and the download offers
    /// the <b>original</b> file name, not the server-generated name on disk.
    /// </summary>
    [Fact]
    public async Task An_uploaded_file_can_be_downloaded_again_byte_for_byte()
    {
        var (client, branchId) = await AgentAsync("roundtrip");
        var customerId = await CreateAsync(client, NewCustomer("roundtrip@x.local", branchId));

        var bytes = "attachment round trip"u8.ToArray();

        var upload = await client.PostAsync(
            $"{Customers}/{customerId}/attachments", Multipart(bytes, "original name.txt"));

        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);

        var metadata = await upload.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("original name.txt", metadata.GetProperty("fileName").GetString());
        Assert.Equal(bytes.Length, metadata.GetProperty("sizeBytes").GetInt32());

        // 201's Location points at the download, which is the created resource.
        var download = await client.GetAsync(upload.Headers.Location!.ToString());

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("text/plain", download.Content.Headers.ContentType!.MediaType);
        Assert.Contains("original name.txt", download.Content.Headers.ContentDisposition!.ToString());
    }

    // ---------------------------------------------------------------- 5. AP-4

    /// <summary>
    /// <b>AP-4 — out of scope is <c>404</c>, never <c>403</c>.</b> A <c>403</c> would confirm the id
    /// exists, which is exactly the leak AP-4 prevents. Asserted by comparing a real id the caller
    /// may not reach against one that does not exist: the two answers must be indistinguishable.
    /// </summary>
    [Fact]
    public async Task A_customer_downloading_a_customer_owned_file_gets_404_not_403()
    {
        var (agent, branchId) = await AgentAsync("ap4");
        var customerId = await CreateAsync(agent, NewCustomer("ap4@x.local", branchId));

        var upload = await agent.PostAsync(
            $"{Customers}/{customerId}/attachments", Multipart("secret"u8.ToArray(), "secret.txt"));

        var attachmentId = (await upload.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetString()!;

        var customerUserId = await Factory.AddCustomerRoleUserAsync("ap4.customer@test.local");
        var asCustomer = Factory.CreateClientFor(customerUserId);

        var real = await asCustomer.GetAsync($"{Attachments}/{attachmentId}/content");
        var fictional = await asCustomer.GetAsync($"{Attachments}/{Guid.NewGuid()}/content");

        Assert.Equal(HttpStatusCode.NotFound, real.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, fictional.StatusCode);

        // Not merely the same status — the same body, so nothing distinguishes them.
        Assert.Equal(await ProblemTypeAsync(fictional), await ProblemTypeAsync(real));

        // The download endpoint carries no role policy (AP-19), so this 404 came from owner
        // reachability. Anonymous is still 401, which proves [Authorize] is present.
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await Factory.CreateClient().GetAsync($"{Attachments}/{attachmentId}/content")).StatusCode);
    }

    // ---------------------------------------------------------------- 6. storagePath

    /// <summary>
    /// <b>No response body anywhere in this suite contains <c>storagePath</c></b> — plan task 10's
    /// sixth test, swept across every customer route rather than asserted on one.
    /// <para>
    /// The stored value is read from the database and searched for as well, so a rename of the
    /// property could not make this pass vacuously.
    /// </para>
    /// </summary>
    [Fact]
    public async Task No_response_body_contains_a_storage_path()
    {
        var (client, branchId) = await AgentAsync("nopath");
        var customerId = await CreateAsync(client, NewCustomer("nopath@x.local", branchId));

        await client.PostAsJsonAsync($"{Customers}/{customerId}/notes", new { body = "a note" });
        await client.PostAsync($"{Customers}/{customerId}/attachments", Multipart("x"u8.ToArray(), "f.txt"));

        var storedPath = await Factory.WithDbAsync(db => db.Attachments.AsNoTracking()
            .Where(a => a.CustomerId == customerId).Select(a => a.StoragePath).SingleAsync());

        foreach (var path in new[]
                 {
                     Customers,
                     $"{Customers}/{customerId}",
                     $"{Customers}/{customerId}/timeline",
                     $"{Customers}/{customerId}/notes",
                     $"{Customers}/{customerId}/attachments"
                 })
        {
            var body = await (await client.GetAsync(path)).Content.ReadAsStringAsync();

            Assert.DoesNotContain("storagePath", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(storedPath, body, StringComparison.Ordinal);

            // passwordHash has never appeared in a response and must not start now: the notes and
            // attachment payloads both embed a UserSummary.
            Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---------------------------------------------------------------- Contract details

    /// <summary>
    /// <c>externalReference</c> is the ERP seam's one persisted field, <b>returned read-only and
    /// settable through no endpoint</b> (DM-6, docs/api-design.md §8.3). Sending it changes nothing,
    /// which is what the domain guarantees: the request models have no property for it and
    /// <c>Customer</c> exposes no mutator.
    ///
    /// <para>
    /// <b>Known gap — the request is accepted rather than refused.</b> AP-10 says a request carrying
    /// a server-derived field is a <c>400</c>, "so a client is never misled into thinking it
    /// worked". It is not, because <c>System.Text.Json</c> ignores unmapped members unless
    /// <c>UnmappedMemberHandling.Disallow</c> is configured, and nothing configures it. **This is
    /// pre-existing, not introduced here** — <c>PATCH /users/{id}</c> has behaved the same way since
    /// Story 02. Closing it is a one-line global change to the JSON options that alters the contract
    /// of every endpoint, so it is recorded as finding I-9 rather than taken inside this slice.
    /// </para>
    ///
    /// <para>
    /// This test therefore asserts the guarantee that <em>does</em> hold — the value is unreachable —
    /// and pins the current status so that closing I-9 shows up here as a deliberate change.
    /// </para>
    /// </summary>
    [Fact]
    public async Task External_reference_is_returned_but_settable_through_no_endpoint()
    {
        var (client, branchId) = await AgentAsync("extref");
        var customerId = await CreateAsync(client, NewCustomer("extref@x.local", branchId));

        var fetched = await client.GetFromJsonAsync<JsonElement>($"{Customers}/{customerId}");

        // Absent from the JSON because it is null and nulls are omitted (docs/api-design.md §2) —
        // which is the point: it is never populated by any request.
        Assert.False(fetched.TryGetProperty("externalReference", out _));

        var patched = await client.PatchAsJsonAsync(
            $"{Customers}/{customerId}", new { externalReference = "ERP-123", fullName = "Renamed" });

        // Today: accepted and ignored (I-9). When I-9 is closed this becomes BadRequest.
        Assert.Equal(HttpStatusCode.OK, patched.StatusCode);

        // The assertion that actually matters, and that holds either way: the seam field was NOT
        // written, while the legitimate field alongside it was.
        var row = await Factory.WithDbAsync(db => db.Customers.AsNoTracking()
            .Where(c => c.Id == customerId)
            .Select(c => new { c.ExternalReference, c.FullName })
            .SingleAsync());

        Assert.Null(row.ExternalReference);
        Assert.Equal("Renamed", row.FullName);
    }

    /// <summary>An unknown sort field is a <c>400</c>, never silently ignored (AP-15).</summary>
    [Fact]
    public async Task An_unknown_sort_field_is_rejected()
    {
        var (client, _) = await AgentAsync("sort");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await client.GetAsync($"{Customers}?sort=email")).StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync($"{Customers}?sort=fullName:desc")).StatusCode);
    }

    /// <summary>
    /// A note shows its author and timestamp, and <b>no route edits or deletes one</b> — the
    /// contract half of the immutability rule (docs/data-model.md §5 constraint 16).
    /// </summary>
    [Fact]
    public async Task A_note_is_attributed_and_no_route_can_change_it()
    {
        var (client, branchId) = await AgentAsync("notes.http");
        var customerId = await CreateAsync(client, NewCustomer("notes.http@x.local", branchId));

        var created = await client.PostAsJsonAsync(
            $"{Customers}/{customerId}/notes", new { body = "Called back." });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var note = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Called back.", note.GetProperty("body").GetString());
        Assert.NotEqual("", note.GetProperty("author").GetProperty("displayName").GetString());

        // No updatedAt exists, because the entity is immutable (docs/api-design.md §6.3).
        Assert.False(note.TryGetProperty("updatedAt", out _));

        var noteId = note.GetProperty("id").GetString();

        // There is no such route at all — 404/405, never a success.
        foreach (var attempt in new[]
                 {
                     await client.PatchAsJsonAsync($"{Customers}/{customerId}/notes/{noteId}", new { body = "edited" }),
                     await client.DeleteAsync($"{Customers}/{customerId}/notes/{noteId}")
                 })
        {
            Assert.True(
                attempt.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
                $"expected no edit route, got {(int)attempt.StatusCode}");
        }
    }

    // ---------------------------------------------------------------- Harness

    private const string Customers = "/api/v1/customers";
    private const string Attachments = "/api/v1/attachments";

    private static object NewCustomer(string email, Guid branchId) =>
        new { fullName = email.Split('@')[0], email, branchId };

    private static MultipartFormDataContent Multipart(byte[] bytes, string fileName)
    {
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");

        return new MultipartFormDataContent { { part, "file", fileName } };
    }

    private static async Task<string> ProblemTypeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("type").GetString()!;

    private static async Task<Guid> CreateAsync(HttpClient client, object request)
    {
        var response = await client.PostAsJsonAsync(Customers, request);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<(HttpClient Client, Guid BranchId)> AgentAsync(string slug)
    {
        var branchId = await Factory.EnsureBranchAsync($"{slug} branch");
        var agentId = await Factory.AddStaffUserAsync(UserRole.Agent, $"{slug}.agent@test.local");

        return (Factory.CreateClientFor(agentId), branchId);
    }

    private Task<string> CustomerEmailAsync(Guid id) => Factory.WithDbAsync(db =>
        db.Customers.AsNoTracking().Where(c => c.Id == id).Select(c => c.Email).SingleAsync());

    private async Task<Guid> SeedCustomerAsync(string slug)
    {
        var branchId = await Factory.EnsureBranchAsync($"{slug} branch");

        return await Factory.WithDbAsync(async db =>
        {
            var customer = Customer.Create(
                Guid.NewGuid(), slug, $"{slug}@seeded.local", null, branchId, DateTimeOffset.UtcNow);

            db.Customers.Add(customer);
            await db.SaveChangesAsync();

            return customer.Id;
        });
    }
}

/// <summary>
/// The API host for this suite, with the attachment cap lowered and the storage root pointed at a
/// throwaway directory — <b>through real configuration</b>, so options binding, the real
/// <c>LocalDiskAttachmentStorage</c> and the real cap check are exercised rather than substituted.
/// <para>
/// Class-scoped because the host and its database are: building one host per test would be slow, and
/// the storage root has to outlive any single upload.
/// </para>
/// </summary>
public sealed class CustomerApiFixture : IDisposable
{
    /// <summary>Small enough that an "oversized" upload is a few bytes rather than megabytes.</summary>
    public const long MaxAttachmentBytes = 1024;

    private readonly string _storageRoot = Path.Combine(
        Path.GetTempPath(), $"supportcrm-api-{Guid.NewGuid():N}");

    public SupportCrmApiFactory Factory { get; }

    public CustomerApiFixture()
    {
        Directory.CreateDirectory(_storageRoot);

        Factory = new SupportCrmApiFactory
        {
            ConfigurationOverrides =
            {
                ["SupportCrm:Attachments:StorageRoot"] = _storageRoot,
                ["SupportCrm:Attachments:MaxSizeBytes"] = MaxAttachmentBytes.ToString()
            }
        };
    }

    public void Dispose()
    {
        Factory.Dispose();

        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }
}
