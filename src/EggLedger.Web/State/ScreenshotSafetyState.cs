using System.Text.RegularExpressions;

namespace EggLedger.Web.State;

/// <summary>
/// Holds the "mask player IDs on screen" (screenshot-safety) toggle and applies
/// it to displayed text. C# port of the Vue <c>screenshotSafety</c> ref +
/// <c>maskEid</c>: when on, any <c>EI</c> followed by 16 digits is replaced with
/// a placeholder so EIDs do not leak into screenshots. Scoped (one per app); the
/// shell loads the persisted value at startup and updates it from Settings.
/// </summary>
public sealed partial class ScreenshotSafetyState
{
    private bool _enabled;

    /// <summary>Whether EIDs are masked on screen.</summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
            {
                return;
            }
            _enabled = value;
            Changed?.Invoke();
        }
    }

    /// <summary>Raised when the toggle changes so subscribers re-render.</summary>
    public event Action? Changed;

    /// <summary>
    /// Returns <paramref name="text"/> with every EID masked when enabled,
    /// otherwise unchanged. Mirrors the Vue regex <c>/EI\d{16}/g</c>.
    /// </summary>
    public string Mask(string? text)
    {
        if (string.IsNullOrEmpty(text) || !_enabled)
        {
            return text ?? "";
        }
        return EidRegex().Replace(text, "EI[eid-bar]");
    }

    [GeneratedRegex(@"EI\d{16}")]
    private static partial Regex EidRegex();
}
