using FitTrack.Application.Health;
using Microsoft.AspNetCore.Mvc;

namespace FitTrack.Web.Controllers;

[ApiController]
[Route("api/pelvic-floor")]
public class PelvicFloorController : ControllerBase
{
    private readonly IPelvicFloorTrainingService _svc;
    public PelvicFloorController(IPelvicFloorTrainingService svc) => _svc = svc;

    [HttpGet("program")]
    public async Task<ActionResult<PelvicFloorProgramSummaryDto>> GetProgram(CancellationToken ct)
    {
        var summary = await _svc.GetActiveOrMostRecentAsync(ct);
        return summary is null ? NoContent() : Ok(summary);
    }

    [HttpPost("programs")]
    public async Task<ActionResult<PelvicFloorProgramSummaryDto>> Start(StartPelvicFloorProgramDto dto, CancellationToken ct)
        => Ok(await _svc.StartAsync(dto, ct));

    [HttpPost("programs/{programId:int}/sessions")]
    public async Task<ActionResult<PelvicFloorSessionLogDto>> LogSession(int programId, LogPelvicFloorSessionDto dto, CancellationToken ct)
        => Ok(await _svc.LogSessionAsync(programId, dto, ct));

    [HttpPut("programs/{programId:int}/check-ins")]
    public async Task<ActionResult<PelvicFloorDailyCheckInDto>> UpsertCheckIn(int programId, UpsertPelvicFloorCheckInDto dto, CancellationToken ct)
        => Ok(await _svc.UpsertCheckInAsync(programId, dto, ct));

    [HttpPost("programs/{programId:int}/complete")]
    public async Task<IActionResult> Complete(int programId, PelvicFloorProgramDateDto dto, CancellationToken ct)
        => await _svc.CompleteAsync(programId, dto.Date, ct) ? NoContent() : NotFound();

    [HttpPost("programs/{programId:int}/cancel")]
    public async Task<IActionResult> Cancel(int programId, PelvicFloorProgramDateDto dto, CancellationToken ct)
        => await _svc.CancelAsync(programId, dto.Date, ct) ? NoContent() : NotFound();

    [HttpDelete("programs/{programId:int}/sessions/{sessionId:int}")]
    public async Task<IActionResult> DeleteSession(int programId, int sessionId, CancellationToken ct)
        => await _svc.DeleteSessionAsync(programId, sessionId, ct) ? NoContent() : NotFound();

    [HttpDelete("programs/{programId:int}/check-ins/{checkInId:int}")]
    public async Task<IActionResult> DeleteCheckIn(int programId, int checkInId, CancellationToken ct)
        => await _svc.DeleteCheckInAsync(programId, checkInId, ct) ? NoContent() : NotFound();
}
