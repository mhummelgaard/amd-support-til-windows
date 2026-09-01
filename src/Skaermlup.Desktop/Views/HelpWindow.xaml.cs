using System.Windows;
using Skaermlup.Core.Settings;
using Skaermlup.Core.Text;

namespace Skaermlup.Desktop.Views;

/// <summary>Den fulde vejledning. Samme tekster som bjælken bruger — bare med plads til det hele.</summary>
public partial class HelpWindow : Window
{
    public HelpWindow(AppSettings settings)
    {
        InitializeComponent();

        Steps.ItemsSource = Instructions.GettingStarted(settings);
        Sections.ItemsSource = Instructions.FullGuide(settings);
        Keys.ItemsSource = Shortcuts.ForDisplay();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
