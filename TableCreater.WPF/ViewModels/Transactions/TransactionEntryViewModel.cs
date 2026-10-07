using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using TableCreater.WPF.Enums;
using TableCreater.WPF.Models;
using TableCreater.WPF.Services;

namespace TableCreater.WPF.ViewModels.Transactions;

/// <summary>
/// ViewModel for the Transaction Entry form.
/// Implements the LIVE Calculation Engine from Section 5.1 — every input change
/// immediately recalculates TotalExpenseUsd and RemainingDebtUsd in real-time.
///
/// Uses CommunityToolkit.Mvvm partial methods (OnXxxChanged) for reactive updates.
/// </summary>
public partial class TransactionEntryViewModel : ObservableObject
{
    private readonly ITransactionService _transactionService;
    private readonly ICustomerService _customerService;

    public TransactionEntryViewModel(
        ITransactionService transactionService,
        ICustomerService customerService)
    {
        _transactionService = transactionService;
        _customerService = customerService;
    }

    // =========================================================================
    // CUSTOMER SELECTION
    // =========================================================================

    [ObservableProperty]
    private ObservableCollection<CustomerReadResponse> _customers = new();

    [ObservableProperty]
    private CustomerReadResponse? _selectedCustomer;

    [ObservableProperty]
    private string _customerSearchText = string.Empty;

    // =========================================================================
    // TRANSACTION INPUT FIELDS
    // All fields trigger Recalculate() on change via partial OnXxxChanged methods.
    // =========================================================================

    [ObservableProperty]
    private DateOnly _transactionDate = DateOnly.FromDateTime(DateTime.Today);

    [ObservableProperty]
    private string _productName = string.Empty;

    [ObservableProperty]
    private string _receivingCompany = string.Empty;

    [ObservableProperty]
    private string _sendingCompany = string.Empty;

    [ObservableProperty]
    private decimal _weightTon;

    [ObservableProperty]
    private decimal _pricePerTonRub;

    [ObservableProperty]
    private TransportType _transportType = TransportType.Truck;

    /// <summary>
    /// Nəqliyyat valyutası — manual seçilir, artıq avtomatik TransportType-a görə deyil.
    /// </summary>
    [ObservableProperty]
    private PaymentCurrency _transportCurrency = PaymentCurrency.Usd;

    [ObservableProperty]
    private int _vehicleCount = 1;

    [ObservableProperty]
    private decimal _pricePerVehicle;

    [ObservableProperty]
    private PaymentCurrency _paidCurrency = PaymentCurrency.Usd;

    [ObservableProperty]
    private decimal _paidAmount;

    [ObservableProperty]
    private decimal _historicalExchangeRate = 1.0m;

    // =========================================================================
    // ƏLAVƏ XƏRCLƏR (Additional Expenses)
    // =========================================================================

    [ObservableProperty]
    private decimal _additionalExpenseAmount;

    [ObservableProperty]
    private PaymentCurrency _additionalExpenseCurrency = PaymentCurrency.Usd;

    [ObservableProperty]
    private string _additionalExpenseDescription = string.Empty;

    // =========================================================================
    // ÖDƏNİŞ STATUSU VƏ TON BAŞINA QAZANC
    // =========================================================================

    [ObservableProperty]
    private PaymentStatus _paymentStatus = PaymentStatus.Paid;

    [ObservableProperty]
    private decimal _profitPerTon;

    [ObservableProperty]
    private PaymentCurrency _profitPerTonCurrency = PaymentCurrency.Usd;

    public PaymentStatus[] PaymentStatuses => Enum.GetValues<PaymentStatus>();

    // =========================================================================
    // GÖNDƏRMƏ STATUSU VƏ TARİXLƏR (Shipment Tracking)
    // =========================================================================

    [ObservableProperty]
    private ShipmentStatus _shipmentStatus = ShipmentStatus.Pending;

    [ObservableProperty]
    private DateTime? _loadedDate;

    [ObservableProperty]
    private DateTime? _inTransitStartDate;

    [ObservableProperty]
    private DateTime? _inTransitEndDate;

    [ObservableProperty]
    private DateTime? _deliveredDate;

    [ObservableProperty]
    private bool _isInTransitAutoDates = true;

    public ShipmentStatus[] ShipmentStatuses => Enum.GetValues<ShipmentStatus>();

    // =========================================================================
    // FILE UPLOAD
    // =========================================================================

    [ObservableProperty]
    private string? _documentFilePath;

