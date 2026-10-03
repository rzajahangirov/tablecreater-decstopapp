using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TableCreater.WPF.Models;
using TableCreater.WPF.Services;
using TableCreater.WPF.ViewModels.Customers;

namespace TableCreater.WPF.ViewModels;

/// <summary>
/// Main application ViewModel — controls sidebar navigation and hosts the current page ViewModel.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IServiceProvider _serviceProvider;

    public MainViewModel(IAuthService authService, IServiceProvider serviceProvider)
    {
        _authService = authService;
        _serviceProvider = serviceProvider;
    }

    // =========================================================================
    // OBSERVABLE PROPERTIES
    // =========================================================================

    /// <summary>
    /// The currently active page ViewModel displayed in the content area.
    /// </summary>
    [ObservableProperty]
    private ObservableObject? _currentPage;

    /// <summary>
    /// Title for the current page shown in the header.
    /// </summary>
    [ObservableProperty]
    private string _currentPageTitle = "Customers";

    /// <summary>
    /// The currently logged-in user's session info.
    /// </summary>
    [ObservableProperty]
    private UserSessionInfo? _currentUser;

    /// <summary>
    /// Index of the selected navigation item in the sidebar.
    /// </summary>
    [ObservableProperty]
    private int _selectedNavIndex;

    // =========================================================================
    // INITIALIZATION
    // =========================================================================

    /// <summary>
    /// Called when the main window loads. Sets up the initial page and user info.
    /// </summary>
    [RelayCommand]
    private void Initialize()
    {
        CurrentUser = _authService.GetCurrentUser();
        NavigateToCustomers();
    }

    // =========================================================================
    // NAVIGATION COMMANDS
    // =========================================================================

    [RelayCommand]
    private void NavigateToCustomers()
    {
        var vm = _serviceProvider.GetService(typeof(CustomerListViewModel)) as CustomerListViewModel;
        CurrentPage = vm;
        CurrentPageTitle = "Customers";
        SelectedNavIndex = 0;
    }

    [RelayCommand]
    private void NavigateToReports()
    {
        // Step 5: Will navigate to FinancialDashboardViewModel
        CurrentPageTitle = "Reports";
        SelectedNavIndex = 1;
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        CurrentPageTitle = "Settings";
        SelectedNavIndex = 2;
    }

    [RelayCommand]
    private void Logout()
    {
        _authService.Logout();
        // In a full implementation, this would navigate back to the LoginView
    }
}
