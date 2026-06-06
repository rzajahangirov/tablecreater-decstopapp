namespace TableCreater.WPF.Models;

// ============================================================================
// AUTH DTOs / Records
// Mapped from Java LoginDto, RegisterDto, UserLoggedDto.
// JWT/RefreshToken DTOs are ELIMINATED — desktop app uses local session.
// ============================================================================

/// <summary>
/// Request model for user login.
/// Mapped from Java LoginDto.
/// </summary>
public record LoginRequest(string Email, string Password);

/// <summary>
/// Request model for user registration.
/// Mapped from Java RegisterDto.
/// </summary>
public record RegisterRequest(string Name, string Surname, string Email, string Password);

/// <summary>
/// Holds the current user's session information after login.
/// Mapped from Java UserLoggedDto. Replaces JWT-based auth in the desktop app.
/// </summary>
public record UserSessionInfo
{
    public long Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string RoleName { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}