    [ObservableProperty]
    private string? _documentFileName;

    [ObservableProperty]
    private bool _hasDocument;

    // =========================================================================
    // LIVE CALCULATION OUTPUTS (Read-only, updated reactively)
    // =========================================================================

    [ObservableProperty]
    private decimal _liveGoodsCostRub;

    [ObservableProperty]
    private decimal _liveGoodsCostUsd;

    [ObservableProperty]
    private decimal _liveTransportCostRaw;

    [ObservableProperty]
    private decimal _liveTransportCostUsd;

    [ObservableProperty]
    private decimal _liveAdditionalExpenseUsd;

    [ObservableProperty]
    private decimal _liveTotalExpenseUsd;

    [ObservableProperty]
    private decimal _livePaidInUsd;

    [ObservableProperty]
    private decimal _liveRemainingDebtUsd;

    [ObservableProperty]
    private decimal _liveUserProfitUsd;

    [ObservableProperty]
    private decimal _liveCustomerBilledUsd;

    [ObservableProperty]
    private decimal _liveBalanceDeltaUsd;

    /// <summary>
    /// Friendly text: "Gəlir" (positive), "Zərər" (negative), or "Balans" (zero).
    /// </summary>
    [ObservableProperty]
    private string _debtStatusText = "Gəlir";

    /// <summary>
    /// Color indicator: Green for Gəlir, Red for Zərər, Gray for balanced.
    /// </summary>
    [ObservableProperty]
    private string _debtStatusColor = "#2E7D32";

    // =========================================================================
    // UI STATE
    // =========================================================================

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _successMessage;

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private long _editTransactionId;

    /// <summary>
    /// Pre-set customer ID when navigating from customer detail view.
    /// </summary>
    [ObservableProperty]
    private long? _presetCustomerId;

    // =========================================================================
    // ENUM SOURCES (for ComboBox binding)
    // =========================================================================

    public TransportType[] TransportTypes => Enum.GetValues<TransportType>();
    public PaymentCurrency[] PaymentCurrencies => Enum.GetValues<PaymentCurrency>();

    // =========================================================================
    // REACTIVE RECALCULATION — partial OnXxxChanged methods
    // CommunityToolkit.Mvvm generates these hooks automatically.
    // Every input change triggers instant recalculation.
    // =========================================================================

    partial void OnWeightTonChanged(decimal value) => Recalculate();
    partial void OnPricePerTonRubChanged(decimal value) => Recalculate();
    partial void OnTransportTypeChanged(TransportType value) => Recalculate();
    partial void OnTransportCurrencyChanged(PaymentCurrency value) => Recalculate();
    partial void OnVehicleCountChanged(int value) => Recalculate();
    partial void OnPricePerVehicleChanged(decimal value) => Recalculate();
    partial void OnPaidCurrencyChanged(PaymentCurrency value) => Recalculate();
    partial void OnPaidAmountChanged(decimal value) => Recalculate();
    partial void OnHistoricalExchangeRateChanged(decimal value) => Recalculate();
    partial void OnAdditionalExpenseAmountChanged(decimal value) => Recalculate();
    partial void OnPaymentStatusChanged(PaymentStatus value) => Recalculate();
    partial void OnProfitPerTonChanged(decimal value) => Recalculate();
    partial void OnProfitPerTonCurrencyChanged(PaymentCurrency value) => Recalculate();
    partial void OnAdditionalExpenseCurrencyChanged(PaymentCurrency value) => Recalculate();

    // =========================================================================
    // CORE CALCULATION ENGINE — Section 5.1 (Live Preview)
    // Identical algorithm to TransactionService.CalculateHistoricalFields(),
    // but operates on ViewModel properties for instant UI feedback.
    //
    // UPDATED: Transport currency is now manual. Additional expenses included.
    // =========================================================================

