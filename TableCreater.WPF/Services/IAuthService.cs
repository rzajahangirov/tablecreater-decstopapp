using TableCreater.WPF.Models;

namespace TableCreater.WPF.Services;

/// <summary>
/// Authentication service interface.
/// Replaces JWT-based Spring Security auth with local BCrypt password verification.
/// Registered as Singleton to maintain session state across the application lifecycle.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Authenticates a user by email/password. Verifies BCrypt hash and IsActive flag.
    /// Sets the in-memory session on success.
    /// </summary>
    Task<UserSessionInfo?> Login(string email, string password);

    /// <summary>
    /// Registers a new user. Hashes password with BCrypt before persisting.
    /// Assigns the default "USER" role.
    /// </summary>
    Task Register(RegisterRequest request);

    /// <summary>
    /// Returns the currently authenticated user's session info, or null if not logged in.
    /// </summary>
    UserSessionInfo? GetCurrentUser();

    /// <summary>
    /// Clears the current session. Used for "Switch User" or returning to login.
    /// </summary>
    void Logout();

    /// <summary>
    /// Seeds a default admin user if the database has no users (first-run convenience).
    /// </summary>
    Task SeedDefaultAdminIfEmpty();
}
