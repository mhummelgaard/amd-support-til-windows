using System.Runtime.CompilerServices;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Skaermlup.Core.Colors;
using Skaermlup.Core.Input;
using Skaermlup.Core.Magnification;
using Skaermlup.Core.Settings;
using Skaermlup.Core.Text;
using Skaermlup.Desktop.Interop;
using Skaermlup.Desktop.Services;

namespace Skaermlup.Desktop.ViewModels;

/// <summary>Kontrolpanelet: alt kan stilles her, og alt står med stor skrift.</summary>
internal sealed partial class ControlPanelViewModel : ObservableObject
{
    private readonly AppShell _shell;
    private readonly List<ColorOption> _colorOptions;

    public ControlPanelViewModel(AppShell shell)
    {
        _shell = shell;

        _colorOptions = ColorSchemes.Order
            .Select(scheme => new ColorOption(scheme, scheme == shell.Settings.Colors, SelectColors))
            .ToList();

        _shell.Magnifier.Changed += (_, _) => RefreshAll();
        _shell.MouseTriggerHealthChanged += (_, _) => RefreshAll();
    }

    private AppSettings S => _shell.Settings;

    // ---------- Tilstand ----------

    public bool IsOn => _shell.Magnifier.IsActive;

    public string PowerLabel => IsOn ? "SLUK LUPPEN" : "TÆND LUPPEN";

    public string StatusHeadline => Instructions.Headline(IsOn, S);

    public string StatusDetail => Instructions.CallToAction(IsOn, S, _shell.MouseTriggerWorks);

    public IReadOnlyList<string> GettingStarted => Instructions.GettingStarted(S);

    // ---------- Advarsler ----------

    public bool ShowMouseWarning => !_shell.MouseTriggerWorks;

    public bool ShowCaptureWarning => !_shell.Magnifier.HiddenFromCapture;

    public bool ShowShortcutWarning => _shell.UnavailableShortcuts.Count > 0;

    public string ShortcutWarningText
    {
        get
        {
            var taken = _shell.UnavailableShortcuts
                .Select(s => s.Display)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return taken.Count == 0
                ? string.Empty
                : "Et andet program bruger allerede " + string.Join(", ", taken)
                  + ". De genveje virker ikke her. Museknappen virker uanset hvad.";
        }
    }

    // ---------- Forstørrelse ----------

    public string ZoomText => ZoomLadder.Format(S.Zoom);

    [RelayCommand]
    private void ZoomIn() => _shell.Magnifier.ZoomIn();

    [RelayCommand]
    private void ZoomOut() => _shell.Magnifier.ZoomOut();

    // ---------- Størrelse ----------

    public string SizeText => S.Mode == ViewMode.Dock
        ? S.DockHeightPercent + " %"
        : S.LensWidthPercent + " %";

    public string SizeCaption => S.Mode == ViewMode.Dock
        ? "Læsebåndets højde i forhold til skærmen"
        : "Luppens bredde i forhold til skærmen";

    [RelayCommand]
    private void SizeUp() => _shell.Magnifier.ResizeLens(LensSizer.StepPercent);

    [RelayCommand]
    private void SizeDown() => _shell.Magnifier.ResizeLens(-LensSizer.StepPercent);

    // ---------- Visning ----------

    public bool IsLensMode
    {
        get => S.Mode == ViewMode.Lens;
        set { if (value) Set(ViewMode.Lens, s => s.Mode, (s, v) => s.Mode = v, nameof(IsLensMode)); }
    }

    public bool IsDockMode
    {
        get => S.Mode == ViewMode.Dock;
        set { if (value) Set(ViewMode.Dock, s => s.Mode, (s, v) => s.Mode = v, nameof(IsDockMode)); }
    }

    public bool IsRectangle
    {
        get => S.Shape == LensShape.Rectangle;
        set { if (value) Set(LensShape.Rectangle, s => s.Shape, (s, v) => s.Shape = v, nameof(IsRectangle)); }
    }

    public bool IsCircle
    {
        get => S.Shape == LensShape.Circle;
        set { if (value) Set(LensShape.Circle, s => s.Shape, (s, v) => s.Shape = v, nameof(IsCircle)); }
    }

    public bool DockAtTop
    {
        get => S.DockEdge == DockEdge.Top;
        set { if (value) Set(DockEdge.Top, s => s.DockEdge, (s, v) => s.DockEdge = v, nameof(DockAtTop)); }
    }

