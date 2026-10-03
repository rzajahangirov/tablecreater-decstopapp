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
using TableCreater.WPF.Views.Windows;

namespace TableCreater.WPF;

/// <summary>
/// Application entry point with Dependency Injection container setup.
/// Configures EF Core (SQLite/SQLCipher), Services, and ViewModels.
/// Shows PIN-based LoginWindow before MainWindow.
/// </summary>
public partial class App : Application
{
    public const string Version = "1.0.0";
    public const string AppName = "TableCreater Accounting";

    /// <summary>
    /// Global access to the DI service provider.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Singleton SecurityService, initialized before DI container.
    /// </summary>
    public static ISecurityService Security { get; private set; } = null!;

    private static string DbFilePath => Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "tablecreater.db");

    public App()
    {
        // Initialize SQLCipher provider EXPLICITLY before anything else.
        // We must use the e_sqlcipher provider (not the default e_sqlite3)
        // to support encrypted databases.
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlcipher());

        // Create security service (reads/creates security.json)
        Security = new SecurityService();

        // ─── Global Exception Handlers ───────────────────────────────
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ─── Step 1: Show Login Window ───────────────────────────────
        var loginWindow = new LoginWindow(Security);
        bool? loginResult = loginWindow.ShowDialog();

        if (loginResult != true || string.IsNullOrEmpty(Security.ActiveDek))
        {
            Shutdown();
            return;
        }

        // ─── Step 2: Encrypt DB if still plain ──────────────────────
        // This is non-fatal: if encryption migration fails, the app
        // continues with the unencrypted DB. The method has its own
        // internal error handling and logging.
        Security.EnsureDatabaseEncrypted(DbFilePath);

        // ─── Step 3: Build DI container with encrypted connection ────
        Services = ConfigureServices();

        try
        {
            // Ensure the database is created with the latest schema
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();

            // Migrate schema if tables were created with an older structure
            MigrateDatabaseSchema(db);

            // Seed default admin user on first run
            var authService = Services.GetRequiredService<IAuthService>();
            await authService.SeedDefaultAdminIfEmpty();

            // Auto-login the default admin
            await authService.Login("admin@tablecreater.com", "admin123");
        }
        catch (Exception ex)
        {
            ShowFatalError("Başlanğıc Xətası",
                $"Tətbiqi işə salmaq mümkün olmadı:\n\n{ex.Message}");
            return;
        }

        // ─── Step 4: Show Main Window ───────────────────────────────
        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // ─── Database ────────────────────────────────────────────────
        var connStr = Security.GetConnectionString(DbFilePath);
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connStr));

        // ─── Services ────────────────────────────────────────────────
        services.AddSingleton<ISecurityService>(Security);
        services.AddSingleton<IAuthService, AuthService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IExcelService, ExcelService>();
        services.AddSingleton<IBackupService, BackupService>();

        // ─── ViewModels ──────────────────────────────────────────────
        services.AddTransient<MainViewModel>();
        services.AddTransient<CustomerListViewModel>();
        services.AddTransient<TransactionEntryViewModel>();
        services.AddTransient<ReportsViewModel>();
        services.AddTransient<CustomerDetailsViewModel>();
        services.AddTransient<TransactionDetailViewModel>();
        services.AddTransient<SettingsViewModel>();

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
        ShowErrorDialog("Gözlənilməz Xəta", e.Exception.Message);
    }

    private void OnDomainUnhandledException(object sender,
        UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            ShowFatalError("Kritik Xəta", ex.Message);
        }
    }

    private void OnUnobservedTaskException(object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        Dispatcher.Invoke(() =>
            ShowErrorDialog("Arxa Plan Xətası",
                e.Exception?.InnerException?.Message ?? "Xəta baş verdi."));
    }

    /// <summary>
    /// Shows a non-fatal error dialog. App continues running.
    /// </summary>
    private static void ShowErrorDialog(string title, string message)
    {
        MessageBox.Show(
            $"{message}\n\nZəhmət olmasa yenidən cəhd edin və ya proqramı yenidən başladın.",
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
            $"{message}\n\nProqram bağlanacaq.",
            $"{AppName} — {title}",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        Environment.Exit(1);
    }

    /// <summary>
    /// Safely adds new columns to SQLite database if upgrading from an earlier version.
    /// </summary>
    private static void MigrateDatabaseSchema(AppDbContext db)
    {
        string[] migrationQueries =
        [
            "ALTER TABLE Transactions ADD COLUMN SendingCompany TEXT;",
            "ALTER TABLE Transactions ADD COLUMN TransportCurrency TEXT NOT NULL DEFAULT 'Usd';",
            "ALTER TABLE Transactions ADD COLUMN AdditionalExpenseAmount REAL;",
            "ALTER TABLE Transactions ADD COLUMN AdditionalExpenseDescription TEXT;",
            "ALTER TABLE Transactions ADD COLUMN ShipmentStatus TEXT NOT NULL DEFAULT 'Pending';",
            "ALTER TABLE Transactions ADD COLUMN LoadedDate TEXT;",
            "ALTER TABLE Transactions ADD COLUMN InTransitStartDate TEXT;",
            "ALTER TABLE Transactions ADD COLUMN InTransitEndDate TEXT;",
            "ALTER TABLE Transactions ADD COLUMN DeliveredDate TEXT;",
            "ALTER TABLE Transactions ADD COLUMN IsInTransitAutoDates INTEGER NOT NULL DEFAULT 1;",
            "ALTER TABLE Customers ADD COLUMN BalanceUsd REAL NOT NULL DEFAULT 0;",
            "ALTER TABLE Transactions ADD COLUMN PaymentStatus TEXT NOT NULL DEFAULT 'Paid';",
            "ALTER TABLE Transactions ADD COLUMN ProfitPerTon REAL NOT NULL DEFAULT 0;",
            "ALTER TABLE Transactions ADD COLUMN ProfitPerTonCurrency TEXT NOT NULL DEFAULT 'Usd';",
            "ALTER TABLE Transactions ADD COLUMN HistoricalUserProfitUsd REAL NOT NULL DEFAULT 0;",
            "ALTER TABLE Transactions ADD COLUMN HistoricalCustomerBilledUsd REAL NOT NULL DEFAULT 0;",
            "ALTER TABLE Transactions ADD COLUMN HistoricalBalanceDeltaUsd REAL NOT NULL DEFAULT 0;",
            @"CREATE TABLE IF NOT EXISTS CustomerBalanceHistories (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CustomerId INTEGER NOT NULL,
                TransactionId INTEGER,
                CreatedAt TEXT NOT NULL,
                Type TEXT NOT NULL,
                AmountUsd REAL NOT NULL,
                BalanceAfterUsd REAL NOT NULL,
                Description TEXT,
                FOREIGN KEY (CustomerId) REFERENCES Customers(Id) ON DELETE CASCADE,
                FOREIGN KEY (TransactionId) REFERENCES Transactions(Id) ON DELETE SET NULL
            );"
        ];

        foreach (var sql in migrationQueries)
        {
            try
            {
                db.Database.ExecuteSqlRaw(sql);
            }
            catch
            {
                // Column already exists or schema is already up to date
            }
        }
    }
}
