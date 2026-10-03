using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TableCreater.WPF.Enums;
using TableCreater.WPF.Models;
using TableCreater.WPF.Services;

namespace TableCreater.WPF.ViewModels;

/// <summary>
/// ViewModel for the Transaction Detail page.
/// Displays comprehensive read-only information about a single transaction.
/// </summary>
public partial class TransactionDetailViewModel : ObservableObject
{
    private readonly ITransactionService _transactionService;
    private readonly ICustomerService _customerService;

    public TransactionDetailViewModel(
        ITransactionService transactionService,
        ICustomerService customerService)
    {
        _transactionService = transactionService;
        _customerService = customerService;
    }

    // =========================================================================
    // CORE DATA
    // =========================================================================

    [ObservableProperty]
    private long _transactionId;

    [ObservableProperty]
    private long _customerId;

    [ObservableProperty]
    private TransactionReadResponse? _transaction;

    [ObservableProperty]
    private string _customerName = string.Empty;

    [ObservableProperty]
    private string _customerPhone = string.Empty;

    // =========================================================================
    // UI STATE
    // =========================================================================

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    // =========================================================================
    // COMMANDS
    // =========================================================================

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            // Load all transactions for the customer to find the one we need
            var transactions = await _transactionService.GetTransactionsByCustomer(CustomerId);
            Transaction = transactions.FirstOrDefault(t => t.Id == TransactionId);

            if (Transaction == null)
            {
                ErrorMessage = "Tranzaksiya tapılmadı.";
                return;
            }

            // Load customer details
            var customer = await _customerService.GetCustomerById(CustomerId);
            CustomerName = customer.Name;
            CustomerPhone = customer.Phone;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    // =========================================================================
    // COMPUTED DISPLAY PROPERTIES
    // =========================================================================

    /// <summary>
    /// Formatted payment status display text.
    /// </summary>
    public string PaymentStatusText => Transaction?.PaymentStatus switch
    {
        PaymentStatus.Paid => "Ödənilib ✅",
        PaymentStatus.Unpaid => "Ödənilməyib ❌",
        _ => "—"
    };

    /// <summary>
    /// Formatted shipment status display text.
    /// </summary>
    public string ShipmentStatusText => Transaction?.ShipmentStatus switch
    {
        ShipmentStatus.Pending => "🕐 Gözləmədə",
        ShipmentStatus.Loaded => "📦 Yükləndi",
        ShipmentStatus.InTransit => "🚛 Yoldadır",
        ShipmentStatus.Delivered => "✅ Çatdı",
        _ => "—"
    };

    /// <summary>
    /// Formatted transport type.
    /// </summary>
    public string TransportTypeText => Transaction?.TransportType switch
    {
        TransportType.Truck => "Yük maşını (TIR)",
        TransportType.Wagon => "Vaqon (Dəmir yolu)",
        _ => Transaction?.TransportType.ToString() ?? "—"
    };
}
