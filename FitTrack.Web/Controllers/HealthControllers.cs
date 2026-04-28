using FitTrack.Application.Health;
using FitTrack.Domain.Health;
using Microsoft.AspNetCore.Mvc;

namespace FitTrack.Web.Controllers;

[ApiController]
[Route("api/body-measurements")]
public class BodyMeasurementsController : ControllerBase
{
    private readonly IBodyMeasurementService _svc;
    public BodyMeasurementsController(IBodyMeasurementService svc) => _svc = svc;

    [HttpGet] public Task<List<BodyMeasurementDto>> List(CancellationToken ct) => _svc.ListAsync(ct);

    [HttpPost]
    public async Task<ActionResult<BodyMeasurementDto>> Create(UpsertBodyMeasurementDto dto, CancellationToken ct)
        => Ok(await _svc.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpsertBodyMeasurementDto dto, CancellationToken ct)
        => await _svc.UpdateAsync(id, dto, ct) ? NoContent() : NotFound();

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => await _svc.DeleteAsync(id, ct) ? NoContent() : NotFound();
}

[ApiController]
[Route("api/body-parts")]
public class BodyPartMeasurementsController : ControllerBase
{
    private readonly IBodyPartMeasurementService _svc;
    public BodyPartMeasurementsController(IBodyPartMeasurementService svc) => _svc = svc;

    [HttpGet] public Task<List<BodyPartMeasurementDto>> List([FromQuery] BodyPart? part, CancellationToken ct) => _svc.ListAsync(part, ct);

    [HttpPost]
    public async Task<ActionResult<BodyPartMeasurementDto>> Create(UpsertBodyPartMeasurementDto dto, CancellationToken ct)
        => Ok(await _svc.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpsertBodyPartMeasurementDto dto, CancellationToken ct)
        => await _svc.UpdateAsync(id, dto, ct) ? NoContent() : NotFound();

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => await _svc.DeleteAsync(id, ct) ? NoContent() : NotFound();
}

[ApiController]
[Route("api/blood-pressure")]
public class BloodPressureController : ControllerBase
{
    private readonly IBloodPressureService _svc;
    public BloodPressureController(IBloodPressureService svc) => _svc = svc;

    [HttpGet] public Task<List<BloodPressureDto>> List(CancellationToken ct) => _svc.ListAsync(ct);

    [HttpPost]
    public async Task<ActionResult<BloodPressureDto>> LogSession(LogBpSessionDto dto, CancellationToken ct)
        => Ok(await _svc.LogSessionAsync(dto, ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => await _svc.DeleteAsync(id, ct) ? NoContent() : NotFound();
}

[ApiController]
[Route("api/colds")]
public class ColdsController : ControllerBase
{
    private readonly IColdEpisodeService _svc;
    public ColdsController(IColdEpisodeService svc) => _svc = svc;

    [HttpGet] public Task<List<ColdEpisodeDto>> List([FromQuery] int? year, CancellationToken ct) => _svc.ListAsync(year, ct);

    [HttpPost]
    public async Task<ActionResult<ColdEpisodeDto>> Create(UpsertColdEpisodeDto dto, CancellationToken ct)
        => Ok(await _svc.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpsertColdEpisodeDto dto, CancellationToken ct)
        => await _svc.UpdateAsync(id, dto, ct) ? NoContent() : NotFound();

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => await _svc.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
