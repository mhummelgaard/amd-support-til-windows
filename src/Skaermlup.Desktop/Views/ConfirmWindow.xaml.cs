using System.Windows;
using Skaermlup.Core.Text;

namespace Skaermlup.Desktop.Views;

/// <summary>
/// Et spørgsmål før noget, der ikke kan fortrydes, med programmets egen store skrift. En
/// systemdialog ville have tekst i normal størrelse — for lille til at læse for netop dem, der
/// skal svare på den.
/// </summary>
public partial class ConfirmWindow : Window
{
    public ConfirmWindow(Confirmation question)
    {
        InitializeComponent();

        Title = "Skærmlup — " + question.Title;
        Headline.Text = question.Title;
        Body.ItemsSource = question.Body;
        KeepButton.Content = question.Keep;
        ProceedButton.Content = question.Proceed;

        Loaded += (_, _) => KeepButton.Focus();
    }

    /// <summary>Spørger og venter på svaret. Sandt hvis brugeren valgte at gøre det.</summary>
    public static bool Ask(Confirmation question, Window? owner)
    {
        var window = new ConfirmWindow(question);

        if (owner is { IsVisible: true })
        {
            window.Owner = owner;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            // Uden et vindue at stå foran — fx når der spørges fra ikonet ved uret — skal det
            // stå øverst og midt på skærmen, ikke gemt bag noget andet.
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.Topmost = true;
        }

        return window.ShowDialog() == true;
    }

    private void OnKeep(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnProceed(object sender, RoutedEventArgs e) => DialogResult = true;
}
