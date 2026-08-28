using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Identity;
using SupportCrm.Domain.Modules.Customers;

namespace SupportCrm.Application.Modules.Customers;

/// <summary>
/// Notes on a customer — the two note endpoints of docs/api-design.md §5.5 (Story 04 task 4).
/// Both are <c>RequireAgent</c>; the policy is applied by the controller, which arrives with task 8.
///
/// <para>
/// <b>There is no update method and no delete method — not merely no endpoint.</b> A note is
/// immutable once written (docs/data-model.md §2.5, §5 constraint 16), and the
/// <c>customer-records</c> intake requires notes to be attributed and "not silently editable by
/// another user". <see cref="CustomerNote"/> makes that structural by exposing no mutator, and this
/// service keeps it that way by offering nothing that could call one. Adding an edit path here is a
/// change to the data model, not a feature.
/// </para>
///
/// <para>
/// <b>Author and timestamp are server-set from <see cref="ICurrentUser"/></b> and are never accepted
/// from a client (docs/api-design.md §7). <see cref="CreateNoteRequest"/> has exactly one property,
/// which is how AP-10 enforces that: a request carrying an author or a timestamp is a <c>400</c>.
/// </para>
/// </summary>
public sealed class CustomerNoteService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    TimeProvider clock)
{
    /// <summary>
    /// <c>GET /customers/{id}/notes</c> — paged, <b>newest first</b> (docs/api-design.md §5.5).
    /// <para>
    /// The order is fixed rather than sortable: §5.5 states it, and no document offers a sort
    /// parameter for this endpoint. AP-15's whitelist rule is about endpoints that <em>accept</em> a
    /// sort; the smaller surface is the one that publishes none.
    /// </para>
    /// </summary>
    public async Task<PagedResult<CustomerNoteDto>> ListAsync(
        Guid customerId, PageQuery? page, CancellationToken ct)
    {
        await EnsureCustomerExistsAsync(customerId, ct);

        var (pageNumber, pageSize) = page.Normalize();

        // Joined to User for the author's display name — docs/api-design.md §6.3 embeds a
        // UserSummary, not a bare id. An INNER join is correct: AuthorUserId is required and carries
        // a Restrict foreign key, and a user is deactivated rather than deleted (§2.1), so the
        // author of a note always resolves.
        return await (from n in db.CustomerNotes.AsNoTracking().Where(n => n.CustomerId == customerId)
                      join u in db.Users.AsNoTracking() on n.AuthorUserId equals u.Id
                      orderby n.CreatedAt descending
                      select new CustomerNoteDto(
                          n.Id,
                          new UserSummaryDto(u.Id, u.DisplayName),
                          n.Body,
                          n.CreatedAt))
            .ToPagedResultAsync(pageNumber, pageSize, ct);
    }

    /// <summary>
    /// <c>POST /customers/{id}/notes</c> — <c>{ body }</c>, and nothing else.
    /// <para>
    /// The note is written once and can never be changed. <b>Not audited</b>: a note is business
    /// data, not a security event (AD-10).
    /// </para>
    /// </summary>
    public async Task<CustomerNoteDto> AddAsync(
        Guid customerId, CreateNoteRequest request, CancellationToken ct)
    {
        await EnsureCustomerExistsAsync(customerId, ct);

        // Both server-set. ICurrentUser is the only source of caller identity in this layer
        // (docs/architecture.md §4.3), so there is no way for a client-supplied author to reach here
        // even if the request model grew one.
        var note = CustomerNote.Write(
            id: Guid.NewGuid(),
            customerId: customerId,
            authorUserId: currentUser.Id,
            body: request.Body,
            createdAt: clock.GetUtcNow());

        db.CustomerNotes.Add(note);

        await db.SaveChangesAsync(ct);

        return new CustomerNoteDto(
            note.Id,
            new UserSummaryDto(currentUser.Id, currentUser.DisplayName),
            note.Body,
            note.CreatedAt);
    }

    /// <summary>
    /// A note on a customer that does not exist is a <c>404</c> against the customer, not an
    /// orphaned row and not a foreign-key error surfacing as a <c>500</c>.
    /// </summary>
    private async Task EnsureCustomerExistsAsync(Guid customerId, CancellationToken ct)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == customerId, ct))
        {
            throw new NotFoundException("Customer not found.");
        }
    }
}
