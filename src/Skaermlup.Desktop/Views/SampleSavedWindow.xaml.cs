using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using Skaermlup.Core.Text;

namespace Skaermlup.Desktop.Views;

/// <summary>
/// Vises efter et prøvebillede er gemt. Se <see cref="SampleWarning"/> for hvorfor advarslen
/// findes: billedet viser skærmen, og den der har bedt om det kan ikke nødvendigvis selv læse
/// hvad der kom med.
/// </summary>
public partial class SampleSavedWindow : Window
{
    private readonly string _folder;

    public SampleSavedWindow(string folder, IReadOnlyList<string> savedFiles)
    {
        InitializeComponent();

        _folder = folder;

        Title = SampleWarning.Title;
        Headline.Text = SampleWarning.Headline;
        Subheadline.Text = SampleWarning.Subheadline;
        Body.ItemsSource = SampleWarning.Body;

        FolderHeading.Text = SampleWarning.FolderHeading;
        FolderPath.Text = folder;

        FileHeading.Text = SampleWarning.FileHeading;
        Files.ItemsSource = savedFiles.Count > 0
            ? savedFiles.Select(Path.GetFileName).ToList()
            : [SampleWarning.NoFiles];

        OpenButton.Content = SampleWarning.OpenFolder;
        CloseButton.Content = SampleWarning.Close;

        // Knappen er ligegyldig hvis der ikke er noget at åbne.
        OpenButton.IsEnabled = savedFiles.Count > 0;
    }

    private SampleSavedWindow()
    {
        InitializeComponent();
        _folder = string.Empty;
    }

    /// <summary>
    /// IT har slået prøvebilleder fra på denne pc (<see cref="Core.Settings.MachinePolicy"/>).
    /// Samme vindue og samme skriftstørrelse, men ikke advarselsfarvet: der er ikke sket noget
    /// farligt, og en rød besked ville skræmme uden grund. Ingen mappe, ingen filer — kun en
    /// forklaring og en knap til at lukke.
    /// </summary>
    public static SampleSavedWindow Blocked()
    {
        var window = new SampleSavedWindow
        {
            Title = SampleWarning.BlockedTitle,
            Height = 560,
        };

        window.Headline.Text = SampleWarning.BlockedHeadline;
        window.Subheadline.Text = SampleWarning.BlockedSubheadline;
        window.Body.ItemsSource = SampleWarning.BlockedBody;

        // Farverne hentes ved navn, så et kontrasttema stadig bytter dem ud.
        window.HeaderBand.Background = (Brush)window.FindResource("Primary");
        window.Subheadline.Foreground = (Brush)window.FindResource("OnDarkMuted");
        window.BodyCard.Background = (Brush)window.FindResource("Surface");
        window.BodyCard.BorderBrush = (Brush)window.FindResource("Line");

        window.FolderCard.Visibility = Visibility.Collapsed;
        window.OpenButton.Visibility = Visibility.Collapsed;

        // Den eneste knap er den, brugeren skal trykke på. Den står tydeligt og har fokus.
        window.CloseButton.Content = SampleWarning.Close;
        window.CloseButton.Style = (Style)window.FindResource("PrimaryButton");
        window.Loaded += (_, _) => window.CloseButton.Focus();

        return window;
    }

    /// <summary>
    /// At finde en mappe frem er svært nok med fuldt syn. Knappen åbner den, så brugeren kan
    /// nå at se billederne før de bliver sendt — det er hele meningen med advarslen.
    /// </summary>
    private void OnOpenFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            // UseShellExecute lader Windows selv vælge stifinderen. Ingen kommandolinje at
            // få galt i halsen, heller ikke når mappenavnet har mellemrum i sig.
            Process.Start(new ProcessStartInfo(_folder) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception)
        {
            // Kan mappen ikke åbnes, står stien stadig i vinduet og kan læses op eller kopieres.
            OpenButton.IsEnabled = false;
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
