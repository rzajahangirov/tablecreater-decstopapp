using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TableCreater.WPF.ViewModels.Customers;

namespace TableCreater.WPF.Views.Pages;

/// <summary>
/// Code-behind for CustomerListPage.
/// Delegates all business logic to CustomerListViewModel.
/// </summary>
public partial class CustomerListPage : UserControl
{
    public CustomerListPage()
    {
        InitializeComponent();
    }

    private CustomerListViewModel? ViewModel => DataContext as CustomerListViewModel;

    private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            ViewModel?.SearchCommand.Execute(null);
    }

    private void BtnSearch_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.SearchCommand.Execute(null);
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.OpenAddDialogCommand.Execute(null);
    }

    private void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.OpenEditDialogCommand.Execute(null);
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Bu müştərini silmək istədiyinizə əminsiniz?\nBütün əlaqəli tranzaksiyalar da silinəcək.",
            "Silməni Təsdiqlə",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
            ViewModel?.DeleteCustomerCommand.Execute(null);
    }

    private void BtnSaveDialog_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.SaveCustomerCommand.Execute(null);
    }

    private void BtnCancelDialog_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.CancelDialogCommand.Execute(null);
    }

    private void BtnToggleStatus_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.ToggleStatusCommand.Execute(null);
    }

    private void MenuCustomerDetails_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.SelectedCustomer != null)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.NavigateToCustomerDetails(ViewModel.SelectedCustomer.Id);
        }
    }

    private void MenuCustomerEdit_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.OpenEditDialogCommand.Execute(null);
    }

    private void MenuToggleStatus_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.ToggleStatusCommand.Execute(null);
    }

    private void MenuCustomerDelete_Click(object sender, RoutedEventArgs e)
    {
        BtnDelete_Click(sender, e);
    }

    private void CustomerGrid_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel?.SelectedCustomer != null)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.NavigateToCustomerDetails(ViewModel.SelectedCustomer.Id);
        }
    }
}
