using System.Windows;
using System.Windows.Controls;
using TableCreater.WPF.Models;
using TableCreater.WPF.ViewModels;

namespace TableCreater.WPF.Views.Pages;

public partial class DashboardPage : UserControl
{
    private DashboardViewModel? ViewModel => DataContext as DashboardViewModel;

    public DashboardPage()
    {
        InitializeComponent();
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel != null)
        {
            await ViewModel.LoadDashboardCommand.ExecuteAsync(null);
        }
    }

    private void BtnNewTransaction_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow is MainWindow mainWindow)
        {
            mainWindow.NavigateToNewTransaction();
        }
    }

    private void BtnViewAllTransactions_Click(object sender, RoutedEventArgs e)
    {
        if (Application.Current.MainWindow is MainWindow mainWindow)
        {
            mainWindow.NavigateToCustomers();
        }
    }

    private void BtnRowViewTransaction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TransactionReadResponse tx)
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.NavigateToTransactionDetail(tx.Id, tx.CustomerId);
            }
        }
    }

    private void BtnViewDebtorCustomer_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is NegativeCustomerItem debtor)
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.NavigateToCustomerDetails(debtor.CustomerId);
            }
        }
    }
}