    private void Recalculate()
    {
        decimal rate = HistoricalExchangeRate > 0 ? HistoricalExchangeRate : 1m;

        // Step 1 & 2: Goods Cost (rate = 1 USD = X RUB, so RUB / rate = USD)
        LiveGoodsCostRub = WeightTon * PricePerTonRub;
        LiveGoodsCostUsd = LiveGoodsCostRub / rate;

        // Step 3 & 4: Transport Cost — manual currency selection
        LiveTransportCostRaw = PricePerVehicle * VehicleCount;
        LiveTransportCostUsd = TransportCurrency == PaymentCurrency.Rub
            ? LiveTransportCostRaw / rate    // RUB → USD
            : LiveTransportCostRaw;          // Already USD

        // Step 5: Additional Expense
        LiveAdditionalExpenseUsd = AdditionalExpenseAmount > 0
            ? (AdditionalExpenseCurrency == PaymentCurrency.Rub
                ? AdditionalExpenseAmount / rate
                : AdditionalExpenseAmount)
            : 0m;

        // Step 6: Total Expense
        LiveTotalExpenseUsd = LiveGoodsCostUsd + LiveTransportCostUsd + LiveAdditionalExpenseUsd;

        // Step 7: Paid in USD
        LivePaidInUsd = PaidCurrency == PaymentCurrency.Rub
            ? PaidAmount / rate              // RUB → USD
            : PaidAmount;                    // Already USD

        // Step 8: Remaining Debt
        LiveRemainingDebtUsd = LivePaidInUsd - LiveTotalExpenseUsd;

        // Step 9: User Profit (ton başına qazanc)
        decimal profitPerTonUsd = ProfitPerTonCurrency == PaymentCurrency.Rub
            ? ProfitPerTon / rate
            : ProfitPerTon;
        LiveUserProfitUsd = WeightTon * profitPerTonUsd;

        // Step 10: Customer Billed
        LiveCustomerBilledUsd = LiveTotalExpenseUsd + LiveUserProfitUsd;

        // Step 11: Balance Delta
        if (PaymentStatus == PaymentStatus.Unpaid)
        {
            LiveBalanceDeltaUsd = -LiveCustomerBilledUsd;
        }
        else
        {
            LiveBalanceDeltaUsd = LivePaidInUsd - LiveCustomerBilledUsd;
        }

        // Update status indicator
        if (LiveBalanceDeltaUsd < 0)
        {
            DebtStatusText = "Borc";
            DebtStatusColor = "#C62828";  // Red
        }
        else if (LiveBalanceDeltaUsd > 0)
        {
            DebtStatusText = "Avans";
            DebtStatusColor = "#2E7D32";  // Green
        }
        else
        {
            DebtStatusText = "Balans";
            DebtStatusColor = "#888888";    // Gray
        }

        // Re-evaluate save command eligibility
        SaveCommand.NotifyCanExecuteChanged();
    }

    // =========================================================================
    // COMMANDS
    // =========================================================================

