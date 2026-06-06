using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TableCreater.WPF.ViewModels.Transactions;

namespace TableCreater.WPF.Views.Pages;

/// <summary>
/// Code-behind for TransactionEntryPage.
/// Thin event handlers delegating to ViewModel commands.
/// </summary>
public partial class TransactionEntryPage : UserControl
{
    public TransactionEntryPage()
    {
        InitializeComponent();
    }

    private TransactionEntryViewModel? ViewModel => DataContext as TransactionEntryViewModel;

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = Window.GetWindow(this) as MainWindow;
        if (ViewModel?.PresetCustomerId != null)
            mainWindow?.NavigateToCustomerDetails(ViewModel.PresetCustomerId.Value);
        else
            mainWindow?.NavigateToCustomers();
    }

    private void BtnUpload_Click(object sender, RoutedEventArgs e)
        => ViewModel?.UploadFileCommand.Execute(null);

    private void BtnClearFile_Click(object sender, RoutedEventArgs e)
        => ViewModel?.ClearFileCommand.Execute(null);

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        // Force any focused input to commit its value to the binding source
        // before executing Save. Prevents "unsaved data on submit" issue.
        var focusedElement = Keyboard.FocusedElement as UIElement;
        focusedElement?.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        focusedElement?.Focus();

        ViewModel?.SaveCommand.Execute(null);
    }

    private void BtnReset_Click(object sender, RoutedEventArgs e)
        => ViewModel?.ResetFormCommand.Execute(null);
}
