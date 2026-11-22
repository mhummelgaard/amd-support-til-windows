using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Skaermlup.Desktop;

/// <summary>
/// Lader en side rulle, når man trækker i den med fingeren. Det gør WPF ikke af sig selv: uden
/// det her skulle man ramme den smalle rullebjælke i kanten, og det er svært nok med en mus.
///
/// Sættes kun på de sider, der skal rulle — ikke på alle rullefelter. Et tekstfelt har også et
/// rullefelt indeni, og dér skal et træk med fingeren markere tekst, ikke rulle.
/// </summary>
public static class TouchScrolling
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(TouchScrolling), new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);

    public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);

    private static void OnEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not ScrollViewer viewer) return;

        if ((bool)e.NewValue)
        {
            viewer.PanningMode = PanningMode.VerticalOnly;
            viewer.ManipulationBoundaryFeedback += StayPut;
        }
        else
        {
            viewer.PanningMode = PanningMode.None;
            viewer.ManipulationBoundaryFeedback -= StayPut;
        }
    }

    /// <summary>
    /// Når siden er rullet helt til bunds, får WPF ellers hele vinduet til at hoppe som tegn på det.
    /// For en, der i forvejen har svært ved at holde øje med, hvor de er, er det bare uro.
    /// </summary>
    private static void StayPut(object? sender, ManipulationBoundaryFeedbackEventArgs e) => e.Handled = true;
}
