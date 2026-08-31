using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Abstractions;
using SupportCrm.Application.Modules.Sla;

namespace SupportCrm.Api.Controllers;

/// <summary>
/// The two notification endpoints of docs/api-design.md §5.10 — and <b>only</b> those two.
///
/// <para>
/// <b><c>[Authorize]</c> with no role policy, deliberately.</b> A notification's audience is one
/// person, not a role: a Manager has no more right to another user's notifications than an Agent
/// does, so the scoping is by recipient in <c>NotificationService</c> and there is nothing here for a
/// policy to add. Every other controller in this API carries a role gate; this one is the exception
/// because §5.10 says <em>"Authenticated"</em> rather than naming a role.
/// </para>
///
/// <para>
/// <b>There is no create action</b> — notifications are raised by the server as a consequence of
/// assignment, breach, escalation or a customer reply, never by a client (§5.10). And <b>there is no
/// <c>read-all</c> action</b>: it was removed from the contract as unrequested surface (AP-18), and
/// the notification screen offers no mark-all-read control either (ui-design §5.8). A test asserts
/// the route does not exist, because "we did not add it" is easy to undo by accident.
/// </para>
/// </summary>
[Authorize]
public sealed class NotificationsController(NotificationService notifications) : ApiControllerBase
{
    /// <summary>
    /// The caller's own notifications, newest first, with <c>unreadCount</c> at the top level of the
    /// envelope for the shell's badge (§6.6).
    /// </summary>
    /// <remarks>
    /// The paging parameter is named <c>paging</c> rather than <c>page</c> for the reason
    /// <c>UsersController.List</c> records: a complex query parameter whose name collides with an
    /// incoming query key switches the model binder to prefix mode and silently leaves
    /// <c>pageSize</c> unbound.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<NotificationPage>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<NotificationPage>> List(
        [FromQuery] bool unreadOnly, [FromQuery] PageQuery paging, CancellationToken ct) =>
        Ok(await notifications.ListAsync(unreadOnly, paging, ct));

    /// <summary>
    /// Marks one notification read — <c>204</c>. <b>Idempotent</b>: a second call leaves the original
    /// <c>readAt</c> untouched and still answers <c>204</c> (docs/data-model.md §5 constraint 22).
    /// Another user's id is <c>404</c> (AP-4).
    /// </summary>
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await notifications.MarkReadAsync(id, ct);

        return NoContent();
    }
}
