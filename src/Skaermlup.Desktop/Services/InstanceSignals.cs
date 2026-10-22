using System.Security.AccessControl;
using System.Security.Principal;

namespace Skaermlup.Desktop.Services;

/// <summary>
/// De navngivne Windows-objekter, som programmet bruger til at tale med den kopi af sig selv der
/// allerede kører — og som installeren bruger til det samme.
///
/// Installeren kører som administrator, og ikke nødvendigvis som den person, der bruger luppen.
/// Med Windows' standardrettigheder kan den så hverken se at luppen kører, eller bede den lukke
/// pænt, og må i stedet tvinge den ned. Sker det mens luppen er tændt, bliver musepilen væk —
/// og brugeren har netop luppen tændt, fordi de skal kunne læse installeren. Derfor får
/// administratorer lov til at se programmet og sende det beskeder. Ikke andet.
/// </summary>
internal sealed class InstanceSignals : IDisposable
{
    /// <summary>Navnet. Installeren kender det også — se installer\Skaermlup.iss.</summary>
    public const string Key = "Skaermlup.EnKoerselAdGangen.v1";

    private readonly Mutex? _instanceLock;

    private InstanceSignals(Mutex? instanceLock, bool isFirstInstance)
    {
        _instanceLock = instanceLock;
        IsFirstInstance = isFirstInstance;

        ShowPanel = Signal(".VisPanel");
        Sample = Signal(".Proevebillede");
        Quit = Signal(".Afslut");
        Uninstall = Signal(".Afinstaller");
    }

    /// <summary>Sandt hvis det er denne kopi, der skal køre luppen.</summary>
    public bool IsFirstInstance { get; }

    /// <summary>En anden kopi blev startet: vis kontrolpanelet.</summary>
    public EventWaitHandle ShowPanel { get; }

    /// <summary>En anden kopi blev startet med --proevebillede.</summary>
    public EventWaitHandle Sample { get; }

    /// <summary>Installeren skal skifte filerne ud. Luk pænt ned, så musepilen kommer tilbage.</summary>
    public EventWaitHandle Quit { get; }

    /// <summary>Programmet afinstalleres. Fjern autostart for brugeren, og luk pænt ned.</summary>
    public EventWaitHandle Uninstall { get; }

    public static InstanceSignals Open()
    {
        try
        {
            var mutex = MutexAcl.Create(initiallyOwned: true, Key, out bool isFirst, LockSecurity());
            return new InstanceSignals(mutex, isFirst);
        }
        catch (UnauthorizedAccessException)
        {
            // Der kører en kopi, som denne ikke har lov at tale med — fx en der er startet som
            // administrator. To lupper oven på hinanden er værre end ingen, så denne giver op.
            return new InstanceSignals(null, isFirstInstance: false);
        }
    }

    private static EventWaitHandle Signal(string suffix)
    {
        try
        {
            return EventWaitHandleAcl.Create(false, EventResetMode.AutoReset, Key + suffix, out _, SignalSecurity());
        }
        catch (UnauthorizedAccessException)
        {
            // Kan ikke åbnes: beskeden når bare ikke frem. Et ubrugt signal skader ingen.
            return new EventWaitHandle(false, EventResetMode.AutoReset);
        }
    }

    private static MutexSecurity LockSecurity()
    {
        var security = new MutexSecurity();
        security.AddAccessRule(new MutexAccessRule(CurrentUser(), MutexRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new MutexAccessRule(Sid(WellKnownSidType.LocalSystemSid), MutexRights.FullControl, AccessControlType.Allow));

        // Nok til at se at programmet kører. Ikke nok til at tage låsen.
        security.AddAccessRule(new MutexAccessRule(Sid(WellKnownSidType.BuiltinAdministratorsSid),
            MutexRights.Synchronize, AccessControlType.Allow));
        return security;
    }

    private static EventWaitHandleSecurity SignalSecurity()
    {
        var security = new EventWaitHandleSecurity();
        security.AddAccessRule(new EventWaitHandleAccessRule(CurrentUser(),
            EventWaitHandleRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new EventWaitHandleAccessRule(Sid(WellKnownSidType.LocalSystemSid),
            EventWaitHandleRights.FullControl, AccessControlType.Allow));

        // Nok til at sende beskeden. Ikke nok til at ændre signalets rettigheder.
        security.AddAccessRule(new EventWaitHandleAccessRule(Sid(WellKnownSidType.BuiltinAdministratorsSid),
            EventWaitHandleRights.Modify | EventWaitHandleRights.Synchronize, AccessControlType.Allow));
        return security;
    }

    private static SecurityIdentifier CurrentUser()
        => WindowsIdentity.GetCurrent().User ?? Sid(WellKnownSidType.LocalSystemSid);

    private static SecurityIdentifier Sid(WellKnownSidType type) => new(type, null);

    public void Dispose()
    {
        ShowPanel.Dispose();
        Sample.Dispose();
        Quit.Dispose();
        Uninstall.Dispose();

        if (_instanceLock is null) return;
        if (IsFirstInstance)
        {
            try { _instanceLock.ReleaseMutex(); } catch (ApplicationException) { }
        }
        _instanceLock.Dispose();
    }
}