    public bool DockAtBottom
    {
        get => S.DockEdge == DockEdge.Bottom;
        set { if (value) Set(DockEdge.Bottom, s => s.DockEdge, (s, v) => s.DockEdge = v, nameof(DockAtBottom)); }
    }

    // ---------- Farver ----------

    public IReadOnlyList<ColorOption> ColorOptions => _colorOptions;

    private void SelectColors(ColorScheme scheme)
        => Set(scheme, s => s.Colors, (s, v) => s.Colors = v, nameof(ColorOptions));

    // ---------- Den gule tone ----------

    public string YellowToneText => YellowTones.Name(S.YellowTone);

    public string YellowToneDescription => YellowTones.Description(S.YellowTone);

    /// <summary>
    /// Selve farven, så ruden mellem knapperne kan males i den tone der er valgt. Uden den
    /// prøve ville indstillingen kun kunne mærkes i de to gule tilstande — her kan tonen ses
    /// med det samme, uanset hvilke farver luppen ellers står på.
    /// </summary>
    public Brush YellowToneSwatch
    {
        get
        {
            var (red, green, blue) = YellowTones.Rgb(S.YellowTone);
            var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
            brush.Freeze();
            return brush;
        }
    }

    [RelayCommand]
    private void YellowToneWarmer() => SetYellowTone(YellowTones.Warmer(S.YellowTone));

    [RelayCommand]
    private void YellowTonePaler() => SetYellowTone(YellowTones.Paler(S.YellowTone));

    private void SetYellowTone(YellowTone tone)
    {
        if (!Set(tone, s => s.YellowTone, (s, v) => s.YellowTone = v, nameof(YellowToneText))) return;

        OnPropertyChanged(nameof(YellowToneDescription));
        OnPropertyChanged(nameof(YellowToneSwatch));
    }

    public string ContrastText => Signed(S.Contrast);

    public string BrightnessText => Signed(S.Brightness);

    private static string Signed(int value) => value > 0 ? "+" + value : value.ToString();

    [RelayCommand]
    private void ContrastUp() => Set(Math.Min(50, S.Contrast + 10), s => s.Contrast, (s, v) => s.Contrast = v, nameof(ContrastText));

    [RelayCommand]
    private void ContrastDown() => Set(Math.Max(-50, S.Contrast - 10), s => s.Contrast, (s, v) => s.Contrast = v, nameof(ContrastText));

    [RelayCommand]
    private void BrightnessUp() => Set(Math.Min(50, S.Brightness + 10), s => s.Brightness, (s, v) => s.Brightness = v, nameof(BrightnessText));

    [RelayCommand]
    private void BrightnessDown() => Set(Math.Max(-50, S.Brightness - 10), s => s.Brightness, (s, v) => s.Brightness = v, nameof(BrightnessText));

    // ---------- Museknapper ----------

    public bool TriggerMiddle
    {
        get => S.Trigger.HasFlag(MouseTriggerButtons.Middle);
        set => SetTrigger(MouseTriggerButtons.Middle, value);
    }

    public bool TriggerSideBack
    {
        get => S.Trigger.HasFlag(MouseTriggerButtons.SideBack);
        set => SetTrigger(MouseTriggerButtons.SideBack, value);
    }

    public bool TriggerSideForward
    {
        get => S.Trigger.HasFlag(MouseTriggerButtons.SideForward);
        set => SetTrigger(MouseTriggerButtons.SideForward, value);
    }

    public bool TriggerChord
    {
        get => S.Trigger.HasFlag(MouseTriggerButtons.LeftRightChord);
        set => SetTrigger(MouseTriggerButtons.LeftRightChord, value);
    }

    private void SetTrigger(MouseTriggerButtons button, bool enabled)
    {
        var next = enabled ? S.Trigger | button : S.Trigger & ~button;

        // Den sidste knap kan ikke slås fra. Uden en udløser ville programmet kun
        // kunne betjenes fra tastaturet, og det er ikke det brugeren har bedt om.
        if (next == MouseTriggerButtons.None) next = S.Trigger;

        Set(next, s => s.Trigger, (s, v) => s.Trigger = v, nameof(TriggerMiddle));
        RefreshAll();
    }

    public bool TriggerIsToggle
    {
        get => S.TriggerMode == TriggerMode.Toggle;
        set { if (value) Set(TriggerMode.Toggle, s => s.TriggerMode, (s, v) => s.TriggerMode = v, nameof(TriggerIsToggle)); }
    }

