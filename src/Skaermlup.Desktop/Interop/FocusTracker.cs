using System.Runtime.InteropServices;
using System.Windows.Automation;
using Skaermlup.Core.Geometry;

namespace Skaermlup.Desktop.Interop;

/// <summary>
/// Holder øje med hvilket felt tastaturet står i — i alle programmer — så luppen kan følge med,
/// når brugeren flytter rundt med Tab. Det sker gennem UI Automation, som Windows' egen
/// Forstørrelse og Oplæser også bruger.
///
/// Kun feltets placering læses. Aldrig navnet, aldrig indholdet, aldrig hvad der står i det —
/// heller ikke i et adgangskodefelt. Og kun mens luppen er tændt: slukket lytter programmet
/// slet ikke efter, hvor tastaturet er. Se D11 i docs\dataflow-og-compliance.md.
/// </summary>
internal sealed class FocusTracker : IDisposable
{
    private readonly object _gate = new();
    private readonly AutomationFocusChangedEventHandler _handler;
    private Task _queue = Task.CompletedTask;
    private PixelRect? _current;
    private bool _running;

    public FocusTracker() => _handler = OnFocusChanged;

    /// <summary>Feltet tastaturet står i lige nu, eller null.</summary>
    public PixelRect? Current
    {
        get { lock (_gate) return _current; }
    }

    public void Start()
    {
        lock (_gate)
        {
            if (_running) return;
            _running = true;
            _current = null;
        }

        // Aldrig fra programmets hovedtråd: svarer et andet program langsomt, ville luppen hænge
        // med. Til- og afmelding sker i rækkefølge i en kø, så et hurtigt tænd-sluk ikke kan
        // efterlade lytteren tilmeldt.
        Enqueue(() => Automation.AddAutomationFocusChangedEventHandler(_handler));
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (!_running) return;
            _running = false;
            _current = null;
        }

        Enqueue(() => Automation.RemoveAutomationFocusChangedEventHandler(_handler));
    }

    private void Enqueue(Action action)
    {
        lock (_gate)
        {
            _queue = _queue.ContinueWith(_ =>
            {
                try { action(); }
                catch (Exception ex) when (ex is COMException or InvalidOperationException or ArgumentException)
                {
                    // UI Automation er ikke til rådighed. Så følges tastaturet bare ikke —
                    // skrivemarkøren og musen virker som før.
                }
            }, TaskScheduler.Default);
        }
    }

    private void OnFocusChanged(object sender, AutomationFocusChangedEventArgs e)
    {
        // Det element, begivenheden kommer fra, er ikke altid det, der ender med fokus. Windows 11's
        // Notesblok melder beholderen — fanelinjen, menulinjen — og flytter så fokus videre ind på
        // knappen uden at melde det. Målt: én besked om beholderen efter ca. 20 ms, og så intet.
        // Derfor spørges Windows, hvad der har fokus nu — og en gang til lidt efter.
        MeasureFocusedElement();
        _ = Task.Delay(150).ContinueWith(_ => MeasureFocusedElement(), TaskScheduler.Default);
    }

    private void MeasureFocusedElement()
    {
        lock (_gate)
        {
            if (!_running) return;
        }

        try
        {
            if (AutomationElement.FocusedElement is not { } element) return;

            // Kun hvor feltet er. Ikke hvad det hedder, og ikke hvad der står i det.
            var bounds = element.Current.BoundingRectangle;
            if (bounds.IsEmpty || !double.IsFinite(bounds.Width) || !double.IsFinite(bounds.Height)) return;

            var rect = new PixelRect(
                (int)Math.Round(bounds.X), (int)Math.Round(bounds.Y),
                (int)Math.Round(bounds.Width), (int)Math.Round(bounds.Height));

            lock (_gate)
            {
                if (_running) _current = rect;
            }
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or COMException or InvalidOperationException)
        {
            // Feltet forsvandt, før det kunne måles. Det næste fokusskift kommer.
        }
    }

    public void Dispose()
    {
        Stop();

        Task pending;
        lock (_gate) pending = _queue;

        // Afmeldingen skal nå at ske, før programmet lukker. Ellers kan Windows kalde ind i et
        // program, der er på vej ud.
        pending.Wait(TimeSpan.FromSeconds(2));
    }
}
