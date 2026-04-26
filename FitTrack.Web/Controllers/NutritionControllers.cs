using FitTrack.Application.Nutrition;
using Microsoft.AspNetCore.Mvc;

namespace FitTrack.Web.Controllers;

[ApiController]
[Route("api/foods")]
public class FoodsController : ControllerBase
{
    private readonly IFoodService _svc;
    public FoodsController(IFoodService svc) => _svc = svc;

    [HttpGet] public Task<List<FoodDto>> List([FromQuery] string? search, CancellationToken ct) => _svc.ListAsync(search, ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<FoodDto>> Get(int id, CancellationToken ct)
        => (await _svc.GetAsync(id, ct)) is { } f ? Ok(f) : NotFound();

    [HttpPost]
    public async Task<ActionResult<FoodDto>> Create(UpsertFoodDto dto, CancellationToken ct)
        => Ok(await _svc.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpsertFoodDto dto, CancellationToken ct)
        => await _svc.UpdateAsync(id, dto, ct) ? NoContent() : NotFound();

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => await _svc.DeleteAsync(id, ct) ? NoContent() : NotFound();
}

[ApiController]
[Route("api/recipes")]
public class RecipesController : ControllerBase
{
    private readonly IRecipeService _svc;
    public RecipesController(IRecipeService svc) => _svc = svc;

    [HttpGet] public Task<List<RecipeDto>> List(CancellationToken ct) => _svc.ListAsync(ct);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RecipeDto>> Get(int id, CancellationToken ct)
        => (await _svc.GetAsync(id, ct)) is { } r ? Ok(r) : NotFound();

    [HttpPost]
    public async Task<ActionResult<RecipeDto>> Create(UpsertRecipeDto dto, CancellationToken ct)
        => Ok(await _svc.CreateAsync(dto, ct));

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpsertRecipeDto dto, CancellationToken ct)
        => await _svc.UpdateAsync(id, dto, ct) ? NoContent() : NotFound();

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => await _svc.DeleteAsync(id, ct) ? NoContent() : NotFound();
}

[ApiController]
[Route("api/meals")]
public class MealsController : ControllerBase
{
    private readonly IMealEntryService _svc;
    public MealsController(IMealEntryService svc) => _svc = svc;

    [HttpGet("day/{date}")]
    public Task<DailyNutritionDto> Day(DateOnly date, CancellationToken ct) => _svc.GetDayAsync(date, ct);

    [HttpPost]
    public async Task<ActionResult<MealEntryDto>> Create(UpsertMealEntryDto dto, CancellationToken ct)
    {
        try { return Ok(await _svc.CreateAsync(dto, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpsertMealEntryDto dto, CancellationToken ct)
    {
        try { return await _svc.UpdateAsync(id, dto, ct) ? NoContent() : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => await _svc.DeleteAsync(id, ct) ? NoContent() : NotFound();
}
