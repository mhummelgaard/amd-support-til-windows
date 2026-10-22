using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Skaermlup.Desktop;

/// <summary>
/// Får Oplæser til at sige den nye værdi, når den skifter — "5x" efter et tryk på "Forstør mere".
/// Uden det hører en, der bruger Oplæser, kun knappens navn og må selv lede efter tallet.
///
/// Mange med AMD tager Oplæser i brug, efterhånden som synet bliver dårligere, og bruger den
/// sammen med luppen. Kontrolpanelet skal kunne bruges begge veje.
/// </summary>
public static class SpokenValue
{
    public static readonly DependencyProperty AnnounceProperty = DependencyProperty.RegisterAttached(
        "Announce", typeof(bool), typeof(SpokenValue), new PropertyMetadata(false, OnAnnounceChanged));

    public static bool GetAnnounce(DependencyObject element) => (bool)element.GetValue(AnnounceProperty);

    public static void SetAnnounce(DependencyObject element, bool value) => element.SetValue(AnnounceProperty, value);

    private static readonly DependencyPropertyDescriptor TextChanges =
        DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));

    private static void OnAnnounceChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not TextBlock text) return;

        if ((bool)e.NewValue)
        {
            AutomationProperties.SetLiveSetting(text, AutomationLiveSetting.Polite);
            TextChanges.AddValueChanged(text, OnTextChanged);
        }
        else
        {
            TextChanges.RemoveValueChanged(text, OnTextChanged);
        }
    }

    private static void OnTextChanged(object? sender, EventArgs e)
    {
        // Kun når vinduet står fremme. Et skjult kontrolpanel skal ikke snakke.
        if (sender is not TextBlock { IsVisible: true } text) return;

        var peer = UIElementAutomationPeer.FromElement(text) ?? UIElementAutomationPeer.CreatePeerForElement(text);
        peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }
}
