using FitTrack.Application.Health;
using FitTrack.Application.Nutrition;
using FitTrack.Application.Users;
using FitTrack.Application.Workouts;
using Microsoft.Extensions.DependencyInjection;

namespace FitTrack.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IUserProvisioningService, UserProvisioningService>();

        services.AddScoped<IExerciseService, ExerciseService>();
        services.AddScoped<IMesocycleService, MesocycleService>();
        services.AddScoped<IMesocycleInstanceService, MesocycleInstanceService>();
        services.AddScoped<IWorkoutSessionService, WorkoutSessionService>();

        services.AddScoped<IBodyMeasurementService, BodyMeasurementService>();
        services.AddScoped<IBodyPartMeasurementService, BodyPartMeasurementService>();
        services.AddScoped<IBloodPressureService, BloodPressureService>();
        services.AddScoped<IColdEpisodeService, ColdEpisodeService>();
        services.AddScoped<ICardioSessionService, CardioSessionService>();

        services.AddScoped<IFoodService, FoodService>();
        services.AddScoped<IRecipeService, RecipeService>();
        services.AddScoped<IMealEntryService, MealEntryService>();

        return services;
    }
}
