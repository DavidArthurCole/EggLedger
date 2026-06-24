using System.Text.RegularExpressions;

namespace EggLedger.Web.State;

/// <summary>Screenshot-safety toggle: when on, masks any EI+16-digits so EIDs do not leak into screenshots. Scoped (one per app).</summary>
public sealed partial class ScreenshotSafetyState
{
    /// <summary>Whether EIDs are masked on screen.</summary>
    public bool Enabled
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }
            field = value;
            Changed?.Invoke();
        }
    }

    /// <summary>Raised when the toggle changes so subscribers re-render.</summary>
    public event Action? Changed;

    /// <summary>Returns <paramref name="text"/> with every EID masked when enabled, otherwise unchanged.</summary>
    public string Mask(string? text)
    {
        if (string.IsNullOrEmpty(text) || !Enabled)
        {
            return text ?? "";
        }
        return EidRegex().Replace(text, "EI[eid-bar]");
    }

    [GeneratedRegex(@"EI\d{16}")]
    private static partial Regex EidRegex();
}
