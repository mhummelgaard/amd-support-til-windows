using System.ComponentModel;
using System.Windows;
using Skaermlup.Desktop.Services;
using Skaermlup.Desktop.ViewModels;

namespace Skaermlup.Desktop;

/// <summary>Kontrolpanelet. Alt kan stilles herfra, men intet af det er nødvendigt for at komme i gang.</summary>
public partial class MainWindow : Window
{
    private readonly AppShell _shell;

    internal MainWindow(AppShell shell)
    {
        _shell = shell;
        InitializeComponent();
        DataContext = new ControlPanelViewModel(shell);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Programmet skal blive kørende, så museknappen stadig tænder luppen.
        // Der lukkes helt med knappen "Afslut programmet".
        base.OnClosing(e);

        if (_shell.IsShuttingDown) return;

        e.Cancel = true;
        Hide();
        _shell.SaveNow();
    }
}
