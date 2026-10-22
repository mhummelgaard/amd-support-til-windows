using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace Skaermlup.Desktop.Services;

/// <summary>
/// Ikonet ved uret. Uden det var der, med vejledningsbjælken skjult og kontrolpanelet lukket,
/// intet på skærmen der viste at Skærmlup kørte — og ingen måde at nå programmet på, ud over en
/// tastaturgenvej man skulle kunne huske, eller at finde det i Start-menuen igen.
///
/// Et klik åbner kontrolpanelet. Ikke et dobbeltklik: at ramme et lille ikon to gange hurtigt
/// efter hinanden er svært med nedsat syn. Højreklik giver en menu med stor skrift.
///
/// Windows 11 lægger nye ikoner i den skjulte del bag "^", og intet program må selv flytte det
/// frem. Det gør brugeren — eller den der hjælper — én gang.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly AppShell _shell;
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _power;

    public TrayIcon(AppShell shell)
    {
        _shell = shell;

        _power = new Forms.ToolStripMenuItem(string.Empty, null, (_, _) => _shell.Magnifier.Toggle());

        var menu = new Forms.ContextMenuStrip
        {
            // Windows' egen menuskrift er for lille til dem, programmet er lavet til.
            Font = new Drawing.Font("Segoe UI", 16f),
            ShowImageMargin = false,
        };
        menu.Items.Add(_power);
        menu.Items.Add(new Forms.ToolStripMenuItem("Indstillinger", null, (_, _) => _shell.ShowControlPanel()));
        menu.Items.Add(new Forms.ToolStripMenuItem("Vejledning", null, (_, _) => _shell.ShowHelp()));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new Forms.ToolStripMenuItem("Afslut Skærmlup …", null, (_, _) => _shell.ExitAfterAsking()));
        menu.Opening += (_, _) => Refresh();

        _icon = new Forms.NotifyIcon
        {
            Icon = AppIcon(),
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) _shell.ShowControlPanel();
        };

        _shell.Magnifier.Changed += (_, _) => Refresh();
        Refresh();
    }

    private void Refresh()
    {
        bool on = _shell.Magnifier.IsActive;
        _power.Text = on ? "Sluk luppen" : "Tænd luppen";

        // Højst 63 tegn — mere kan Windows ikke vise.
        _icon.Text = on ? "Skærmlup — luppen er tændt" : "Skærmlup — luppen er slukket";
    }

    private static Drawing.Icon AppIcon()
    {
        // Programmets eget ikon, det samme som på skrivebordet og i Start-menuen.
        if (Environment.ProcessPath is { } path && Drawing.Icon.ExtractAssociatedIcon(path) is { } icon)
            return icon;
        return Drawing.SystemIcons.Application;
    }

    public void Dispose()
    {
        // Uden det her bliver et dødt ikon stående ved uret, til nogen fører musen hen over det.
        _icon.Visible = false;
        _icon.Dispose();
    }
}
