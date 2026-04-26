using FitTrack.Application.Abstractions;
using FitTrack.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace FitTrack.Application.Users;

public record UserDto(int Id, string ExternalId, string Email, string DisplayName, bool IsAdmin);

public interface IUserProvisioningService
{
    /// <summary>
    /// Upserts a user by ExternalId (Entra oid). The very first user ever provisioned
    /// is automatically promoted to admin. Returns the DB user.
    /// </summary>
    Task<UserDto> EnsureProvisionedAsync(
        string externalId, string email, string displayName, CancellationToken ct = default);
}

public class UserProvisioningService : IUserProvisioningService
{
    private readonly IAppDbContext _db;

    public UserProvisioningService(IAppDbContext db) => _db = db;

    public async Task<UserDto> EnsureProvisionedAsync(
        string externalId, string email, string displayName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            throw new ArgumentException("externalId is required", nameof(externalId));

        var user = await _db.AppUsers.FirstOrDefaultAsync(u => u.ExternalId == externalId, ct);

        if (user is null)
        {
            var isFirstUser = !await _db.AppUsers.AnyAsync(ct);
            user = new AppUser
            {
                ExternalId = externalId,
                Email = email ?? string.Empty,
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? email ?? externalId : displayName,
                IsAdmin = isFirstUser,
                LastLoginAt = DateTimeOffset.UtcNow
            };
            _db.AppUsers.Add(user);
        }
        else
        {
            // Keep profile fields in sync with the IdP on each login.
            if (!string.IsNullOrWhiteSpace(email) && user.Email != email) user.Email = email;
            if (!string.IsNullOrWhiteSpace(displayName) && user.DisplayName != displayName) user.DisplayName = displayName;
            user.LastLoginAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return new UserDto(user.Id, user.ExternalId, user.Email, user.DisplayName, user.IsAdmin);
    }
}
