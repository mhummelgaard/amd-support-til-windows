using CommunityToolkit.Mvvm.ComponentModel;
using Skaermlup.Core.Colors;
using Skaermlup.Core.Settings;

namespace Skaermlup.Desktop.ViewModels;

/// <summary>Én farvetilstand som den vises i kontrolpanelet.</summary>
internal sealed class ColorOption : ObservableObject
{
    private readonly Action<ColorScheme> _select;
    private bool _isSelected;

    public ColorOption(ColorScheme scheme, bool isSelected, Action<ColorScheme> select)
    {
        Scheme = scheme;
        _isSelected = isSelected;
        _select = select;
    }

    public ColorScheme Scheme { get; }

    public string Name => ColorSchemes.Name(Scheme);

    public string Description => ColorSchemes.Description(Scheme);

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!SetProperty(ref _isSelected, value)) return;
            if (value) _select(Scheme);
        }
    }

    public void Sync(ColorScheme current) => SetProperty(ref _isSelected, current == Scheme, nameof(IsSelected));
}
