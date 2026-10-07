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

    private static readonly string CrashLogPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "crash.log");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            File.WriteAllText(CrashLogPath, $"[{DateTime.Now}] App starting...\n");
        }
        catch { }

        // Prevent application from shutting down when LoginWindow closes
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // ─── Step 1: Show Login Window ───────────────────────────────
        var loginWindow = new LoginWindow(Security);
        bool? loginResult = loginWindow.ShowDialog();

        if (loginResult != true || string.IsNullOrEmpty(Security.ActiveDek))
        {
            Shutdown();
            return;
        }

        try { File.AppendAllText(CrashLogPath, $"[{DateTime.Now}] Login OK. DEK length={Security.ActiveDek?.Length}\n"); } catch { }

        // ─── Step 2: Encrypt DB if still plain ──────────────────────
        Security.EnsureDatabaseEncrypted(DbFilePath);

        try { File.AppendAllText(CrashLogPath, $"[{DateTime.Now}] EnsureDatabaseEncrypted done. DB path={DbFilePath}, exists={File.Exists(DbFilePath)}\n"); } catch { }

        // ─── Step 3: Build DI container with encrypted connection ────
        var connStr = Security.GetConnectionString(DbFilePath);
        try { File.AppendAllText(CrashLogPath, $"[{DateTime.Now}] Connection string: {connStr}\n"); } catch { }

        Services = ConfigureServices();

        try
        {
            // Ensure the database is created with the latest schema
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            try { File.AppendAllText(CrashLogPath, $"[{DateTime.Now}] Calling EnsureCreated...\n"); } catch { }
            db.Database.EnsureCreated();

            try { File.AppendAllText(CrashLogPath, $"[{DateTime.Now}] EnsureCreated OK. Running migrations...\n"); } catch { }

            // Migrate schema if tables were created with an older structure
            MigrateDatabaseSchema(db);

            try { File.AppendAllText(CrashLogPath, $"[{DateTime.Now}] Migrations OK. Seeding admin...\n"); } catch { }

            // Seed default admin user on first run
            var authService = Services.GetRequiredService<IAuthService>();
            await authService.SeedDefaultAdminIfEmpty();

            // Auto-login the default admin
            await authService.Login("admin@tablecreater.com", "admin123");

            try { File.AppendAllText(CrashLogPath, $"[{DateTime.Now}] All startup steps OK!\n"); } catch { }
        }
        catch (Exception ex)
        {
            // Write full error to crash log file
            try
            {
                var fullError = $"[{DateTime.Now}] FATAL ERROR:\n{ex}\n";
                if (ex.InnerException != null)
                    fullError += $"\nInner: {ex.InnerException}\n";
                File.AppendAllText(CrashLogPath, fullError);
            }
            catch { }

            ShowFatalError("Başlanğıc Xətası",
                $"Tətbiqi işə salmaq mümkün olmadı:\n\n{ex.Message}");
            return;
        }

        // ─── Step 4: Show Main Window ───────────────────────────────
        try
        {
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
            try { File.AppendAllText(CrashLogPath, $"[{DateTime.Now}] MainWindow shown successfully!\n"); } catch { }
        }
        catch (Exception ex)
        {
            try { File.AppendAllText(CrashLogPath, $"[{DateTime.Now}] MainWindow display error:\n{ex}\n"); } catch { }
            ShowFatalError("Pəncərə Xətası", ex.Message);
        }
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
        services.AddTransient<DashboardViewModel>();
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
        try { File.AppendAllText(CrashLogPath, $"[{DateTime.Now}] DISPATCHER ERROR:\n{e.Exception}\n\n"); } catch { }
        ShowErrorDialog("Gözlənilməz Xəta", e.Exception.Message);
    }

    private void OnDomainUnhandledException(object sender,
        UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            try { File.AppendAllText(CrashLogPath, $"[{DateTime.Now}] DOMAIN ERROR:\n{ex}\n\n"); } catch { }
            ShowFatalError("Kritik Xəta", ex.Message);
        }
    }

    private void OnUnobservedTaskException(object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        try { File.AppendAllText(CrashLogPath, $"[{DateTime.Now}] TASK ERROR:\n{e.Exception}\n\n"); } catch { }
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
        // Write to stderr so we can see errors in terminal
        Console.Error.WriteLine($"[FATAL] {title}: {message}");
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
            "ALTER TABLE Customers ADD COLUMN BalanceRub REAL NOT NULL DEFAULT 0;",
            "ALTER TABLE CustomerBalanceHistories ADD COLUMN Currency TEXT NOT NULL DEFAULT 'Usd';",
            "ALTER TABLE CustomerBalanceHistories ADD COLUMN Amount REAL NOT NULL DEFAULT 0;",
            "ALTER TABLE CustomerBalanceHistories ADD COLUMN BalanceAfter REAL NOT NULL DEFAULT 0;",
            "ALTER TABLE CustomerBalanceHistories ADD COLUMN RelatedHistoryId INTEGER;",
            "UPDATE CustomerBalanceHistories SET Amount = AmountUsd, BalanceAfter = BalanceAfterUsd, Currency = 'Usd' WHERE (Amount = 0 AND AmountUsd != 0) OR Currency IS NULL;",
            @"CREATE TABLE IF NOT EXISTS CustomerBalanceHistories (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CustomerId INTEGER NOT NULL,
                TransactionId INTEGER,
                CreatedAt TEXT NOT NULL,
                Type TEXT NOT NULL,
                Currency TEXT NOT NULL DEFAULT 'Usd',
                Amount REAL NOT NULL DEFAULT 0,
                BalanceAfter REAL NOT NULL DEFAULT 0,
                AmountUsd REAL NOT NULL DEFAULT 0,
                BalanceAfterUsd REAL NOT NULL DEFAULT 0,
                RelatedHistoryId INTEGER,
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
