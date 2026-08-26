using Microsoft.AspNetCore.Mvc;

namespace SupportCrm.Api.Controllers;

/// <summary>
/// The <c>/api/v1</c> prefix is applied here, once. No controller repeats it by hand
/// (docs/api-design.md §2, .squad/plans/00-implementation-plan.md §6).
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public abstract class ApiControllerBase : ControllerBase;
