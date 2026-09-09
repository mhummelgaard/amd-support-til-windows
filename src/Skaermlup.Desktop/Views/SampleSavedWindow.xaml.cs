using System.Diagnostics;
using System.IO;
using System.Windows;
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
