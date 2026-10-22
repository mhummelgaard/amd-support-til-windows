namespace Skaermlup.Core.Text;

/// <summary>
/// Hvem der har lavet programmet, og på hvilke vilkår det må bruges.
///
/// Skærmlup er gratis for alle, der har det, og må kopieres og gives videre. Det eneste krav er,
/// at oplysningen om udvikleren følger med — det er netop betingelsen i MIT-licensen. Derfor
/// står det i programmet selv, ikke kun i en fil ingen åbner.
/// </summary>
public static class About
{
    public const string Developer = "Michael Hummelgaard";

    public const string Heading = "Om Skærmlup";

    public static string Version(string version) => "Skærmlup " + version;

    public static IReadOnlyList<string> Lines { get; } =
    [
        "Udviklet af " + Developer + ".",
        "Gratis for alle at bruge, kopiere og give videre.",
        "Det eneste krav er, at oplysningen om, hvem der har lavet programmet, følger med. "
        + "Det står i licensen (MIT), som ligger i programmets mappe.",
    ];

    /// <summary>Den korte udgave til en enkelt linje, fx nederst i vejledningen.</summary>
    public static string OneLine(string version)
        => Version(version) + " · udviklet af " + Developer + " · gratis for alle (MIT-licens)";
}
