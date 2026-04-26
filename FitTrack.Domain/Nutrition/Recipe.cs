using FitTrack.Domain.Common;

namespace FitTrack.Domain.Nutrition;

/// <summary>
/// A recipe composed of foods. Macros are computed by summing ingredient
/// macros scaled to the ingredient's grams. Serves yields per-serving macros.
/// </summary>
public class Recipe : Entity
{
    // Recipes are shared library data — no UserId.
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Servings { get; set; } = 1;

    public List<RecipeIngredient> Ingredients { get; set; } = new();
}

public class RecipeIngredient : Entity
{
    public int RecipeId { get; set; }
    public Recipe? Recipe { get; set; }

    public int FoodId { get; set; }
    public Food? Food { get; set; }

    public decimal Grams { get; set; }
}

public enum MealType
{
    Breakfast = 0,
    Lunch = 1,
    Dinner = 2,
    Snack = 3,
    Other = 4
}

/// <summary>
/// A single consumed food or recipe on a given date.
/// Exactly one of FoodId / RecipeId is set; Grams is used for Food, Servings for Recipe.
/// </summary>
public class MealEntry : Entity
{
    public int UserId { get; set; }
    public DateOnly EatenOn { get; set; }
    public MealType MealType { get; set; } = MealType.Other;

    public int? FoodId { get; set; }
    public Food? Food { get; set; }

    public int? RecipeId { get; set; }
    public Recipe? Recipe { get; set; }

    public decimal? Grams { get; set; }
    public decimal? Servings { get; set; }

    public string? Notes { get; set; }
}
