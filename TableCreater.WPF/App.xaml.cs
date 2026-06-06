using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TableCreater.WPF.Data;
using TableCreater.WPF.Services;
using TableCreater.WPF.ViewModels;
using TableCreater.WPF.ViewModels.Customers;
using TableCreater.WPF.ViewModels.Transactions;

namespace TableCreater.WPF;

/// <summary>
/// Application entry point with Dependency Injection container setup.
/// Configures EF Core (SQLite), Services, and ViewModels per Section 8.2 of the spec.
/// Includes global exception handling for production readiness.
/// </summary>
public partial class App : Application
{
    public const string Version = "1.0.0";
    public const string AppName = "TableCreater Accounting";

    /// <summary>
    /// Global access to the DI service provider.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        Services = ConfigureServices();

        // ─── Global Exception Handlers ───────────────────────────────
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // Ensure the SQLite database directory exists
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TableCreater");
            if (!Directory.Exists(dbPath))
                Directory.CreateDirectory(dbPath);

            // Ensure the SQLite database is created with the latest schema
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();

            // Seed default admin user on first run
            var authService = Services.GetRequiredService<IAuthService>();
            await authService.SeedDefaultAdminIfEmpty();

            // Auto-login the default admin for development
            await authService.Login("admin@tablecreater.com", "admin123");
        }
        catch (Exception ex)
        {
            ShowFatalError("Startup Error",
                $"Failed to initialize the application:\n\n{ex.Message}");
        }
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // ─── Database ────────────────────────────────────────────────
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite("Data Source=tablecreater.db"));

        // ─── Services ────────────────────────────────────────────────
        services.AddSingleton<IAuthService, AuthService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IExcelService, ExcelService>();

        // ─── ViewModels ──────────────────────────────────────────────
        services.AddTransient<MainViewModel>();
        services.AddTransient<CustomerListViewModel>();
        services.AddTransient<TransactionEntryViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<CustomerDetailsViewModel>();

        return services.BuildServiceProvider();
    }

    // =========================================================================
    // GLOBAL EXCEPTION HANDLERS
    // Catches unhandled exceptions and shows a user-friendly dialog
    // instead of crashing the application.
    // =========================================================================

    private void OnDispatcherUnhandledException(object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        ShowErrorDialog("Unexpected Error", e.Exception.Message);
    }

    private void OnDomainUnhandledException(object sender,
        UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            ShowFatalError("Critical Error", ex.Message);
        }
    }

    private void OnUnobservedTaskException(object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        Dispatcher.Invoke(() =>
            ShowErrorDialog("Background Error",
                e.Exception?.InnerException?.Message ?? "An error occurred."));
    }

    /// <summary>
    /// Shows a non-fatal error dialog. App continues running.
    /// </summary>
    private static void ShowErrorDialog(string title, string message)
    {
        MessageBox.Show(
            $"{message}\n\nPlease try again or restart the application.",
            $"{AppName} — {title}",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    /// <summary>
    /// Shows a fatal error dialog and shuts down.
    /// </summary>
    private static void ShowFatalError(string title, string message)
    {
        MessageBox.Show(
            $"{message}\n\nThe application will now close.",
            $"{AppName} — {title}",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        Environment.Exit(1);
    }
}
