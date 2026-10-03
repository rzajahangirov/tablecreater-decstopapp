using Microsoft.EntityFrameworkCore;
using TableCreater.WPF.Data.Entities;
using TableCreater.WPF.Enums;

namespace TableCreater.WPF.Data;

/// <summary>
/// Entity Framework Core DbContext for the TableCreater application.
/// Uses SQLite as the backing store. Configured per Section 6 of the Master Specification.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // === DbSets ===
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<CustomerBalanceHistory> CustomerBalanceHistories => Set<CustomerBalanceHistory>();
    public DbSet<CustomColumn> CustomColumns => Set<CustomColumn>();
    public DbSet<CustomFieldValue> CustomFieldValues => Set<CustomFieldValue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // =====================================================================
        // USER & ROLE — Many-to-Many via "UserRoles" join table
        // =====================================================================
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();

            entity.HasMany(u => u.Roles)
                  .WithMany(r => r.Users)
                  .UsingEntity<Dictionary<string, object>>(
                      "UserRoles",
                      join => join.HasOne<Role>().WithMany().HasForeignKey("RoleId"),
                      join => join.HasOne<User>().WithMany().HasForeignKey("UserId")
                  );
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasIndex(r => r.Name).IsUnique();
        });

        // =====================================================================
        // CUSTOMER
        // =====================================================================
        modelBuilder.Entity<Customer>(entity =>
        {
            // Store CustomerType enum as string (matches SQLite TEXT DEFAULT 'ACTIVE')
            entity.Property(c => c.Type)
                  .HasConversion<string>()
                  .HasDefaultValue(CustomerType.Active);

            // One-to-Many: Customer → Transactions with CASCADE DELETE
            entity.HasMany(c => c.Transactions)
                  .WithOne(t => t.Customer)
                  .HasForeignKey(t => t.CustomerId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Property(c => c.BalanceUsd)
                  .HasColumnType("REAL")
                  .HasDefaultValue(0m);
        });

        // =====================================================================
        // TRANSACTION
        // =====================================================================
        modelBuilder.Entity<Transaction>(entity =>
        {
            // Store enums as strings in SQLite
            entity.Property(t => t.TransportType)
                  .HasConversion<string>();

            entity.Property(t => t.TransportCurrency)
                  .HasConversion<string>();

            entity.Property(t => t.PaidCurrency)
                  .HasConversion<string>();

            entity.Property(t => t.AdditionalExpenseCurrency)
                  .HasConversion<string>();

            entity.Property(t => t.PaymentStatus)
                  .HasConversion<string>()
                  .HasDefaultValue(PaymentStatus.Paid);

            entity.Property(t => t.ProfitPerTonCurrency)
                  .HasConversion<string>()
                  .HasDefaultValue(PaymentCurrency.Usd);

            // IsCompleted default = false (SQLite INTEGER DEFAULT 0)
            entity.Property(t => t.IsCompleted)
                  .HasDefaultValue(false);

            // TransactionDate stored as TEXT in SQLite
            entity.Property(t => t.TransactionDate)
                  .HasConversion(
                      v => v.ToString("yyyy-MM-dd"),
                      v => DateOnly.Parse(v));

            // CreatedAt stored as TEXT in SQLite
            entity.Property(t => t.CreatedAt)
                  .HasConversion(
                      v => v.ToString("o"),
                      v => DateTime.Parse(v));

            // Decimal precision for financial fields
            entity.Property(t => t.WeightTon).HasColumnType("REAL");
            entity.Property(t => t.PricePerTonRub).HasColumnType("REAL");
            entity.Property(t => t.PricePerVehicle).HasColumnType("REAL");
            entity.Property(t => t.PaidAmount).HasColumnType("REAL");
            entity.Property(t => t.HistoricalExchangeRate).HasColumnType("REAL");
            entity.Property(t => t.HistoricalTotalExpenseUsd).HasColumnType("REAL");
            entity.Property(t => t.AdditionalExpenseAmount).HasColumnType("REAL");
            entity.Property(t => t.ProfitPerTon).HasColumnType("REAL");
            entity.Property(t => t.HistoricalUserProfitUsd).HasColumnType("REAL");
            entity.Property(t => t.HistoricalCustomerBilledUsd).HasColumnType("REAL");
            entity.Property(t => t.HistoricalBalanceDeltaUsd).HasColumnType("REAL");

            // Shipment tracking properties
            entity.Property(t => t.ShipmentStatus)
                  .HasConversion<string>()
                  .HasDefaultValue(ShipmentStatus.Pending);

            entity.Property(t => t.LoadedDate)
                  .HasConversion(
                      v => v.HasValue ? v.Value.ToString("yyyy-MM-dd") : null,
                      v => !string.IsNullOrEmpty(v) ? DateOnly.Parse(v) : null);

            entity.Property(t => t.InTransitStartDate)
                  .HasConversion(
                      v => v.HasValue ? v.Value.ToString("yyyy-MM-dd") : null,
                      v => !string.IsNullOrEmpty(v) ? DateOnly.Parse(v) : null);

            entity.Property(t => t.InTransitEndDate)
                  .HasConversion(
                      v => v.HasValue ? v.Value.ToString("yyyy-MM-dd") : null,
                      v => !string.IsNullOrEmpty(v) ? DateOnly.Parse(v) : null);

            entity.Property(t => t.DeliveredDate)
                  .HasConversion(
                      v => v.HasValue ? v.Value.ToString("yyyy-MM-dd") : null,
                      v => !string.IsNullOrEmpty(v) ? DateOnly.Parse(v) : null);

            entity.Property(t => t.IsInTransitAutoDates)
                  .HasDefaultValue(true);
        });

        // =====================================================================
        // CUSTOMER BALANCE HISTORY (Ledger)
        // =====================================================================
        modelBuilder.Entity<CustomerBalanceHistory>(entity =>
        {
            entity.Property(h => h.Type).HasConversion<string>();
            entity.Property(h => h.CreatedAt)
                  .HasConversion(
                      v => v.ToString("o"),
                      v => DateTime.Parse(v));
            entity.Property(h => h.AmountUsd).HasColumnType("REAL");
            entity.Property(h => h.BalanceAfterUsd).HasColumnType("REAL");

            entity.HasOne(h => h.Customer)
                  .WithMany(c => c.BalanceHistories)
                  .HasForeignKey(h => h.CustomerId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(h => h.Transaction)
                  .WithMany()
                  .HasForeignKey(h => h.TransactionId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // =====================================================================
        // CUSTOM COLUMN (Dynamic Schema)
        // =====================================================================
        modelBuilder.Entity<CustomColumn>(entity =>
        {
            entity.Property(c => c.InputType)
                  .HasConversion<string>();

            entity.Property(c => c.DataType)
                  .HasConversion<string>();
        });

        // =====================================================================
        // CUSTOM FIELD VALUE (Junction: Transaction ↔ CustomColumn)
        // =====================================================================
        modelBuilder.Entity<CustomFieldValue>(entity =>
        {
            entity.HasOne(cfv => cfv.Transaction)
                  .WithMany(t => t.CustomFieldValues)
                  .HasForeignKey(cfv => cfv.TransactionId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(cfv => cfv.CustomColumn)
                  .WithMany(cc => cc.CustomFieldValues)
                  .HasForeignKey(cfv => cfv.CustomColumnId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Unique constraint: one value per transaction per column
            entity.HasIndex(cfv => new { cfv.TransactionId, cfv.CustomColumnId })
                  .IsUnique();
        });
    }
}
