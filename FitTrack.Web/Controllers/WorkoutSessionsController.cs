using FitTrack.Application.Workouts;
using Microsoft.AspNetCore.Mvc;

namespace FitTrack.Web.Controllers;

[ApiController]
[Route("api/workout-sessions")]
public class WorkoutSessionsController : ControllerBase
{
    private readonly IWorkoutSessionService _svc;
    public WorkoutSessionsController(IWorkoutSessionService svc) => _svc = svc;

    [HttpGet("today")]
    public async Task<ActionResult<WorkoutSessionDto>> Today(CancellationToken ct)
        => (await _svc.GetTodaysSessionAsync(ct)) is { } s ? Ok(s) : NotFound();

    [HttpGet("by-instance/{instanceId:int}")]
    public Task<List<WorkoutSessionDto>> ByInstance(int instanceId, CancellationToken ct)
        => _svc.ListForInstanceAsync(instanceId, ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<WorkoutSessionDto>> Get(int id, CancellationToken ct)
        => (await _svc.GetAsync(id, ct)) is { } s ? Ok(s) : NotFound();

    [HttpPut("logs/{logId:int}")]
    public async Task<IActionResult> UpdateLog(int logId, UpdateExerciseLogDto dto, CancellationToken ct)
        => await _svc.LogSetAsync(logId, dto, ct) ? NoContent() : NotFound();

    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id, CancellationToken ct)
        => await _svc.CompleteSessionAsync(id, ct) ? NoContent() : NotFound();
}
