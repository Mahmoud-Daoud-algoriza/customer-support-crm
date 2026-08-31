using SupportCrm.Application.Abstractions;

namespace SupportCrm.Application.Modules.Ai;

/// <summary>
/// <b>The AI seam is unavailable</b> — a provider timeout, a non-success status, or a response that
/// could not be read.
///
/// <para>
/// It derives from <see cref="SeamUnavailableException"/>, which carries the <c>ai-unavailable</c>
/// slug, so the Story 01 Problem Details handler maps it to <b><c>503</c> with no per-endpoint
/// code</b> (AP-12) — and <c>503</c> is the only place in this API that status is used
/// (docs/api-design.md §2.2).
/// </para>
///
/// <para>
/// <b>It is the only exception type that crosses this seam.</b> A provider or transport exception
/// must never bubble into Application: the layer above knows "AI is unavailable" and nothing about
/// how, which is what keeps the provider question open (product-scope §9 question 1) and keeps a
/// provider's exception types out of the contract.
/// </para>
///
/// <para>
/// <b>Its consequence is one degraded feature, never blocked work.</b> Ticket creation, replies and
/// status changes continue when this is thrown — asserted by
/// <c>AiOutageDoesNotBlockWorkTests</c> (T1-F).
/// </para>
/// </summary>
public sealed class AiUnavailableException(
    string message = "The AI service is unavailable.", Exception? inner = null)
    : SeamUnavailableException(message, inner);
