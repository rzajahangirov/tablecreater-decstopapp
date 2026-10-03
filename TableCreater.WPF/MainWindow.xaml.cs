using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using TableCreater.WPF.Services;
using TableCreater.WPF.ViewModels;
using TableCreater.WPF.ViewModels.Customers;
using TableCreater.WPF.ViewModels.Transactions;
using TableCreater.WPF.Views.Pages;

namespace TableCreater.WPF;

/// <summary>
/// Code-behind for MainWindow. Handles navigation and initial setup.
/// Minimal code-behind — delegates to ViewModels for business logic.
/// </summary>
public partial class MainWindow
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = App.Services.GetRequiredService<MainViewModel>();
        DataContext = _viewModel;

        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Set user info in the sidebar
        var user = App.Services.GetRequiredService<IAuthService>().GetCurrentUser();
        if (user != null)
        {
            TxtUserName.Text = $"{user.FirstName} {user.LastName}";
            TxtUserEmail.Text = user.Email;
        }

        // Navigate to the default page (Customers)
        NavigateToCustomers();
    }

    // =========================================================================
    // NAVIGATION EVENT HANDLERS
    // =========================================================================

    private void BtnNavCustomers_Click(object sender, RoutedEventArgs e)
    {
        NavigateToCustomers();
    }

    private void BtnNavNewTransaction_Click(object sender, RoutedEventArgs e)
    {
        NavigateToNewTransaction();
    }

    private void BtnNavReports_Click(object sender, RoutedEventArgs e)
    {
        NavigateToReports();
    }

    private void BtnNavSettings_Click(object sender, RoutedEventArgs e)
    {
        TxtPageTitle.Text = "Settings & About";
        PageContent.Content = new SettingsPage();
    }

    private void BtnLogout_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.LogoutCommand.Execute(null);
    }

    // =========================================================================
    // HELPERS
    // =========================================================================

    public void NavigateToCustomers()
    {
        TxtPageTitle.Text = "Müştərilər";

        var vm = App.Services.GetRequiredService<CustomerListViewModel>();
        var page = new CustomerListPage { DataContext = vm };
        PageContent.Content = page;

        vm.LoadCustomersCommand.Execute(null);
    }

    public void NavigateToCustomerDetails(long customerId)
    {
        TxtPageTitle.Text = "Müştəri Detalları";

        var vm = App.Services.GetRequiredService<CustomerDetailsViewModel>();
        vm.CustomerId = customerId;

        var page = new CustomerDetailsPage { DataContext = vm };
        PageContent.Content = page;

        vm.LoadDataCommand.Execute(null);
    }

    public void NavigateToNewTransaction(long? presetCustomerId = null)
    {
        TxtPageTitle.Text = "Yeni Tranzaksiya";

        var vm = App.Services.GetRequiredService<TransactionEntryViewModel>();
        if (presetCustomerId.HasValue)
            vm.PresetCustomerId = presetCustomerId;

        var page = new TransactionEntryPage { DataContext = vm };
        PageContent.Content = page;

        vm.LoadCustomersCommand.Execute(null);
    }

    public void NavigateToEditTransaction(long transactionId, long customerId)
    {
        TxtPageTitle.Text = "Tranzaksiyanı Redaktə Et";

        var vm = App.Services.GetRequiredService<TransactionEntryViewModel>();
        vm.PresetCustomerId = customerId;

        var page = new TransactionEntryPage { DataContext = vm };
        PageContent.Content = page;

        vm.LoadCustomersCommand.Execute(null);
        vm.LoadForEditCommand.Execute(transactionId);
    }

    public void NavigateToTransactionDetail(long transactionId, long customerId)
    {
        TxtPageTitle.Text = "Tranzaksiya Detalları";

        var vm = App.Services.GetRequiredService<TransactionDetailViewModel>();
        vm.TransactionId = transactionId;
        vm.CustomerId = customerId;

        var page = new TransactionDetailPage { DataContext = vm };
        PageContent.Content = page;

        vm.LoadDataCommand.Execute(null);
    }

    private void NavigateToReports()
    {
        TxtPageTitle.Text = "Hesabatlar";

        var vm = App.Services.GetRequiredService<ReportsViewModel>();
        var page = new ReportsPage { DataContext = vm };
        PageContent.Content = page;

        vm.LoadCustomersCommand.Execute(null);
    }
}