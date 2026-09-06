using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Identity;
using SupportCrm.Domain.Modules.Tickets;

namespace SupportCrm.Application.Modules.Tickets;

/// <summary>
/// <b>The staff-only internal note surface</b> — requirements §4.5, T2-C, docs/data-model.md §2.9,
/// docs/api-design.md §5.6.
///
/// <para>
/// <b>The visibility rule is structural, and this service is deliberately not where it is
/// enforced.</b> There is no filter here that keeps customers out, because there is nothing to
/// filter: <c>TicketInternalNote</c> is a separate table that <b>no customer-facing query names</b>
/// (§2.9's closing invariant, architecture §4.4). The three customer-facing reads were each written
/// before this entity existed and each remains correct without change:
/// </para>
///
/// <list type="table">
///   <item>
///     <term>Portal thread — <c>GET /portal/tickets/{id}/messages</c></term>
///     <description>Queries <c>TicketMessage</c>. It does not join this table (Story 07)</description>
///   </item>
///   <item>
///     <term>Customer timeline — <c>GET /customers/{id}/timeline</c></term>
///     <description>Excludes <c>Internal</c> visibility <b>and</b> does not join this table — two
///     independent reasons (Story 04 task 5, Story 06 task 6, §5 constraint 18)</description>
///   </item>
///   <item>
///     <term>Notifications</term>
///     <description>Four types (A-13), none of which is a note. This service raises none</description>
///   </item>
/// </list>
///
/// <para>
/// <b>The role gate is the other half, and it is on the path.</b> Every route that reaches this
/// service is <c>RequireAgent</c> on the staff controller, and <b>no <c>/portal</c> counterpart
/// exists or may be added</b> (AP-5) — <em>"the path space is the visibility rule"</em>.
/// </para>
///
/// <para>
/// <b>There is no update and no delete method here, by construction</b> (§5 constraint 16). A note
/// is immutable once written; a correction is a new note. A reflection test asserts this type's
/// public surface rather than trusting the comment.
/// </para>
///
/// <para>
/// <b>One unit of work, committed once</b> (architecture §3). The note and its activity row are
/// added together and saved together, so no committed state has one without the other
/// (§5 constraint 17).
/// </para>
/// </summary>
public sealed class TicketInternalNoteService(
    IApplicationDbContext db,
    ICurrentUser currentUser,
    TicketActivityRecorder activity,
    TimeProvider clock)
{
    /// <summary>
    /// <c>GET /tickets/{id}/internal-notes</c> — paged, <b>oldest first</b>.
    ///
    /// <para>
    /// A note thread is read forwards for the same reason a message thread is: it is a running
    /// commentary, not a change log. The <c>TicketInternalNote(TicketId, CreatedAt)</c> index of
    /// docs/data-model.md §6 exists for exactly this read.
    /// </para>
    ///
    /// <para>
    /// <b>Scope first</b>: an unreachable ticket's notes are <c>404</c>, worded identically to a
    /// ticket that does not exist (AP-4). Reading notes must not reveal what reading the ticket
    /// hides — so an out-of-department agent learns nothing.
    /// </para>
    /// </summary>
    public async Task<PagedResult<InternalNoteDto>> ListAsync(
        Guid ticketId, PageQuery? page, CancellationToken ct)
    {
        _ = await db.Tickets.LoadScopedAsync(ticketId, currentUser, ct);

        var (pageNumber, pageSize) = page.Normalize();

        return await db.TicketInternalNotes.AsNoTracking()
            .Where(n => n.TicketId == ticketId)
            .OrderBy(n => n.CreatedAt)
            .ThenBy(n => n.Id) // a stable tiebreak, so paging is meaningful within one timestamp
            .Select(n => new InternalNoteDto(
                n.Id,
                n.TicketId,
                db.Users.Where(u => u.Id == n.AuthorUserId)
                    .Select(u => new UserSummaryDto(u.Id, u.DisplayName)).First(),
                n.Body,
                n.CreatedAt))
            .ToPagedResultAsync(pageNumber, pageSize, ct);
    }

    /// <summary>
    /// <c>POST /tickets/{id}/internal-notes</c>.
    ///
    /// <para>
    /// <b>The order of operations mirrors <c>TicketMessageService.PostAsync</c> and must not be
    /// rearranged:</b>
    /// </para>
    /// <list type="number">
    ///   <item><description><b>Scope</b> — <c>LoadScopedAsync</c>. Missing <em>or</em> out of scope is <c>404</c>, worded identically (AP-4).</description></item>
    ///   <item><description><b>Terminal guard</b> — <c>Closed</c> or <c>Cancelled</c> is <c>409 ticket-terminal</c> (A-5, §5 constraint 8: <em>"no messages, notes or further transitions"</em>).</description></item>
    ///   <item><description>Persist the note. Immutable from this point on — there is no mutator.</description></item>
    ///   <item><description>Write <b>exactly one</b> <c>InternalNotePosted</c> activity row, linking the note (§5 constraint 17), <b>always at <c>Internal</c> visibility</b> — fixed by the factory, not by this call site.</description></item>
    /// </list>
    ///
    /// <para>
    /// <b>Scope before the terminal guard</b>, for the same reason every other write path does it:
    /// a <c>409</c> on someone else's ticket would confirm that it exists (AP-4).
    /// </para>
    ///
    /// <para>
    /// <b>No notification is raised.</b> A-13 enumerates four types and none of them is a note
    /// (docs/data-model.md §2.12) — and inventing one would be the @mention feature T2-C excludes.
    /// A test asserts the silence.
    /// </para>
    /// </summary>
    public async Task<InternalNoteDto> CreateAsync(Guid ticketId, string body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ValidationException("An internal note body is required.");
        }

        // 1. Scope. Fetch-then-authorize, never authorize-then-fetch-by-id (architecture §4.3).
        var ticket = await db.Tickets.LoadScopedAsync(ticketId, currentUser, ct);

        // 2. Terminal. The same rule and the SAME SLUG the reply path uses — §5 constraint 8 names
        //    notes alongside messages, so this is one rule with two call sites, not a new one.
        if (ticket.Status is TicketStatus.Closed or TicketStatus.Cancelled)
        {
            throw new ConflictException(TicketTerminal, TicketTerminalMessage);
        }

        var createdAt = clock.GetUtcNow();

        // 3. Persist. Author and timestamp come from the server, never from the request.
        var note = TicketInternalNote.Write(
            Guid.NewGuid(), ticket.Id, currentUser.Id, body, createdAt);

        db.TicketInternalNotes.Add(note);

        // 4. Exactly one InternalNotePosted row, linking the note. The BODY IS NOT COPIED (DM-4),
        //    and the visibility is not passed: TicketActivity.InternalNotePosted always writes
        //    Internal, so §2.7's "always Internal" cannot be got wrong here.
        await activity.RecordInternalNotePostedAsync(ticket.Id, note.Id, ct);

        await db.SaveChangesAsync(ct);

        return new InternalNoteDto(
            note.Id,
            note.TicketId,
            new UserSummaryDto(currentUser.Id, currentUser.DisplayName),
            note.Body,
            note.CreatedAt);
    }

    /// <summary>
    /// The slug for a note on a terminal ticket — <b>reused, not minted</b>. Story 07 introduced
    /// <c>ticket-terminal</c> for the same rule on the reply path, and §5 constraint 8 is one
    /// constraint covering messages and notes together (docs/api-design.md §6.12).
    /// </summary>
    private const string TicketTerminal = "ticket-terminal";

    private const string TicketTerminalMessage =
        "This ticket is closed. No further notes are accepted.";
}
