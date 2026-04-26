using FitTrack.Application.Abstractions;
using FitTrack.Domain.Nutrition;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.Application.Nutrition;

public interface IFoodService
{
    Task<List<FoodDto>> ListAsync(string? search, CancellationToken ct = default);
    Task<FoodDto?> GetAsync(int id, CancellationToken ct = default);
    Task<FoodDto> CreateAsync(UpsertFoodDto dto, CancellationToken ct = default);
    Task<bool> UpdateAsync(int id, UpsertFoodDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

public class FoodService : IFoodService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _current;

    public FoodService(IAppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<List<FoodDto>> ListAsync(string? search, CancellationToken ct = default)
    {
        _current.RequireUserId();
        var q = _db.Foods.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(f => f.Name.ToLower().Contains(s) || (f.Brand != null && f.Brand.ToLower().Contains(s)));
        }
        return await q.OrderBy(f => f.Name)
            .Select(f => new FoodDto(f.Id, f.Name, f.Brand,
                f.CaloriesPer100g, f.ProteinPer100g, f.CarbsPer100g, f.FatPer100g, f.FiberPer100g))
            .ToListAsync(ct);
    }

    public async Task<FoodDto?> GetAsync(int id, CancellationToken ct = default)
    {
        _current.RequireUserId();
        var f = await _db.Foods.FindAsync(new object?[] { id }, ct);
        return f is null ? null : new FoodDto(f.Id, f.Name, f.Brand,
            f.CaloriesPer100g, f.ProteinPer100g, f.CarbsPer100g, f.FatPer100g, f.FiberPer100g);
    }

    public async Task<FoodDto> CreateAsync(UpsertFoodDto dto, CancellationToken ct = default)
    {
        RequireAdmin();
        var f = new Food
        {
            Name = dto.Name.Trim(),
            Brand = dto.Brand,
            CaloriesPer100g = dto.CaloriesPer100g,
            ProteinPer100g = dto.ProteinPer100g,
            CarbsPer100g = dto.CarbsPer100g,
            FatPer100g = dto.FatPer100g,
            FiberPer100g = dto.FiberPer100g
        };
        _db.Foods.Add(f);
        await _db.SaveChangesAsync(ct);
        return new FoodDto(f.Id, f.Name, f.Brand, f.CaloriesPer100g, f.ProteinPer100g, f.CarbsPer100g, f.FatPer100g, f.FiberPer100g);
    }

    public async Task<bool> UpdateAsync(int id, UpsertFoodDto dto, CancellationToken ct = default)
    {
        RequireAdmin();
        var f = await _db.Foods.FindAsync(new object?[] { id }, ct);
        if (f is null) return false;
        f.Name = dto.Name.Trim();
        f.Brand = dto.Brand;
        f.CaloriesPer100g = dto.CaloriesPer100g;
        f.ProteinPer100g = dto.ProteinPer100g;
        f.CarbsPer100g = dto.CarbsPer100g;
        f.FatPer100g = dto.FatPer100g;
        f.FiberPer100g = dto.FiberPer100g;
        f.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        RequireAdmin();
        var f = await _db.Foods.FindAsync(new object?[] { id }, ct);
        if (f is null) return false;
        _db.Foods.Remove(f);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private void RequireAdmin()
    {
        _current.RequireUserId();
        if (!_current.IsAdmin)
            throw new ForbiddenException("Only admins can modify shared foods.");
    }
}

public interface IRecipeService
{
    Task<List<RecipeDto>> ListAsync(CancellationToken ct = default);
    Task<RecipeDto?> GetAsync(int id, CancellationToken ct = default);
    Task<RecipeDto> CreateAsync(UpsertRecipeDto dto, CancellationToken ct = default);
    Task<bool> UpdateAsync(int id, UpsertRecipeDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

public class RecipeService : IRecipeService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _current;

    public RecipeService(IAppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<List<RecipeDto>> ListAsync(CancellationToken ct = default)
    {
        _current.RequireUserId();
        var recipes = await _db.Recipes
            .AsNoTracking()
            .Include(r => r.Ingredients).ThenInclude(i => i.Food)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);
        return recipes.Select(ToDto).ToList();
    }

    public async Task<RecipeDto?> GetAsync(int id, CancellationToken ct = default)
    {
        _current.RequireUserId();
        var r = await _db.Recipes
            .AsNoTracking()
            .Include(r => r.Ingredients).ThenInclude(i => i.Food)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        return r is null ? null : ToDto(r);
    }

    public async Task<RecipeDto> CreateAsync(UpsertRecipeDto dto, CancellationToken ct = default)
    {
        RequireAdmin();
        var r = new Recipe
        {
            Name = dto.Name.Trim(),
            Description = dto.Description,
            Servings = Math.Max(1, dto.Servings),
            Ingredients = dto.Ingredients.Select(i => new RecipeIngredient
            {
                FoodId = i.FoodId,
                Grams = i.Grams
            }).ToList()
        };
        _db.Recipes.Add(r);
        await _db.SaveChangesAsync(ct);
        return (await GetAsync(r.Id, ct))!;
    }

    public async Task<bool> UpdateAsync(int id, UpsertRecipeDto dto, CancellationToken ct = default)
    {
        RequireAdmin();
        var r = await _db.Recipes.Include(r => r.Ingredients).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (r is null) return false;

        r.Name = dto.Name.Trim();
        r.Description = dto.Description;
        r.Servings = Math.Max(1, dto.Servings);
        r.UpdatedAt = DateTimeOffset.UtcNow;

        _db.RecipeIngredients.RemoveRange(r.Ingredients);
        r.Ingredients = dto.Ingredients.Select(i => new RecipeIngredient
        {
            FoodId = i.FoodId,
            Grams = i.Grams
        }).ToList();

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        RequireAdmin();
        var r = await _db.Recipes.FindAsync(new object?[] { id }, ct);
        if (r is null) return false;
        _db.Recipes.Remove(r);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private void RequireAdmin()
    {
        _current.RequireUserId();
        if (!_current.IsAdmin)
            throw new ForbiddenException("Only admins can modify shared recipes.");
    }

    public static RecipeDto ToDto(Recipe r)
    {
        decimal totCal = 0, totP = 0, totC = 0, totF = 0;
        var ingredients = new List<RecipeIngredientDto>();
        foreach (var i in r.Ingredients)
        {
            var factor = i.Grams / 100m;
            var cal = (i.Food?.CaloriesPer100g ?? 0) * factor;
            var p = (i.Food?.ProteinPer100g ?? 0) * factor;
            var c = (i.Food?.CarbsPer100g ?? 0) * factor;
            var f = (i.Food?.FatPer100g ?? 0) * factor;
            totCal += cal; totP += p; totC += c; totF += f;
            ingredients.Add(new RecipeIngredientDto(
                i.Id, i.FoodId, i.Food?.Name ?? string.Empty, i.Grams,
                Math.Round(cal, 1), Math.Round(p, 1), Math.Round(c, 1), Math.Round(f, 1)));
        }
        var srv = Math.Max(1, r.Servings);
        return new RecipeDto(
            r.Id, r.Name, r.Description, r.Servings, ingredients,
            Math.Round(totCal, 1), Math.Round(totP, 1), Math.Round(totC, 1), Math.Round(totF, 1),
            Math.Round(totCal / srv, 1), Math.Round(totP / srv, 1),
            Math.Round(totC / srv, 1), Math.Round(totF / srv, 1));
    }
}

public interface IMealEntryService
{
    Task<DailyNutritionDto> GetDayAsync(DateOnly date, CancellationToken ct = default);
    Task<MealEntryDto> CreateAsync(UpsertMealEntryDto dto, CancellationToken ct = default);
    Task<bool> UpdateAsync(int id, UpsertMealEntryDto dto, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

public class MealEntryService : IMealEntryService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _current;

    public MealEntryService(IAppDbContext db, ICurrentUserService current)
    {
        _db = db;
        _current = current;
    }

    public async Task<DailyNutritionDto> GetDayAsync(DateOnly date, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var entries = await _db.MealEntries
            .AsNoTracking()
            .Include(m => m.Food)
            .Include(m => m.Recipe).ThenInclude(r => r!.Ingredients).ThenInclude(i => i.Food)
            .Where(m => m.UserId == userId && m.EatenOn == date)
            .OrderBy(m => m.MealType).ThenBy(m => m.Id)
            .ToListAsync(ct);

        decimal totCal = 0, totP = 0, totC = 0, totF = 0;
        var dtos = new List<MealEntryDto>();
        foreach (var e in entries)
        {
            var (cal, p, c, f) = ComputeMacros(e);
            totCal += cal; totP += p; totC += c; totF += f;
            dtos.Add(new MealEntryDto(
                e.Id, e.EatenOn, e.MealType,
                e.FoodId, e.Food?.Name,
                e.RecipeId, e.Recipe?.Name,
                e.Grams, e.Servings,
                Math.Round(cal, 1), Math.Round(p, 1), Math.Round(c, 1), Math.Round(f, 1),
                e.Notes));
        }

        return new DailyNutritionDto(date,
            Math.Round(totCal, 1), Math.Round(totP, 1), Math.Round(totC, 1), Math.Round(totF, 1),
            dtos);
    }

    public async Task<MealEntryDto> CreateAsync(UpsertMealEntryDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        Validate(dto);
        var e = new MealEntry
        {
            UserId = userId,
            EatenOn = dto.EatenOn,
            MealType = dto.MealType,
            FoodId = dto.FoodId,
            RecipeId = dto.RecipeId,
            Grams = dto.Grams,
            Servings = dto.Servings,
            Notes = dto.Notes
        };
        _db.MealEntries.Add(e);
        await _db.SaveChangesAsync(ct);
        return (await GetEntryDtoAsync(e.Id, ct))!;
    }

    public async Task<bool> UpdateAsync(int id, UpsertMealEntryDto dto, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        Validate(dto);
        var e = await _db.MealEntries.FindAsync(new object?[] { id }, ct);
        if (e is null) return false;
        if (e.UserId != userId) throw new ForbiddenException("You do not own this meal entry.");
        e.EatenOn = dto.EatenOn;
        e.MealType = dto.MealType;
        e.FoodId = dto.FoodId;
        e.RecipeId = dto.RecipeId;
        e.Grams = dto.Grams;
        e.Servings = dto.Servings;
        e.Notes = dto.Notes;
        e.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var userId = _current.RequireUserId();
        var e = await _db.MealEntries.FindAsync(new object?[] { id }, ct);
        if (e is null) return false;
        if (e.UserId != userId) throw new ForbiddenException("You do not own this meal entry.");
        _db.MealEntries.Remove(e);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<MealEntryDto?> GetEntryDtoAsync(int id, CancellationToken ct)
    {
        var e = await _db.MealEntries
            .AsNoTracking()
            .Include(m => m.Food)
            .Include(m => m.Recipe).ThenInclude(r => r!.Ingredients).ThenInclude(i => i.Food)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
        if (e is null) return null;
        var (cal, p, c, f) = ComputeMacros(e);
        return new MealEntryDto(
            e.Id, e.EatenOn, e.MealType,
            e.FoodId, e.Food?.Name,
            e.RecipeId, e.Recipe?.Name,
            e.Grams, e.Servings,
            Math.Round(cal, 1), Math.Round(p, 1), Math.Round(c, 1), Math.Round(f, 1),
            e.Notes);
    }

    private static (decimal cal, decimal p, decimal c, decimal f) ComputeMacros(MealEntry e)
    {
        if (e.FoodId.HasValue && e.Food is not null && e.Grams is decimal g)
        {
            var factor = g / 100m;
            return (e.Food.CaloriesPer100g * factor,
                    e.Food.ProteinPer100g * factor,
                    e.Food.CarbsPer100g * factor,
                    e.Food.FatPer100g * factor);
        }
        if (e.RecipeId.HasValue && e.Recipe is not null && e.Servings is decimal s)
        {
            decimal totCal = 0, totP = 0, totC = 0, totF = 0;
            foreach (var i in e.Recipe.Ingredients)
            {
                var factor = i.Grams / 100m;
                totCal += (i.Food?.CaloriesPer100g ?? 0) * factor;
                totP += (i.Food?.ProteinPer100g ?? 0) * factor;
                totC += (i.Food?.CarbsPer100g ?? 0) * factor;
                totF += (i.Food?.FatPer100g ?? 0) * factor;
            }
            var srv = Math.Max(1, e.Recipe.Servings);
            return (totCal / srv * s, totP / srv * s, totC / srv * s, totF / srv * s);
        }
        return (0, 0, 0, 0);
    }

    private static void Validate(UpsertMealEntryDto dto)
    {
        var hasFood = dto.FoodId.HasValue;
        var hasRecipe = dto.RecipeId.HasValue;
        if (hasFood == hasRecipe)
            throw new InvalidOperationException("Exactly one of FoodId or RecipeId must be set.");
        if (hasFood && (dto.Grams is null or <= 0))
            throw new InvalidOperationException("Grams must be > 0 for a food entry.");
        if (hasRecipe && (dto.Servings is null or <= 0))
            throw new InvalidOperationException("Servings must be > 0 for a recipe entry.");
    }
}
