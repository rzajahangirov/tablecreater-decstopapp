using System.Windows;
using System.Windows.Controls;
using TableCreater.WPF.ViewModels;
using TableCreater.WPF.Services;
using Microsoft.Extensions.DependencyInjection;

namespace TableCreater.WPF.Views.Pages;

public partial class ReportsPage : UserControl
{
    public ReportsPage()
    {
        InitializeComponent();

        Loaded += async (s, e) =>
        {
            if (ViewModel != null && ViewModel.Customers.Count == 0)
            {
                await ViewModel.LoadCustomersAsync();
            }
        };
    }

    private ReportsViewModel? ViewModel => DataContext as ReportsViewModel;

    private void BtnDateRangeReport_Click(object sender, RoutedEventArgs e)
        => ViewModel?.GenerateDateRangeReportCommand.Execute(null);

    private void BtnCustomerReport_Click(object sender, RoutedEventArgs e)
        => ViewModel?.GenerateCustomerReportCommand.Execute(null);

    private async void BtnExportExcel_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedCustomer == null)
        {
            MessageBox.Show("Zəhmət olmasa ixrac etmək üçün müştəri seçin.", "Müştəri Seçilməyib", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Load transactions for the selected customer
        var transactionService = App.Services.GetRequiredService<ITransactionService>();
        var transactions = await transactionService.GetTransactionsByCustomer(ViewModel.SelectedCustomer.Id);

        var dialog = new Dialogs.ExcelExportColumnsDialog(
            ViewModel.SelectedCustomer.Id,
            ViewModel.SelectedCustomer.Name,
            ViewModel.ExcelService,
            transactions)
        {
            Owner = Window.GetWindow(this)
        };

        dialog.ShowDialog();
    }

    private void BtnExportReportExcel_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null || ViewModel.AllReportTransactions.Count == 0)
        {
            MessageBox.Show("İxrac ediləcək tranzaksiya yoxdur.", "Məlumat", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        long customerId = ViewModel.SelectedCustomer?.Id ?? 0;
        string title = !string.IsNullOrEmpty(ViewModel.ReportTypeTitle)
            ? ViewModel.ReportTypeTitle
            : "Maliyyə Hesabatı";

        var dialog = new Dialogs.ExcelExportColumnsDialog(
            customerId,
            title,
            ViewModel.ExcelService,
            ViewModel.AllReportTransactions,
            ViewModel.ReportTransactions.ToList())
        {
            Owner = Window.GetWindow(this)
        };

        dialog.ShowDialog();
    }
}
