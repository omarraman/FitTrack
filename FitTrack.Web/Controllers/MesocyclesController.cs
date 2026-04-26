using FitTrack.Application.Workouts;
using Microsoft.AspNetCore.Mvc;

namespace FitTrack.Web.Controllers;

[ApiController]
[Route("api/mesocycles")]
public class MesocyclesController : ControllerBase
{
    private readonly IMesocycleService _svc;
    public MesocyclesController(IMesocycleService svc) => _svc = svc;

    [HttpGet]
    public Task<List<MesocycleDto>> List(CancellationToken ct) => _svc.ListAsync(ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MesocycleDto>> Get(int id, CancellationToken ct)
        => (await _svc.GetAsync(id, ct)) is { } m ? Ok(m) : NotFound();

    [HttpPost]
    public async Task<ActionResult<MesocycleDto>> Create(CreateMesocycleDto dto, CancellationToken ct)
    {
        var created = await _svc.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CreateMesocycleDto dto, CancellationToken ct)
        => await _svc.UpdateAsync(id, dto, ct) ? NoContent() : NotFound();

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => await _svc.DeleteAsync(id, ct) ? NoContent() : NotFound();
}

[ApiController]
[Route("api/mesocycle-instances")]
public class MesocycleInstancesController : ControllerBase
{
    private readonly IMesocycleInstanceService _svc;
    public MesocycleInstancesController(IMesocycleInstanceService svc) => _svc = svc;

    public record StartInstanceRequest(int MesocycleId, DateOnly StartDate);

    [HttpGet]
    public Task<List<MesocycleInstanceDto>> List(CancellationToken ct) => _svc.ListAsync(ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MesocycleInstanceDto>> Get(int id, CancellationToken ct)
        => (await _svc.GetAsync(id, ct)) is { } i ? Ok(i) : NotFound();

    [HttpPost("start")]
    public async Task<ActionResult<MesocycleInstanceDto>> Start(StartInstanceRequest req, CancellationToken ct)
    {
        try
        {
            var created = await _svc.StartAsync(req.MesocycleId, req.StartDate, ct);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:int}/complete")]
    public async Task<IActionResult> Complete(int id, CancellationToken ct)
        => await _svc.CompleteAsync(id, ct) ? NoContent() : NotFound();

    [HttpPost("{id:int}/abandon")]
    public async Task<IActionResult> Abandon(int id, CancellationToken ct)
        => await _svc.AbandonAsync(id, ct) ? NoContent() : NotFound();
}
