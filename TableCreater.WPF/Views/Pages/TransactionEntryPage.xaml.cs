using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TableCreater.WPF.ViewModels.Transactions;

namespace TableCreater.WPF.Views.Pages;

/// <summary>
/// Code-behind for TransactionEntryPage.
/// Thin event handlers delegating to ViewModel commands.
/// Includes click-away calculation commit, scroll wheel bubbling, and keyboard shortcuts.
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
    {
        var result = MessageBox.Show(
            "Formdakı məlumatları sıfırlamaq istədiyinizə əminsiniz?",
            "Sıfırlamanı Təsdiqlə",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            ViewModel?.ResetFormCommand.Execute(null);
        }
    }

    private void BtnOpenStatusDialog_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;

        var dialog = new Dialogs.ShipmentStatusDialog(
            ViewModel.ShipmentStatus,
            ViewModel.LoadedDate.HasValue ? DateOnly.FromDateTime(ViewModel.LoadedDate.Value) : null,
            ViewModel.InTransitStartDate.HasValue ? DateOnly.FromDateTime(ViewModel.InTransitStartDate.Value) : null,
            ViewModel.DeliveredDate.HasValue ? DateOnly.FromDateTime(ViewModel.DeliveredDate.Value) : null)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            ViewModel.ShipmentStatus = dialog.ResultStatus;
            ViewModel.LoadedDate = dialog.ResultLoadedDate?.ToDateTime(TimeOnly.MinValue);
            ViewModel.InTransitStartDate = dialog.ResultInTransitStartDate?.ToDateTime(TimeOnly.MinValue);
            ViewModel.DeliveredDate = dialog.ResultDeliveredDate?.ToDateTime(TimeOnly.MinValue);
        }
    }

    /// <summary>
    /// Click-away behavior: clicking outside active input controls clears focus
    /// and causes immediate live calculation updates.
    /// </summary>
    private void Page_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject d) return;

        bool isInteractiveInput = false;
        var current = d;
        while (current != null && current != this)
        {
            if (current is TextBox or PasswordBox or ComboBox or Button or CheckBox or RadioButton or DatePicker)
            {
                isInteractiveInput = true;
                break;
            }
            current = VisualTreeHelper.GetParent(current);
        }

        if (!isInteractiveInput)
        {
            Focus();
            Keyboard.ClearFocus();
        }
    }

    /// <summary>
    /// Smooth mouse wheel scrolling for the entire transaction form.
    /// </summary>
    private void MainScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer scv)
        {
            scv.ScrollToVerticalOffset(scv.VerticalOffset - (e.Delta * 0.75));
            e.Handled = true;
        }
    }

    /// <summary>
    /// Desktop keyboard shortcuts: Ctrl+S to save, Esc to go back.
    /// </summary>
    private void UserControl_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            BtnSave_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            BtnBack_Click(sender, e);
            e.Handled = true;
        }
    }
}
