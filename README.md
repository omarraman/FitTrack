# FitTrack

Personal workout + health tracker built with **ASP.NET Core 8**, **Blazor Server**, **API controllers**, **EF Core** and **PostgreSQL**, following **Clean Architecture**. Multi-user via **Microsoft Entra ID** (OpenID Connect).

## Features

### Workouts
- **Exercises** *(shared library, admin-only writes)* — CRUD a library of movements (bench, squat, etc.) with muscle group.
- **Mesocycles** *(shared library, admin-only writes)* — templates made of one or more workout days, each with planned exercises (sets × reps @ kg), that last N weeks.
- **Instances** *(per user)* — start a mesocycle on a date. Sessions are generated for every week × day. Each user has their own set of instances.
- **Today's workout** — home screen shows the current user's active session with per-set actual reps / weight entry.
- **5% progression (per user)** — when a user starts a new instance of a mesocycle and has a previous instance of it marked `Completed`, the new instance gets a `WeightMultiplier` of `previous × 1.05` (compounds across that user's own completions), and each planned target weight is recalculated as `template_target × multiplier` rounded to the nearest 2.5 kg. Progression timelines are independent per user.

### Health *(per user)*
- **Bodyweight** log with optional body fat %, muscle % and muscle kg. Includes a simple SVG trend line.
- **Blood pressure** log with systolic / diastolic / pulse and AHA category badges.
- **Cold tracker** — episodes with severity (1–5), symptoms, dates; yearly stats.

### Nutrition
- **Foods** *(shared library, admin-only writes)* — per-100g macros (kcal, protein, carbs, fat, fiber).
- **Recipes** *(shared library, admin-only writes)* — composed of foods × grams, showing total and per-serving macros.
- **Food diary** *(per user)* — log meals by food (grams) or recipe (servings); daily kcal + macro rollup.

## Authentication model

FitTrack uses **OpenID Connect** with **Microsoft.Identity.Web** against *your* Entra ID tenant. The auth/authorization model is:

- All pages and API endpoints require an authenticated user (fallback `RequireAuthenticatedUser` policy).
- On first sign-in, an `AppUser` row is auto-provisioned from the token's `oid` / `sub` claim (`ExternalId`), plus email (`preferred_username`) and display name (`name`).
- **The very first user to sign in is automatically promoted to admin** (`IsAdmin = true`). Subsequent users are regular users.
- **Admins** can create / edit / delete the shared library (Exercises, Mesocycle templates, Foods, Recipes). Regular users can read those but not modify them.
- **All users** own their own per-user data (instances, workout sessions, exercise logs, bodyweight, blood pressure, colds, meal entries) and cannot see or touch other users' rows.

There is no role claim needed — admin-ness lives as a DB flag on `AppUser`. If you want to promote another user to admin later, just `UPDATE "AppUsers" SET "IsAdmin" = true WHERE "Email" = '…'`.

## Architecture

```
FitTrack.sln
├── FitTrack.Domain          # Entities, enums, domain rules (no dependencies)
├── FitTrack.Application     # DTOs, service interfaces, use-cases, IAppDbContext, ICurrentUserService
├── FitTrack.Infrastructure  # EF Core DbContext, Npgsql, migrations, seeding
└── FitTrack.Web             # ASP.NET Core host: OIDC auth, API controllers + Blazor Server UI, CurrentUserService impl
```

Dependency direction: **Web → Infrastructure → Application → Domain**. Blazor components inject the `Application` services directly (no HTTP round-trip). API controllers are provided for external access. User scoping is enforced inside application services via `ICurrentUserService` so the same DbContext can still serve admin operations on shared data.

## Running locally

### Prereqs
- .NET 8 SDK
- Docker (or a local Postgres instance)
- An Entra ID (Azure AD) tenant where you can create an app registration

### 1. Register an app in Entra ID

1. Go to **Entra ID admin center → Applications → App registrations → New registration**.
2. Name it e.g. `FitTrack-local`.
3. **Supported account types**: *Accounts in this organizational directory only* (single tenant) — unless you want multi-tenant.
4. **Redirect URI**: `Web` → `https://localhost:5001/signin-oidc` (adjust port to whatever your app listens on).
5. After it's created, open **Authentication** and also add these URIs:
   - `https://localhost:5001/signout-callback-oidc`
   - Under **Front-channel logout URL**: `https://localhost:5001/signout-oidc` *(optional)*
   - Tick **ID tokens** under *Implicit grant and hybrid flows*.
6. Open **Token configuration → Add optional claim** and add `email` and `preferred_username` to the **ID** token (for nicer user info). Also add `upn` if you like.
7. Copy the **Application (client) ID** and **Directory (tenant) ID** from the Overview page.

### 2. Configure the app

Edit `FitTrack.Web/appsettings.json` and fill in the `AzureAd` section:

```json
"AzureAd": {
  "Instance": "https://login.microsoftonline.com/",
  "Domain": "yourtenant.onmicrosoft.com",
  "TenantId": "<your-tenant-id-guid>",
  "ClientId": "<your-app-registration-client-id>",
  "CallbackPath": "/signin-oidc",
  "SignedOutCallbackPath": "/signout-callback-oidc"
}
```

### 3. Start PostgreSQL

```bash
docker compose up -d
```

This starts Postgres on `localhost:5432` with user/password/db all set to `fittrack`.

If you prefer a local Postgres, create the db manually and edit `FitTrack.Web/appsettings.json → ConnectionStrings:Postgres`.

### 4. Run the app

```bash
dotnet run --project FitTrack.Web
```

On first run the app applies migrations and seeds a shared library (exercises, a sample mesocycle "Upper / Lower 4-Week", and a handful of foods). **No `AppUser` rows are seeded** — they are created on first sign-in.

Override the listening URL with `ASPNETCORE_URLS`, but make sure it matches the redirect URI you registered in Entra ID. **OIDC requires HTTPS in production; for local testing use the `https://localhost:5001` profile** from `launchSettings.json`, which has a dev cert by default.

```bash
dotnet run --project FitTrack.Web --launch-profile https
```

Then open:
- **App**: https://localhost:5001/ — you'll be redirected to Microsoft sign-in
- **Swagger (API docs)**: https://localhost:5001/swagger *(also requires sign-in)*

### 5. Trying it out

1. Sign in with your Entra ID account. You're now user #1 and were auto-promoted to admin (sidebar shows the `admin` badge).
2. Go to **Mesocycles** → the seeded "Upper / Lower 4-Week" is already there.
3. Go to **Instances** → select a mesocycle and click **Start instance** (this instance is yours).
4. Go to **Today** → log actual reps/weight per set and mark the session complete.
5. When the instance is done, go to **Instances** → click **Complete**.
6. Start a new instance of the same mesocycle → the multiplier steps to `1.0500` and target weights bump ~5%.
7. Sign out, sign in as a **different** Entra user → they start from scratch (their own instances, their own progression), can read shared exercises/recipes but can't edit them.

## API

All API endpoints are under `/api/...` and visible in Swagger. Every endpoint requires a signed-in user. Endpoints that write to shared library entities require admin.

| Method | Route                                     | Purpose                                  |
|--------|-------------------------------------------|------------------------------------------|
| GET    | `/api/workout-sessions/today`             | Today's active session (for current user)|
| PUT    | `/api/workout-sessions/logs/{logId}`      | Log actual reps/weight for a set         |
| POST   | `/api/workout-sessions/{id}/complete`     | Mark session complete                    |
| POST   | `/api/mesocycle-instances/start`          | Start a new instance (applies +5% rule)  |
| POST   | `/api/mesocycle-instances/{id}/complete`  | Mark instance completed                  |
| GET    | `/api/meals/day/{yyyy-MM-dd}`             | Day rollup with per-meal macros          |

## Migrations

Migrations live in `FitTrack.Infrastructure/Persistence/Migrations` and are applied automatically on startup.

To add a new migration after changing the domain model:

```bash
dotnet ef migrations add YourMigrationName \
  --project FitTrack.Infrastructure \
  --startup-project FitTrack.Web \
  --output-dir Persistence/Migrations
```

**Upgrading from a pre-auth version of FitTrack?** The auth retrofit changes primary keys / foreign keys on several tables. Simplest path for a personal dev DB is to drop + recreate:

```bash
docker compose down -v    # wipes the Postgres volume
docker compose up -d
dotnet run --project FitTrack.Web
```

## Notes

- Progression rounds to the nearest 2.5 kg, the standard smallest increment for typical gym plates. Tweak in `MesocycleInstanceService.RoundToNearest2p5` if you use 1 kg fractionals.
- Sessions are scheduled one workout per day in `DayOrder` sequence, then the next week continues after the last day of the previous week. If you want rest-day spacing, extend `MesocycleInstanceService.StartAsync`.
- The "first-user-is-admin" bootstrap runs inside `UserProvisioningService.EnsureProvisionedAsync`. For a more robust setup (e.g., multiple bootstrap admins by email), extend that service or manage the `IsAdmin` flag in the database.
- User identity is keyed on the `oid` claim (preferred) with fallback to `sub` / `NameIdentifier`. This is stable across renames and email changes within a tenant.
