namespace SupportCrm.Domain.Modules.Knowledge;

/// <summary>
/// One knowledge-base article — docs/data-model.md §2.13. FAQs, help articles and solution guides
/// are <b>one entity with a type</b> (T2-E), not three subsystems.
///
/// <para>
/// <b>There is no navigation to <c>Ticket</c>, and none may be added.</b> §2.13 is explicit:
/// <em>"Deliberately no relationship to <c>Ticket</c>: suggested solutions are computed by keyword
/// retrieval at read time (AD-13), not stored links."</em> Adding one would turn §7.4's retrieval
/// into a curated mapping nobody asked to maintain.
/// </para>
///
/// <para>
/// <b>No versioning.</b> §2.13 excludes it outright, so an edit overwrites and bumps
/// <see cref="UpdatedAt"/>. There is no history table, no revision number and no draft copy.
/// </para>
///
/// <para>
/// <b><see cref="Publish"/> and <see cref="Unpublish"/> are separate from <see cref="Update"/>, and
/// that is the one-path rule of docs/api-design.md §6.11 enforced by the entity rather than only by
/// the controller.</b> <c>PATCH</c> cannot reach <see cref="IsPublished"/> because <c>Update</c>
/// does not take it — so publication state changes through <c>/publish</c> and <c>/unpublish</c>
/// alone, and no future request model can accidentally re-open the second path.
/// </para>
///
/// <para>
/// <b>There is no deletion concept here</b> (T2-E, docs/ui-design.md §6): no delete method, no
/// soft-delete flag, and no endpoint anywhere that would call one.
/// </para>
///
/// Plain C# with no EF attributes (AD-4).
/// </summary>
public sealed class KnowledgeArticle
{
    private KnowledgeArticle()
    {
        // EF Core materialization.
    }

    public Guid Id { get; private set; }

    /// <summary>Searched, alongside <see cref="Body"/> (AD-13). Name tier (§6.1).</summary>
    public string Title { get; private set; } = default!;

    /// <summary>
    /// Plain text / basic markdown. Searched. <b>No rich text, no media, no embedded files</b>
    /// (T2-E) — the column holds authored text and nothing else. Text tier, never indexed (§6.1).
    /// </summary>
    public string Body { get; private set; } = default!;

    public ArticleType Type { get; private set; }

    public ArticleVisibility Visibility { get; private set; }

    /// <summary>
    /// <b>Defaults to false</b>, so an article is drafted before it is visible
    /// (docs/api-design.md §6.11). Changed only by <see cref="Publish"/> and
    /// <see cref="Unpublish"/>.
    /// </summary>
    public bool IsPublished { get; private set; }

    /// <summary>
    /// The Administrator who authored it — authoring is an Administrator capability (A-4). Set once,
    /// at creation, from the authenticated caller; it is never supplied by a client
    /// (docs/api-design.md §6.11, §7).
    /// </summary>
    public Guid AuthorUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Bumped by every <see cref="Update"/>; the list shows recency (§2.13).</summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// The only way an article comes into existence. <paramref name="isPublished"/> is accepted
    /// because <c>POST /kb/articles</c> may create an already-published article; it defaults to
    /// false at the contract edge, not here.
    /// </summary>
    public static KnowledgeArticle Create(
        Guid id,
        string title,
        string body,
        ArticleType type,
        ArticleVisibility visibility,
        bool isPublished,
        Guid authorUserId,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        if (authorUserId == Guid.Empty)
        {
            throw new ArgumentException("An article must have an author (A-4).", nameof(authorUserId));
        }

        return new KnowledgeArticle
        {
            Id = id,
            Title = title,
            Body = body,
            Type = type,
            Visibility = visibility,
            IsPublished = isPublished,
            AuthorUserId = authorUserId,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        };
    }

    /// <summary>
    /// The editable fields of <c>PATCH /kb/articles/{id}</c> — and <b>only</b> those four
    /// (docs/api-design.md §6.11). A null argument means "leave unchanged", which is what a
    /// <c>PATCH</c> carries.
    /// <para>
    /// <b><see cref="IsPublished"/> is not a parameter, deliberately.</b> That absence is the
    /// one-path rule: publication moves through <see cref="Publish"/> and <see cref="Unpublish"/>,
    /// so it cannot be written as a field.
    /// </para>
    /// </summary>
    public void Update(
        string? title,
        string? body,
        ArticleType? type,
        ArticleVisibility? visibility,
        DateTimeOffset now)
    {
        if (title is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(title);
            Title = title;
        }

        if (body is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(body);
            Body = body;
        }

        if (type is { } newType)
        {
            Type = newType;
        }

        if (visibility is { } newVisibility)
        {
            Visibility = newVisibility;
        }

        UpdatedAt = now;
    }

    /// <summary>
    /// <c>POST /kb/articles/{id}/publish</c>. <b>Idempotent</b>: publishing an already-published
    /// article is not an error, so a repeated press answers the same way rather than manufacturing
    /// a conflict the contract does not define (docs/api-design.md §5.9 lists no <c>409</c> here).
    /// </summary>
    public void Publish(DateTimeOffset now)
    {
        if (IsPublished)
        {
            return;
        }

        IsPublished = true;
        UpdatedAt = now;
    }

    /// <inheritdoc cref="Publish"/>
    public void Unpublish(DateTimeOffset now)
    {
        if (!IsPublished)
        {
            return;
        }

        IsPublished = false;
        UpdatedAt = now;
    }
}
