namespace FitTrack.Application.Abstractions;

/// <summary>
/// Resolves the currently signed-in app user. Implemented in the Web layer
/// using HttpContext claims + the database.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>Database primary key of the current user, or null if unauthenticated / not yet provisioned.</summary>
    int? UserId { get; }

    /// <summary>True if the current user has the admin flag set.</summary>
    bool IsAdmin { get; }

    /// <summary>Throws InvalidOperationException if no user is signed in.</summary>
    int RequireUserId();
}

public class NotAuthenticatedException : InvalidOperationException
{
    public NotAuthenticatedException() : base("No authenticated user in the current request.") { }
}

public class ForbiddenException : InvalidOperationException
{
    public ForbiddenException(string message) : base(message) { }
}