    public bool TriggerIsHold
    {
        get => S.TriggerMode == TriggerMode.Hold;
        set { if (value) Set(TriggerMode.Hold, s => s.TriggerMode, (s, v) => s.TriggerMode = v, nameof(TriggerIsHold)); }
    }

    public bool BlockTriggerClicks
    {
        get => S.BlockTriggerClicks;
        set => Set(value, s => s.BlockTriggerClicks, (s, v) => s.BlockTriggerClicks = v);
    }

    public bool WheelAlwaysZooms
    {
        get => S.Wheel == WheelAction.AlwaysZoom;
        set => Set(value ? WheelAction.AlwaysZoom : WheelAction.ModifierOnly,
                   s => s.Wheel, (s, v) => s.Wheel = v, nameof(WheelAlwaysZooms));
    }

    // ---------- Læsning ----------

    public bool FollowCaret
    {
        get => S.FollowCaret;
        set => Set(value, s => s.FollowCaret, (s, v) => s.FollowCaret = v);
    }

    public bool FollowFocus
    {
        get => S.FollowFocus;
        set => Set(value, s => s.FollowFocus, (s, v) => s.FollowFocus = v);
    }

    public bool MarkerIsCrosshair
    {
        get => S.Marker == MarkerStyle.Crosshair;
        set { if (value) Set(MarkerStyle.Crosshair, s => s.Marker, (s, v) => s.Marker = v, nameof(MarkerIsCrosshair)); }
    }

    public bool MarkerIsUnderline
    {
        get => S.Marker == MarkerStyle.Underline;
        set { if (value) Set(MarkerStyle.Underline, s => s.Marker, (s, v) => s.Marker = v, nameof(MarkerIsUnderline)); }
    }

    public bool MarkerIsRing
    {
        get => S.Marker == MarkerStyle.Ring;
        set { if (value) Set(MarkerStyle.Ring, s => s.Marker, (s, v) => s.Marker = v, nameof(MarkerIsRing)); }
    }

    public bool MarkerIsNone
    {
        get => S.Marker == MarkerStyle.None;
        set { if (value) Set(MarkerStyle.None, s => s.Marker, (s, v) => s.Marker = v, nameof(MarkerIsNone)); }
    }

    public string MarkerSizeText => S.MarkerSize + " %";

    [RelayCommand]
    private void MarkerBigger()
        => Set(Math.Min(250, S.MarkerSize + 25), s => s.MarkerSize, (s, v) => s.MarkerSize = v, nameof(MarkerSizeText));

    [RelayCommand]
    private void MarkerSmaller()
        => Set(Math.Max(50, S.MarkerSize - 25), s => s.MarkerSize, (s, v) => s.MarkerSize = v, nameof(MarkerSizeText));

    /// <summary>
    /// Ringen om musepilen når Start-menuen og resten af Windows' egen skal dækker luppen.
    /// </summary>
    public bool ShellPointerRing
    {
        get => S.ShellPointerRing;
        set
        {
            Set(value, s => s.ShellPointerRing, (s, v) => s.ShellPointerRing = v);
            OnPropertyChanged(nameof(ShowRingUnavailable));
        }
    }

    /// <summary>Flyt luppen ud til siden af Start-menuen frem for at lade den forsvinde bag den.</summary>
    public bool ShellMoveLensAside
    {
        get => S.ShellMoveLensAside;
        set => Set(value, s => s.ShellMoveLensAside, (s, v) => s.ShellMoveLensAside = v);
    }

    /// <summary>Sandt hvis brugeren har bedt om ringen, men markørstørrelsen ikke levner plads.</summary>
    public bool ShowRingUnavailable
        => S.ShellPointerRing && CursorRing.Compute(SystemCursor.PointerSize, S.ShellPointerRingSize).IsEmpty;

    public bool SmoothImage
    {
        get => S.SmoothImage;
        set => Set(value, s => s.SmoothImage, (s, v) => s.SmoothImage = v);
    }

    /// <summary>Halverer antallet af billeder i sekundet. Mærkes næsten ikke, men sparer strøm.</summary>
    public bool CalmMode
    {
        get => S.MaxFps <= 30;
        set => Set(value ? 30 : 60, s => s.MaxFps, (s, v) => s.MaxFps = v, nameof(CalmMode));
    }

    // ---------- Vejledningsbjælken ----------

    public bool ShowInstructions
    {
        get => S.ShowInstructions;
        set => Set(value, s => s.ShowInstructions, (s, v) => s.ShowInstructions = v);
    }

