using CommunityToolkit.Mvvm.ComponentModel;

namespace UltimaAnimationForge.Models;

public partial class ClilocEntry : ObservableObject
{
    [ObservableProperty]
    private int number;

    [ObservableProperty]
    private string text = string.Empty;

    [ObservableProperty]
    private byte flag;

    [ObservableProperty]
    private bool isDirty;

    [ObservableProperty]
    private string category = string.Empty;

    [ObservableProperty]
    private bool isDuplicate;

    public string NumberHex => "0x" + Number.ToString("X8");

    public bool IsEmptyText => string.IsNullOrWhiteSpace(Text);

    public string RowStatus
    {
        get
        {
            if (IsDuplicate)
            {
                return "Duplicate";
            }

            if (string.IsNullOrWhiteSpace(Text))
            {
                return "Empty";
            }

            if (IsDirty)
            {
                return "Edited";
            }

            return "OK";
        }
    }

    partial void OnTextChanged(string value)
    {
        IsDirty = true;
        OnPropertyChanged(nameof(IsEmptyText));
        OnPropertyChanged(nameof(RowStatus));
    }

    partial void OnFlagChanged(byte value)
    {
        IsDirty = true;
        OnPropertyChanged(nameof(RowStatus));
    }

    partial void OnIsDirtyChanged(bool value)
    {
        OnPropertyChanged(nameof(RowStatus));
    }

    partial void OnIsDuplicateChanged(bool value)
    {
        OnPropertyChanged(nameof(RowStatus));
    }

    public void AcceptChanges()
    {
        IsDirty = false;
    }
}