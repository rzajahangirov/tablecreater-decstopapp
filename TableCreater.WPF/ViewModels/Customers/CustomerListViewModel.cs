using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TableCreater.WPF.Enums;
using TableCreater.WPF.Models;
using TableCreater.WPF.Services;

namespace TableCreater.WPF.ViewModels.Customers;

/// <summary>
/// ViewModel for the Customer List view.
/// Uses CommunityToolkit.Mvvm source generators for ObservableProperty and RelayCommand.
/// Mapped from Section 7.2 — CustomerListViewModel specification.
/// </summary>
public partial class CustomerListViewModel : ObservableObject
{
    private readonly ICustomerService _customerService;

    public CustomerListViewModel(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    // =========================================================================
    // OBSERVABLE PROPERTIES
    // =========================================================================

    [ObservableProperty]
    private ObservableCollection<CustomerReadResponse> _customers = new();

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private CustomerReadResponse? _selectedCustomer;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    // ── Add/Edit dialog fields ──────────────────────────────────────────
    [ObservableProperty]
    private string _dialogName = string.Empty;

    [ObservableProperty]
    private string _dialogPhone = string.Empty;

    [ObservableProperty]
    private CustomerType _dialogType = CustomerType.Active;

    [ObservableProperty]
    private bool _isDialogOpen;

    [ObservableProperty]
    private bool _isEditMode;

    [ObservableProperty]
    private decimal _dialogInitialBalance;

    [ObservableProperty]
    private PaymentCurrency _dialogInitialBalanceCurrency = PaymentCurrency.Usd;

    [ObservableProperty]
    private decimal _dialogInitialBalanceExchangeRate = 1.0m;

    [ObservableProperty]
    private string _statusFilter = "All"; // "All", "Active", "Inactive"

    private List<CustomerReadResponse> _allLoadedCustomers = new();

    public PaymentCurrency[] PaymentCurrencies => Enum.GetValues<PaymentCurrency>();
    public CustomerType[] CustomerTypes => Enum.GetValues<CustomerType>();

    // =========================================================================
    // COMMANDS
    // =========================================================================

    /// <summary>
    /// Loads all customers from the database. Called on view initialization.
    /// </summary>
    [RelayCommand]
    private async Task LoadCustomersAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            _allLoadedCustomers = await _customerService.GetAllCustomers();
            ApplyFilter();
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
    /// Searches customers by keyword (name or phone, case-insensitive).
    /// Mapped from Section 7.2: IAsyncRelayCommand SearchCommand.
    /// </summary>
    [RelayCommand]
    private async Task SearchAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            _allLoadedCustomers = string.IsNullOrWhiteSpace(SearchKeyword)
                ? await _customerService.GetAllCustomers()
                : await _customerService.SearchCustomers(SearchKeyword);

            ApplyFilter();
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
    /// Applies the current StatusFilter to _allLoadedCustomers.
    /// </summary>
    public void ApplyFilter()
    {
        IEnumerable<CustomerReadResponse> filtered = _allLoadedCustomers;

        if (StatusFilter == "Active")
        {
            filtered = filtered.Where(c => c.Type == CustomerType.Active);
        }
        else if (StatusFilter == "Inactive")
        {
            filtered = filtered.Where(c => c.Type == CustomerType.Inactive);
        }

        Customers = new ObservableCollection<CustomerReadResponse>(
            filtered.OrderBy(c => c.Type == CustomerType.Active ? 0 : 1).ThenBy(c => c.Name));
    }

    partial void OnStatusFilterChanged(string value)
    {
        ApplyFilter();
    }

    /// <summary>
    /// Opens the "Add Customer" dialog with empty fields.
    /// </summary>
    [RelayCommand]
    private void OpenAddDialog()
    {
        DialogName = string.Empty;
        DialogPhone = string.Empty;
        DialogType = CustomerType.Active;
        DialogInitialBalance = 0;
        DialogInitialBalanceCurrency = PaymentCurrency.Usd;
        DialogInitialBalanceExchangeRate = 1.0m;
        IsEditMode = false;
        IsDialogOpen = true;
    }

    /// <summary>
    /// Opens the "Edit Customer" dialog pre-populated with the selected customer's data.
    /// </summary>
    [RelayCommand]
    private void OpenEditDialog()
    {
        if (SelectedCustomer == null) return;

        DialogName = SelectedCustomer.Name;
        DialogPhone = SelectedCustomer.Phone;
        DialogType = SelectedCustomer.Type;
        IsEditMode = true;
        IsDialogOpen = true;
    }

    /// <summary>
    /// Saves the customer (create or update) based on dialog mode.
    /// </summary>
    [RelayCommand]
    private async Task SaveCustomerAsync()
    {
        try
        {
            ErrorMessage = null;

            if (IsEditMode && SelectedCustomer != null)
            {
                // C6 — Update existing customer
                await _customerService.UpdateCustomer(SelectedCustomer.Id,
                    new CustomerUpdateRequest
                    {
                        Name = DialogName,
                        Phone = DialogPhone,
                        Type = DialogType
                    });
            }
            else
            {
                // C1 — Create new customer
                await _customerService.CreateCustomer(
                    new CustomerCreateRequest
                    {
                        Name = DialogName,
                        Phone = DialogPhone,
                        InitialBalance = DialogInitialBalance != 0 ? DialogInitialBalance : null,
                        InitialBalanceCurrency = DialogInitialBalanceCurrency,
                        InitialExchangeRate = DialogInitialBalanceExchangeRate > 0 ? DialogInitialBalanceExchangeRate : 1.0m
                    });
            }

            IsDialogOpen = false;
            await LoadCustomersAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>
    /// Cancels the add/edit dialog.
    /// </summary>
    [RelayCommand]
    private void CancelDialog()
    {
        IsDialogOpen = false;
    }

    /// <summary>
    /// Deletes the selected customer. CASCADE will also delete their transactions.
    /// </summary>
    [RelayCommand]
    private async Task DeleteCustomerAsync()
    {
        if (SelectedCustomer == null) return;

        try
        {
            ErrorMessage = null;
            await _customerService.DeleteCustomer(SelectedCustomer.Id);
            SelectedCustomer = null;
            await LoadCustomersAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>
    /// Toggles customer status between Active and Inactive.
    /// </summary>
    [RelayCommand]
    private async Task ToggleStatusAsync(CustomerReadResponse? customer = null)
    {
        var target = customer ?? SelectedCustomer;
        if (target == null) return;

        try
        {
            ErrorMessage = null;
            var newStatus = target.Type == CustomerType.Active
                ? CustomerType.Inactive
                : CustomerType.Active;

            await _customerService.ChangeStatus(target.Id, newStatus);
            await LoadCustomersAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
