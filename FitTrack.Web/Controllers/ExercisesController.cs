using FitTrack.Application.Workouts;
using Microsoft.AspNetCore.Mvc;

namespace FitTrack.Web.Controllers;

[ApiController]
[Route("api/exercises")]
public class ExercisesController : ControllerBase
{
    private readonly IExerciseService _svc;
    public ExercisesController(IExerciseService svc) => _svc = svc;

    [HttpGet]
    public Task<List<ExerciseDto>> List(CancellationToken ct) => _svc.ListAsync(ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ExerciseDto>> Get(int id, CancellationToken ct)
        => (await _svc.GetAsync(id, ct)) is { } x ? Ok(x) : NotFound();

    [HttpPost]
    public async Task<ActionResult<ExerciseDto>> Create(CreateExerciseDto dto, CancellationToken ct)
    {
        var created = await _svc.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, CreateExerciseDto dto, CancellationToken ct)
        => await _svc.UpdateAsync(id, dto, ct) ? NoContent() : NotFound();

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => await _svc.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
