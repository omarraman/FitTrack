using System.Security.Claims;
using FitTrack.Application.Abstractions;
using FitTrack.Application.Users;

namespace FitTrack.Web.Auth;

/// <summary>
/// HttpContext-backed implementation of <see cref="ICurrentUserService"/>.
/// On first access per request, it:
///   1. Reads the user's stable external identifier (Entra oid / sub / NameIdentifier)
///   2. Upserts a corresponding <c>AppUser</c> row (first user wins admin)
///   3. Caches the resulting id/flags for the rest of the request.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private const string EntraObjectIdClaim = "http://schemas.microsoft.com/identity/claims/objectidentifier";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IUserProvisioningService _provisioning;
    private readonly ILogger<CurrentUserService> _logger;

    private bool _resolved;
    private int? _userId;
    private bool _isAdmin;

    public CurrentUserService(
        IHttpContextAccessor httpContextAccessor,
        IUserProvisioningService provisioning,
        ILogger<CurrentUserService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _provisioning = provisioning;
        _logger = logger;
    }

    public int? UserId
    {
        get
        {
            EnsureResolved();
            return _userId;
        }
    }

    public bool IsAdmin
    {
        get
        {
            EnsureResolved();
            return _isAdmin;
        }
    }

    public int RequireUserId()
    {
        EnsureResolved();
        return _userId ?? throw new NotAuthenticatedException();
    }

    private void EnsureResolved()
    {
        if (_resolved) return;
        _resolved = true;

        var principal = _httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true) return;

        var externalId = principal.FindFirst("oid")?.Value
                      ?? principal.FindFirst(EntraObjectIdClaim)?.Value
                      ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? principal.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(externalId))
        {
            var claimTypes = string.Join(", ", principal.Claims.Select(c => c.Type).Distinct().OrderBy(x => x));
            _logger.LogWarning(
                "Authenticated principal has no oid/objectidentifier/sub/NameIdentifier claim; treating as anonymous. Claims: {ClaimTypes}",
                claimTypes);
            return;
        }

        var email = principal.FindFirst("preferred_username")?.Value
                 ?? principal.FindFirst(ClaimTypes.Email)?.Value
                 ?? principal.FindFirst("email")?.Value
                 ?? string.Empty;
        var name = principal.FindFirst("name")?.Value
                ?? principal.FindFirst(ClaimTypes.Name)?.Value
                ?? email;

        // Task.Run moves the async work onto a thread-pool thread (no sync context),
        // preventing the Blazor Server RendererSynchronizationContext from deadlocking
        // when .GetAwaiter().GetResult() blocks on an awaited EF Core operation.
        var user = Task.Run(() => _provisioning.EnsureProvisionedAsync(externalId, email, name)).GetAwaiter().GetResult();

        _userId = user.Id;
        _isAdmin = user.IsAdmin;
    }
}
