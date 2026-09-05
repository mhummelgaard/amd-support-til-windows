using Shouldly;
using Skaermlup.Core.Input;

namespace Skaermlup.Tests;

public class ShellSurfacesTests
{
    [Theory]
    [InlineData("StartMenuExperienceHost")]
    [InlineData("ShellExperienceHost")]
    [InlineData("SearchHost")]
    [InlineData("SearchApp")]
    [InlineData("TextInputHost")]
    public void Skallens_egne_processer_genkendes(string process)
        => ShellSurfaces.IsShellSurface("Windows.UI.Core.CoreWindow", process, menuIsOpen: false).ShouldBeTrue();

    [Fact]
    public void Sti_og_filendelse_er_ligegyldigt()
    {
        ShellSurfaces.IsShellSurface(null, @"C:\Windows\SystemApps\StartMenuExperienceHost.exe", false).ShouldBeTrue();
        ShellSurfaces.IsShellSurface(null, "startmenuexperiencehost", false).ShouldBeTrue();
        ShellSurfaces.IsShellSurface(null, "  SearchHost.EXE  ", false).ShouldBeTrue();
    }

    [Theory]
    [InlineData("Shell_TrayWnd")]
    [InlineData("Shell_SecondaryTrayWnd")]
    [InlineData("NotifyIconOverflowWindow")]
    [InlineData("TopLevelWindowForOverflowXamlIsland")]
    [InlineData("MultitaskingViewFrame")]
    [InlineData("XamlExplorerHostIslandWindow")]
    public void Proceslinjen_og_opgavevisningen_genkendes_paa_vinduesklassen(string windowClass)
        => ShellSurfaces.IsShellSurface(windowClass, "explorer.exe", menuIsOpen: false).ShouldBeTrue();

    [Fact]
    public void Klassenavnet_maa_gerne_staves_med_andre_store_bogstaver()
        => ShellSurfaces.IsShellSurface("shell_traywnd", "explorer.exe", false).ShouldBeTrue();

    [Fact]
    public void Et_almindeligt_mappevindue_er_ikke_en_shell_flade()
        => ShellSurfaces.IsShellSurface("CabinetWClass", "explorer.exe", menuIsOpen: false).ShouldBeFalse();

    /// <summary>
    /// Mange helt almindelige apps bruger den samme vinduesklasse som Start-menuen. Den alene
    /// må derfor ikke tælle — ellers ville markøren skifte udseende i halvdelen af programmerne.
    /// </summary>
    [Fact]
    public void En_almindelig_app_med_samme_vinduesklasse_er_ikke_en_shell_flade()
        => ShellSurfaces.IsShellSurface("Windows.UI.Core.CoreWindow", "Notepad.exe", false).ShouldBeFalse();

    [Fact]
    public void Et_almindeligt_program_er_ikke_en_shell_flade()
        => ShellSurfaces.IsShellSurface("Chrome_WidgetWin_1", "chrome.exe", menuIsOpen: false).ShouldBeFalse();

    [Fact]
    public void En_aaben_menu_taeller_uanset_hvad_der_ellers_er_i_forgrunden()
        => ShellSurfaces.IsShellSurface("Chrome_WidgetWin_1", "chrome.exe", menuIsOpen: true).ShouldBeTrue();

    [Fact]
    public void Ingen_oplysninger_giver_ingen_shell_flade()
    {
        ShellSurfaces.IsShellSurface(null, null, menuIsOpen: false).ShouldBeFalse();
        ShellSurfaces.IsShellSurface("", "", menuIsOpen: false).ShouldBeFalse();
        ShellSurfaces.IsShellSurface("   ", "   ", menuIsOpen: false).ShouldBeFalse();
    }

    [Fact]
    public void Procesnavnet_skaeres_ned_til_det_blotte_navn()
    {
        ShellSurfaces.NormalizeProcess(@"C:\Windows\explorer.exe").ShouldBe("explorer");
        ShellSurfaces.NormalizeProcess("Explorer.EXE").ShouldBe("explorer");
        ShellSurfaces.NormalizeProcess("/usr/bin/noget").ShouldBe("noget");
        ShellSurfaces.NormalizeProcess(null).ShouldBe("");
    }
}
