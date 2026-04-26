using FitTrack.Domain.Common;

namespace FitTrack.Domain.Users;

/// <summary>
/// A user of the app, provisioned from Entra ID claims on first login.
/// ExternalId is the Entra objectId (stable, per-tenant, comes from the "oid" or "sub" claim).
/// </summary>
public class AppUser : Entity
{
    /// <summary>Stable external identifier from the identity provider (Entra oid / sub).</summary>
    public string ExternalId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Admin users can edit shared library data (exercises, mesocycles, foods, recipes).</summary>
    public bool IsAdmin { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }
}