    /// <summary>
    /// Loads the customer list for the selection ComboBox.
    /// </summary>
    [RelayCommand]
    private async Task LoadCustomersAsync()
    {
        try
        {
            var customers = await _customerService.GetAllCustomers();
            Customers = new ObservableCollection<CustomerReadResponse>(customers);

            // If a preset customer ID was provided, auto-select it
            if (PresetCustomerId.HasValue)
            {
                SelectedCustomer = Customers.FirstOrDefault(c => c.Id == PresetCustomerId.Value);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>
    /// Loads existing transaction data for editing.
    /// </summary>
    [RelayCommand]
    private async Task LoadForEditAsync(long transactionId)
    {
        try
        {
            IsLoading = true;
            IsEditMode = true;
            EditTransactionId = transactionId;

            var data = await _transactionService.GetTransactionForUpdate(transactionId);

            TransactionDate = data.TransactionDate;
            ProductName = data.ProductName;
            ReceivingCompany = data.ReceivingCompany;
            SendingCompany = data.SendingCompany ?? string.Empty;
            WeightTon = data.WeightTon;
            PricePerTonRub = data.PricePerTonRub;
            TransportType = data.TransportType;
            TransportCurrency = data.TransportCurrency;
            VehicleCount = data.VehicleCount ?? 1;
            PricePerVehicle = data.PricePerVehicle ?? 0m;
            PaidCurrency = data.PaidCurrency;
            PaidAmount = data.PaidAmount ?? 0m;
            HistoricalExchangeRate = data.HistoricalExchangeRate;

            // Additional Expenses
            AdditionalExpenseAmount = data.AdditionalExpenseAmount ?? 0m;
            AdditionalExpenseCurrency = data.AdditionalExpenseCurrency ?? PaymentCurrency.Usd;
            AdditionalExpenseDescription = data.AdditionalExpenseDescription ?? string.Empty;

            // Payment Status & Profit
            PaymentStatus = data.PaymentStatus;
            ProfitPerTon = data.ProfitPerTon;
            ProfitPerTonCurrency = data.ProfitPerTonCurrency;

            // Shipment Tracking
            ShipmentStatus = data.ShipmentStatus;
            LoadedDate = data.LoadedDate?.ToDateTime(TimeOnly.MinValue);
            InTransitStartDate = data.InTransitStartDate?.ToDateTime(TimeOnly.MinValue);
            InTransitEndDate = data.InTransitEndDate?.ToDateTime(TimeOnly.MinValue);
            DeliveredDate = data.DeliveredDate?.ToDateTime(TimeOnly.MinValue);
            IsInTransitAutoDates = data.IsInTransitAutoDates;

            if (!string.IsNullOrEmpty(data.DocumentImageUrl))
            {
                DocumentFilePath = data.DocumentImageUrl;
                DocumentFileName = Path.GetFileName(data.DocumentImageUrl);
                HasDocument = true;
            }

            Recalculate();
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

    /// <summary>
    /// Opens a file dialog to select a document (PDF/JPG/PNG).
    /// Replaces Java's MultipartFile upload.
    /// </summary>
    [RelayCommand]
    private void UploadFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Sənəd Seçin",
            Filter = "Sənədlər|*.pdf;*.jpg;*.jpeg;*.png;*.bmp|Bütün Fayllar|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            DocumentFilePath = dialog.FileName;
            DocumentFileName = Path.GetFileName(dialog.FileName);
            HasDocument = true;
        }
    }

    /// <summary>
    /// Clears the selected document.
    /// </summary>
    [RelayCommand]
    private void ClearFile()
    {
        DocumentFilePath = null;
        DocumentFileName = null;
        HasDocument = false;
    }

    /// <summary>
    /// Saves the transaction (create or update).
    /// Can only execute when required fields are valid.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            SuccessMessage = null;

            if (IsEditMode)
            {
                var request = new TransactionUpdateRequest
                {
                    TransactionDate = TransactionDate,
                    ProductName = ProductName,
                    ReceivingCompany = ReceivingCompany,
                    SendingCompany = string.IsNullOrWhiteSpace(SendingCompany) ? null : SendingCompany,
                    WeightTon = WeightTon,
                    PricePerTonRub = PricePerTonRub,
                    TransportType = TransportType,
                    TransportCurrency = TransportCurrency,
                    VehicleCount = VehicleCount,
                    PricePerVehicle = PricePerVehicle,
                    PaidCurrency = PaidCurrency,
                    PaidAmount = PaidAmount,
                    HistoricalExchangeRate = HistoricalExchangeRate,
                    DocumentFilePath = DocumentFilePath,
                    AdditionalExpenseAmount = AdditionalExpenseAmount > 0 ? AdditionalExpenseAmount : null,
                    AdditionalExpenseCurrency = AdditionalExpenseAmount > 0 ? AdditionalExpenseCurrency : null,
                    AdditionalExpenseDescription = string.IsNullOrWhiteSpace(AdditionalExpenseDescription)
                        ? null : AdditionalExpenseDescription,
                    PaymentStatus = PaymentStatus,
                    ProfitPerTon = ProfitPerTon,
                    ProfitPerTonCurrency = ProfitPerTonCurrency,
                    ShipmentStatus = ShipmentStatus,
                    LoadedDate = LoadedDate.HasValue ? DateOnly.FromDateTime(LoadedDate.Value) : null,
                    InTransitStartDate = InTransitStartDate.HasValue ? DateOnly.FromDateTime(InTransitStartDate.Value) : null,
                    InTransitEndDate = (ShipmentStatus == ShipmentStatus.Delivered && DeliveredDate.HasValue) ? DateOnly.FromDateTime(DeliveredDate.Value) : null,
                    DeliveredDate = DeliveredDate.HasValue ? DateOnly.FromDateTime(DeliveredDate.Value) : null,
                    IsInTransitAutoDates = true,
                    IsCompleted = ShipmentStatus == ShipmentStatus.Delivered
                };

                await _transactionService.UpdateTransaction(EditTransactionId, request);
                SuccessMessage = "Tranzaksiya uğurla yeniləndi!";

                // Redirect to Customer Details
                var customerId = SelectedCustomer!.Id;
                var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
                mainWindow?.NavigateToCustomerDetails(customerId);
            }
            else
            {
                var request = new TransactionCreateRequest
                {
                    TransactionDate = TransactionDate,
                    ProductName = ProductName,
                    ReceivingCompany = ReceivingCompany,
                    SendingCompany = string.IsNullOrWhiteSpace(SendingCompany) ? null : SendingCompany,
                    WeightTon = WeightTon,
                    PricePerTonRub = PricePerTonRub,
                    TransportType = TransportType,
                    TransportCurrency = TransportCurrency,
                    VehicleCount = VehicleCount,
                    PricePerVehicle = PricePerVehicle,
                    PaidCurrency = PaidCurrency,
                    PaidAmount = PaidAmount,
                    HistoricalExchangeRate = HistoricalExchangeRate,
                    DocumentFilePath = DocumentFilePath,
                    AdditionalExpenseAmount = AdditionalExpenseAmount > 0 ? AdditionalExpenseAmount : null,
                    AdditionalExpenseCurrency = AdditionalExpenseAmount > 0 ? AdditionalExpenseCurrency : null,
                    AdditionalExpenseDescription = string.IsNullOrWhiteSpace(AdditionalExpenseDescription)
                        ? null : AdditionalExpenseDescription,
                    PaymentStatus = PaymentStatus,
                    ProfitPerTon = ProfitPerTon,
                    ProfitPerTonCurrency = ProfitPerTonCurrency,
                    ShipmentStatus = ShipmentStatus,
                    LoadedDate = LoadedDate.HasValue ? DateOnly.FromDateTime(LoadedDate.Value) : null,
                    InTransitStartDate = InTransitStartDate.HasValue ? DateOnly.FromDateTime(InTransitStartDate.Value) : null,
                    InTransitEndDate = (ShipmentStatus == ShipmentStatus.Delivered && DeliveredDate.HasValue) ? DateOnly.FromDateTime(DeliveredDate.Value) : null,
                    DeliveredDate = DeliveredDate.HasValue ? DateOnly.FromDateTime(DeliveredDate.Value) : null,
                    IsInTransitAutoDates = true
                };

                await _transactionService.CreateTransaction(request, SelectedCustomer!.Id);
                SuccessMessage = "Tranzaksiya uğurla yaradıldı!";

                // Redirect to Customer Details
                var customerId = SelectedCustomer.Id;
                var mainWindow = System.Windows.Application.Current.MainWindow as MainWindow;
                mainWindow?.NavigateToCustomerDetails(customerId);
            }
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

    /// <summary>
    /// Validation: Save is only enabled when all required fields are filled.
    /// </summary>
    private bool CanSave()
    {
        return SelectedCustomer != null
            && !string.IsNullOrWhiteSpace(ProductName)
            && !string.IsNullOrWhiteSpace(ReceivingCompany)
            && WeightTon > 0
            && PricePerTonRub >= 0
            && HistoricalExchangeRate > 0;
    }

    // Notify save eligibility on these changes too
    partial void OnSelectedCustomerChanged(CustomerReadResponse? value)
    {
        if (value != null)
        {
            CustomerSearchText = value.Name;
        }
        SaveCommand.NotifyCanExecuteChanged();
    }
    partial void OnProductNameChanged(string value) => SaveCommand.NotifyCanExecuteChanged();
    partial void OnReceivingCompanyChanged(string value) => SaveCommand.NotifyCanExecuteChanged();

    /// <summary>
    /// Resets the form to default values for a new entry.
    /// </summary>
    [RelayCommand]
    private void ResetForm()
    {
        TransactionDate = DateOnly.FromDateTime(DateTime.Today);
        ProductName = string.Empty;
        ReceivingCompany = string.Empty;
        SendingCompany = string.Empty;
        WeightTon = 0;
        PricePerTonRub = 0;
        TransportType = TransportType.Truck;
        TransportCurrency = PaymentCurrency.Usd;
        VehicleCount = 1;
        PricePerVehicle = 0;
        PaidCurrency = PaymentCurrency.Usd;
        PaidAmount = 0;
        AdditionalExpenseAmount = 0;
        AdditionalExpenseCurrency = PaymentCurrency.Usd;
        AdditionalExpenseDescription = string.Empty;
        PaymentStatus = PaymentStatus.Paid;
        ProfitPerTon = 0;
        ProfitPerTonCurrency = PaymentCurrency.Usd;
        ShipmentStatus = ShipmentStatus.Pending;
        LoadedDate = null;
        InTransitStartDate = null;
        InTransitEndDate = null;
        DeliveredDate = null;
        IsInTransitAutoDates = true;
        // Keep exchange rate — user likely needs the same rate for multiple entries
        ClearFile();
        ErrorMessage = null;
        SuccessMessage = null;
        Recalculate();
    }
}
