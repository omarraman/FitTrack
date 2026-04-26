# AGENTS.md

## FitTrack at a glance
- Solution: `FitTrack.sln` with Clean Architecture projects: `FitTrack.Domain` -> `FitTrack.Application` -> `FitTrack.Infrastructure` -> `FitTrack.Web`.
- Runtime host is `FitTrack.Web/Program.cs`; it serves both Blazor Server UI and REST controllers in one process.
- UI components call application services directly (DI), while controllers expose the same services for API/Swagger access.
- Persistence is EF Core + PostgreSQL (`Npgsql`) through `FitTrack.Infrastructure/Persistence/AppDbContext.cs`.

## Request and data flow you should preserve
- Auth is global-by-default: `FallbackPolicy.RequireAuthenticatedUser` + controller `AuthorizeFilter` in `FitTrack.Web/Program.cs`.
- Current user resolution is in `FitTrack.Web/Auth/CurrentUserService.cs`: reads `oid`/`NameIdentifier`/`sub`, provisions user row, caches `UserId` + `IsAdmin` per request.
- Application services enforce data ownership and admin gates; controllers are thin wrappers (example: `FitTrack.Application/Workouts/ExerciseService.cs`).
- Shared-library writes (Exercises, Mesocycles, Foods, Recipes) require `IsAdmin`; per-user entities must filter by `UserId` and throw `ForbiddenException` on cross-user access.
- Domain entities inherit timestamps from `FitTrack.Domain/Common/Entity.cs`; `CreatedAt`/`UpdatedAt` are finalized centrally in `AppDbContext.SaveChangesAsync`.

## Project-specific rules (not generic)
- First authenticated user becomes admin (`UserProvisioningService.EnsureProvisionedAsync` in `FitTrack.Application/Users/UserProvisioning.cs`).
- Mesocycle progression is per-user and compounds only from that user’s last completed instance: `last.WeightMultiplier * 1.05`, rounded to 4 dp (`MesocycleInstanceService`).
- Planned workout weights are snapshotted at instance start and rounded to nearest 2.5 kg (`MesocycleInstanceService.RoundToNearest2p5`).
- Template updates often replace child collections wholesale (see mesocycle/recipe update logic) instead of diffing.
- Nutrition meal entries must set exactly one of `FoodId` or `RecipeId` (`MealEntryService.Validate`).

## Integration points and config
- PostgreSQL is local via `docker-compose.yml` (`fittrack/fittrack`, port `5432`) unless `ConnectionStrings:Postgres` is overridden.
- Entra ID OIDC settings live in `FitTrack.Web/appsettings.json` under `AzureAd` and must match launch URL/redirect URI.
- Migrations are in `FitTrack.Infrastructure/Persistence/Migrations`; startup auto-runs `Database.MigrateAsync()` via `DbSeeder.SeedAsync`.
- Seeder adds shared exercises, one sample mesocycle, and sample foods; it does not seed `AppUser` records.

## Developer workflows (PowerShell)
- Restore/build whole solution:
  - `dotnet restore .\FitTrack.sln`
  - `dotnet build .\FitTrack.sln`
- Start database:
  - `docker compose up -d`
- Run app with OIDC-friendly HTTPS profile from `launchSettings.json`:
  - `dotnet run --project .\FitTrack.Web --launch-profile https`
- Add EF migration after domain/model changes:
  - `dotnet ef migrations add <Name> --project .\FitTrack.Infrastructure --startup-project .\FitTrack.Web --output-dir Persistence/Migrations`

## When changing code
- Keep controllers minimal; put business/security logic in application services.
- In per-user queries, resolve user via `ICurrentUserService.RequireUserId()` first, then filter by `UserId` in DB query.
- For shared data mutation, use explicit `RequireAdmin()` checks (project pattern in workout/nutrition services).
- Prefer extending existing DTO/service files by feature area (`Workouts`, `Health`, `Nutrition`) over creating ad-hoc cross-layer shortcuts.
- If modifying auth display in UI, note current admin badge in `FitTrack.Web/Shared/MainLayout.razor` checks a `roles` claim, while authorization logic uses DB `IsAdmin`.

