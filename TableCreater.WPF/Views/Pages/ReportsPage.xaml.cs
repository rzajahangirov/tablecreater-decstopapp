using System.Windows;
using System.Windows.Controls;
using TableCreater.WPF.ViewModels;

namespace TableCreater.WPF.Views.Pages;

public partial class ReportsPage : UserControl
{
    public ReportsPage()
    {
        InitializeComponent();
    }

    private ReportsViewModel? ViewModel => DataContext as ReportsViewModel;

    private void BtnDateRangeReport_Click(object sender, RoutedEventArgs e)
        => ViewModel?.GenerateDateRangeReportCommand.Execute(null);

    private void BtnCustomerReport_Click(object sender, RoutedEventArgs e)
        => ViewModel?.GenerateCustomerReportCommand.Execute(null);

    private void BtnExportExcel_Click(object sender, RoutedEventArgs e)
        => ViewModel?.ExportToExcelCommand.Execute(null);
}
