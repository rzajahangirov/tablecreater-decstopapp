using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TableCreater.WPF.Data;
using TableCreater.WPF.Data.Entities;
using TableCreater.WPF.Models;

namespace TableCreater.WPF.Services;

/// <summary>
/// Authentication service implementation.
/// Registered as Singleton to maintain session state across the entire application lifecycle.
///
/// Ports the Java AuthController + UserService logic (Section 2.1):
///   A1 → GetCurrentUser()   — reads from in-memory session (replaces JWT /api/auth/me)
///   A2 → Register()         — BCrypt hash + save (replaces /api/auth/register)
///   A3 → Login()            — BCrypt verify + set session (replaces /api/auth/login)
///   A4 → ELIMINATED         — no refresh tokens in desktop app
///
/// Design Note: Because AuthService is Singleton but AppDbContext is Scoped,
/// we inject IServiceScopeFactory and create short-lived scopes for each DB operation.
/// This is the standard .NET pattern for singletons consuming scoped services.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IServiceScopeFactory _scopeFactory;

    /// <summary>
    /// In-memory session state. Replaces the JWT token-based authentication
    /// from the legacy Spring Boot system. Set on successful Login, cleared on Logout.
    /// Thread-safe via volatile — only one user operates the desktop app at a time.
    /// </summary>
    private volatile UserSessionInfo? _currentSession;

    public AuthService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    // =========================================================================
    // A2 — REGISTER
    // Mapped from: UserService.register(RegisterDto)
    // Business flow: Hash password with BCrypt → Map DTO→Entity → Save to DB
    // =========================================================================

    /// <inheritdoc />
    public async Task Register(RegisterRequest request)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // ── Validate: email must be unique ──────────────────────────────
        bool emailExists = await db.Users
            .AnyAsync(u => u.Email.ToLower() == request.Email.ToLower());

        if (emailExists)
            throw new InvalidOperationException(
                $"A user with email '{request.Email}' already exists.");

        // ── Hash password with BCrypt ───────────────────────────────────
        // Replaces Java: BCryptPasswordEncoder.encode(password)
        string hashedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // ── Map DTO → Entity ────────────────────────────────────────────
        var user = new User
        {
            Name = request.Name,
            Surname = request.Surname,
            Email = request.Email,
            Password = hashedPassword,
            IsActive = true
        };

        // ── Assign default role ─────────────────────────────────────────
        // In the legacy system, newly registered users get a default role.
        // Ensure the "USER" role exists (create it if first run).
        var defaultRole = await db.Roles
            .FirstOrDefaultAsync(r => r.Name == "USER");

        if (defaultRole == null)
        {
            defaultRole = new Role { Name = "USER" };
            db.Roles.Add(defaultRole);
            await db.SaveChangesAsync();
        }

        user.Roles.Add(defaultRole);

        // ── Persist ─────────────────────────────────────────────────────
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }

    // =========================================================================
    // A3 — LOGIN
    // Mapped from: AuthController.authenticateAndGetToken()
    // Legacy flow: checks isActive → authenticates → creates refresh token → JWT
    // WPF flow:    checks isActive → verifies BCrypt hash → sets in-memory session
    // =========================================================================

    /// <inheritdoc />
    public async Task<UserSessionInfo?> Login(string email, string password)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // ── Find user by email (case-insensitive, matches Java behavior) ─
        var user = await db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());

        if (user == null)
            throw new InvalidOperationException("Invalid email or password.");

        // ── Check IsActive flag ─────────────────────────────────────────
        // Legacy: checks isActive before authentication
        if (!user.IsActive)
            throw new InvalidOperationException(
                "This account is deactivated. Please contact an administrator.");

        // ── Verify BCrypt password hash ─────────────────────────────────
        // Replaces Java: AuthenticationManager.authenticate(UsernamePasswordAuthToken)
        bool passwordValid = BCrypt.Net.BCrypt.Verify(password, user.Password);

        if (!passwordValid)
            throw new InvalidOperationException("Invalid email or password.");

        // ── Build session info ──────────────────────────────────────────
        // Replaces Java: UserLoggedDto populated from UserDetails + JWT generation
        // Maps roles to a comma-separated string (matches Java's role mapping)
        string roleName = string.Join(", ", user.Roles.Select(r => r.Name));

        var session = new UserSessionInfo
        {
            Id = user.Id,
            Email = user.Email,
            FirstName = user.Name,
            LastName = user.Surname,
            RoleName = roleName,
            IsActive = user.IsActive
        };

        // ── Set in-memory session (replaces JWT token storage) ──────────
        _currentSession = session;

        return session;
    }

    // =========================================================================
    // A1 — GET CURRENT USER
    // Mapped from: GET /api/auth/me → UserService.getLoggedUserInfo(email)
    // In WPF, simply returns the in-memory session state.
    // =========================================================================

    /// <inheritdoc />
    public UserSessionInfo? GetCurrentUser()
    {
        return _currentSession;
    }

    // =========================================================================
    // LOGOUT (Desktop addition — not in legacy API)
    // Clears the in-memory session. Useful for "Switch User" functionality.
    // =========================================================================

    /// <summary>
    /// Clears the current user session.
    /// Not in the original Java API but essential for desktop app UX
    /// (e.g., returning to login screen, switching users).
    /// </summary>
    public void Logout()
    {
        _currentSession = null;
    }

    // =========================================================================
    // SEED ADMIN (Desktop convenience — ensures first-run usability)
    // =========================================================================

    /// <summary>
    /// Seeds a default admin user if no users exist in the database.
    /// Called during application startup to ensure the app is usable on first launch.
    /// Default credentials: admin@tablecreater.com / admin123
    /// </summary>
    public async Task SeedDefaultAdminIfEmpty()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Only seed if the database has no users at all
        if (await db.Users.AnyAsync())
            return;

        // Ensure ADMIN role exists
        var adminRole = await db.Roles
            .FirstOrDefaultAsync(r => r.Name == "ADMIN");

        if (adminRole == null)
        {
            adminRole = new Role { Name = "ADMIN" };
            db.Roles.Add(adminRole);
        }

        // Ensure USER role exists (for future registrations)
        var userRole = await db.Roles
            .FirstOrDefaultAsync(r => r.Name == "USER");

        if (userRole == null)
        {
            userRole = new Role { Name = "USER" };
            db.Roles.Add(userRole);
        }

        await db.SaveChangesAsync();

        // Create default admin
        var admin = new User
        {
            Name = "Admin",
            Surname = "User",
            Email = "admin@tablecreater.com",
            Password = BCrypt.Net.BCrypt.HashPassword("admin123"),
            IsActive = true
        };

        admin.Roles.Add(adminRole);

        db.Users.Add(admin);
        await db.SaveChangesAsync();
    }
}
