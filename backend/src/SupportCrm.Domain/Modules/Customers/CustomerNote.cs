namespace SupportCrm.Domain.Modules.Customers;

/// <summary>
/// An agent's note on a customer — requirements §1.4, T1-A, docs/data-model.md §2.5. Distinct from a
/// ticket internal note (§2.9), which is about one ticket.
/// <para>
/// <b>Immutable once written, structurally.</b> The <c>customer-records</c> intake requires a note to
/// be attributed and "not silently editable by other users", and immutability is the cheapest
/// guarantee of that (docs/data-model.md §5 constraint 16). So this type has private setters and
/// <b>no mutator method at all</b> — not merely no endpoint. There is consequently no
/// <c>updatedAt</c> to model or return (docs/api-design.md §6.3), and no edit or delete control is
/// rendered anywhere, because none exists to call (docs/ui-design.md §5.5).
/// </para>
/// <para>
/// Staff-visible only; never exposed to the portal (docs/data-model.md §2.5). Customer notes are a
/// separate collection and do <b>not</b> appear in the interaction timeline
/// (docs/api-design.md §5.5).
/// </para>
/// Plain C# with no EF attributes (AD-4).
/// </summary>
public sealed class CustomerNote
{
    private CustomerNote()
    {
        // EF Core materialization.
    }

    public Guid Id { get; private set; }

    /// <summary>The owning profile.</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>
    /// The authoring agent. Attribution is required by the <c>customer-records</c> intake, and it is
    /// <b>server-set from <c>ICurrentUser</c></b> — never accepted from a client
    /// (docs/api-design.md §7).
    /// </summary>
    public Guid AuthorUserId { get; private set; }

    /// <summary>Plain text. The <c>Text</c> tier of docs/data-model.md §6.1 — searched, never indexed.</summary>
    public string Body { get; private set; } = default!;

    /// <summary>Server-set, like the author (docs/api-design.md §7).</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// The only way a note comes into existence, and the only state it will ever have.
    /// </summary>
    public static CustomerNote Write(
        Guid id,
        Guid customerId,
        Guid authorUserId,
        string body,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("A note requires a customer.", nameof(customerId));
        }

        if (authorUserId == Guid.Empty)
        {
            throw new ArgumentException("A note requires an author.", nameof(authorUserId));
        }

        return new CustomerNote
        {
            Id = id,
            CustomerId = customerId,
            AuthorUserId = authorUserId,
            Body = body.Trim(),
            CreatedAt = createdAt,
        };
    }
}
