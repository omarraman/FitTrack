using FitTrack.Domain.Nutrition;

namespace FitTrack.Application.Nutrition;

public record FoodDto(
    int Id, string Name, string? Brand,
    decimal CaloriesPer100g, decimal ProteinPer100g, decimal CarbsPer100g, decimal FatPer100g, decimal? FiberPer100g);

public record UpsertFoodDto(
    string Name, string? Brand,
    decimal CaloriesPer100g, decimal ProteinPer100g, decimal CarbsPer100g, decimal FatPer100g, decimal? FiberPer100g);

public record RecipeIngredientDto(
    int Id, int FoodId, string FoodName, decimal Grams,
    decimal Calories, decimal Protein, decimal Carbs, decimal Fat);

public record UpsertRecipeIngredientDto(int FoodId, decimal Grams);

public record RecipeDto(
    int Id, string Name, string? Description, int Servings,
    List<RecipeIngredientDto> Ingredients,
    decimal TotalCalories, decimal TotalProtein, decimal TotalCarbs, decimal TotalFat,
    decimal PerServingCalories, decimal PerServingProtein, decimal PerServingCarbs, decimal PerServingFat);

public record UpsertRecipeDto(
    string Name, string? Description, int Servings,
    List<UpsertRecipeIngredientDto> Ingredients);

public record MealEntryDto(
    int Id, DateOnly EatenOn, MealType MealType,
    int? FoodId, string? FoodName,
    int? RecipeId, string? RecipeName,
    decimal? Grams, decimal? Servings,
    decimal Calories, decimal Protein, decimal Carbs, decimal Fat,
    string? Notes);

public record UpsertMealEntryDto(
    DateOnly EatenOn, MealType MealType,
    int? FoodId, int? RecipeId,
    decimal? Grams, decimal? Servings,
    string? Notes);

public record DailyNutritionDto(
    DateOnly Date,
    decimal TotalCalories, decimal TotalProtein, decimal TotalCarbs, decimal TotalFat,
    List<MealEntryDto> Entries);
