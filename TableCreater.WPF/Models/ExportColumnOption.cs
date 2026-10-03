using CommunityToolkit.Mvvm.ComponentModel;

namespace TableCreater.WPF.Models;

/// <summary>
/// Represents a selectable column option for Excel export.
/// </summary>
public partial class ExportColumnOption : ObservableObject
{
    public string Id { get; init; } = string.Empty;
    public string HeaderName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public bool IsDefault { get; init; } = true;

    [ObservableProperty]
    private bool _isSelected = true;
}
