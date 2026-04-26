using FitTrack.Application;
using FitTrack.Application.Abstractions;
using FitTrack.Infrastructure;
using FitTrack.Infrastructure.Persistence;
using FitTrack.Web.Auth;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var builder = WebApplication.CreateBuilder(args);

// Forwarded headers: required when running behind Cloudflare / reverse proxy
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;

    // In local homelab / tunnel setups, clear these so forwarded headers are accepted.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// --- Authentication: OpenID Connect against the tenant's Entra authority ---
builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization(options =>
{
    // Default policy: require an authenticated user for anything that doesn't opt out.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// MVC API + JSON enums as strings for nicer payloads. Require auth by default.
builder.Services.AddControllers(options =>
    {
        options.Filters.Add(new AuthorizeFilter());
    })
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    })
    .AddMicrosoftIdentityUI();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Razor Pages + Blazor Server (components call application services directly)
builder.Services.AddRazorPages()
    .AddMicrosoftIdentityUI();
builder.Services.AddServerSideBlazor();

// HttpContext access for Blazor Server + our current-user resolver
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// App layers
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// IMPORTANT: this must run early so auth sees the original https scheme/host
app.UseForwardedHeaders();

// Migrate + seed on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbSeeder.SeedAsync(db);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Optional but usually correct when public traffic is HTTPS at Cloudflare
app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapRazorPages(); // required for /MicrosoftIdentity/Account/SignIn and /SignOut endpoints
app.MapBlazorHub().AllowAnonymous();
app.MapFallbackToPage("/_Host");

app.Run();