    public bool InstructionsAtBottom
    {
        get => S.InstructionsAt == InstructionPlacement.Bottom;
        set { if (value) Set(InstructionPlacement.Bottom, s => s.InstructionsAt, (s, v) => s.InstructionsAt = v, nameof(InstructionsAtBottom)); }
    }

    public bool InstructionsAtTop
    {
        get => S.InstructionsAt == InstructionPlacement.Top;
        set { if (value) Set(InstructionPlacement.Top, s => s.InstructionsAt, (s, v) => s.InstructionsAt = v, nameof(InstructionsAtTop)); }
    }

    public string InstructionScaleText => S.InstructionScale + " %";

    [RelayCommand]
    private void InstructionsBigger()
        => Set(Math.Min(250, S.InstructionScale + 25), s => s.InstructionScale, (s, v) => s.InstructionScale = v, nameof(InstructionScaleText));

    [RelayCommand]
    private void InstructionsSmaller()
        => Set(Math.Max(75, S.InstructionScale - 25), s => s.InstructionScale, (s, v) => s.InstructionScale = v, nameof(InstructionScaleText));

    // ---------- Opstart ----------

    public bool StartWithWindows
    {
        get => S.StartWithWindows;
        set
        {
            bool applied = StartupRegistration.Set(value) && value;
            Set(applied, s => s.StartWithWindows, (s, v) => s.StartWithWindows = v, nameof(StartWithWindows));
            OnPropertyChanged(nameof(StartWithWindows));
        }
    }

    public bool StartMagnifierOnLaunch
    {
        get => S.StartMagnifierOnLaunch;
        set => Set(value, s => s.StartMagnifierOnLaunch, (s, v) => s.StartMagnifierOnLaunch = v);
    }

    // ---------- Handlinger ----------

    [RelayCommand]
    private void TogglePower() => _shell.Magnifier.Toggle();

    [RelayCommand]
    private void ShowHelp() => _shell.ShowHelp();

    [RelayCommand]
    private void ResetToDefaults()
    {
        if (!_shell.Confirm(Confirmations.Reset)) return;

        var defaults = new AppSettings
        {
            StartWithWindows = S.StartWithWindows,
        };

        CopyInto(defaults, S);
        _shell.SettingsChanged();
        RefreshAll();
    }

    private static void CopyInto(AppSettings from, AppSettings to)
    {
        to.Zoom = from.Zoom;
        to.Mode = from.Mode;
        to.Shape = from.Shape;
        to.LensWidthPercent = from.LensWidthPercent;
        to.LensHeightPercent = from.LensHeightPercent;
        to.DockEdge = from.DockEdge;
        to.DockHeightPercent = from.DockHeightPercent;
        to.Colors = from.Colors;
        to.YellowTone = from.YellowTone;
        to.Contrast = from.Contrast;
        to.Brightness = from.Brightness;
        to.SmoothImage = from.SmoothImage;
        to.Marker = from.Marker;
        to.MarkerSize = from.MarkerSize;
        to.ShellPointerRing = from.ShellPointerRing;
        to.ShellPointerRingSize = from.ShellPointerRingSize;
        to.ShellMoveLensAside = from.ShellMoveLensAside;
        to.BorderThickness = from.BorderThickness;
        to.Trigger = from.Trigger;
        to.TriggerMode = from.TriggerMode;
        to.BlockTriggerClicks = from.BlockTriggerClicks;
        to.Wheel = from.Wheel;
        to.FollowCaret = from.FollowCaret;
        to.FollowFocus = from.FollowFocus;
        to.ShowInstructions = from.ShowInstructions;
        to.InstructionsAt = from.InstructionsAt;
        to.InstructionScale = from.InstructionScale;
        to.MaxFps = from.MaxFps;
        to.StartMagnifierOnLaunch = from.StartMagnifierOnLaunch;
    }

    [RelayCommand]
    private void Exit() => _shell.ExitAfterAsking();

    // ---------- Fælles ----------

    private bool Set<T>(T value, Func<AppSettings, T> read, Action<AppSettings, T> write,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(read(S), value)) return false;

        write(S, value);
        OnPropertyChanged(propertyName);
        _shell.SettingsChanged();
        return true;
    }

    private void RefreshAll()
    {
        foreach (var option in _colorOptions) option.Sync(S.Colors);
        OnPropertyChanged(string.Empty);
    }
}
