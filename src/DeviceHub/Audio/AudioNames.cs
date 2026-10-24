using System.Text.RegularExpressions;
using static DeviceHub.Core.I18n;

namespace DeviceHub.Audio;

/// <summary>
/// Windows names endpoints like "Headphones (4- Arctis 7+)". This turns them into the names of the actual devices
/// on this desk, with a line saying what each one is. Unknown devices keep Windows' own name.
/// </summary>
internal static partial class AudioNames
{
    public sealed record Look(string Name, string? Sub, string Kind, bool HandsFree);

    /// <summary>
    /// Devices you switch on or plug in: these keep a "not connected" tile while they are away.
    /// An empty monitor port or the laptop's bare headphone jack does not.
    /// </summary>
    public static readonly string[] ShownWhenAway = ["headset", "speakers", "controller"];

    /// <summary>The order on screen.</summary>
    public static readonly string[] Order = ["headset", "speakers", "monitor", "controller", "laptop", "earbuds", "other"];

    /// <param name="desc">The endpoint's own name: "Speakers", "Headphones", or a monitor's model.</param>
    /// <param name="adapter">The device it belongs to: "4- Arctis 7+", "NVIDIA High Definition Audio".</param>
    public static Look Describe(string desc, string adapter, bool output)
    {
        var a = PortNumber().Replace(adapter, "").Trim();
        var handsFree = a.Contains("Hands-Free", StringComparison.OrdinalIgnoreCase);

        if (a.Contains("Arctis", StringComparison.OrdinalIgnoreCase))
            return new(a, output ? L("אוזניות SteelSeries", "SteelSeries headset") : L("המיקרופון של האוזניות", "Headset mic"), "headset", false);

        // The Logitech speakers are analog, plugged into a UGREEN-90748 Bluetooth receiver. It has no microphone:
        // its recording endpoint (and the "Hands-Free" output) is Bluetooth's phone-call profile. Using it turns
        // the speakers into a phone line, so it is hidden unless Windows already made it a default, and then named for what it is.
        if (a.Contains("UGREEN-90748", StringComparison.OrdinalIgnoreCase))
            return output && !handsFree
                ? new(L("רמקולי Logitech", "Logitech speakers"), L("דרך UGREEN-90748", "via UGREEN-90748"), "speakers", false)
                : new(L("UGREEN לשיחות", "UGREEN calls"), L("מוריד את הרמקולים לאיכות טלפון", "Drops the speakers to phone quality"), "speakers", true);

        if (a.Contains("DualSense", StringComparison.OrdinalIgnoreCase))
            return new(a.Replace("Wireless Controller", "", StringComparison.OrdinalIgnoreCase).Trim(),
                output ? L("רמקול ושקע אוזניות", "Speaker and jack") : L("המיקרופון של השלט", "Controller mic"), "controller", false);

        if (DisplayAudio().IsMatch(a))
            return new(desc.Length > 0 ? desc : a, L("מסך", "Monitor"), "monitor", false);

        if (BuiltInAudio().IsMatch(a))
        {
            var name = !output ? L("מיקרופון הלפטופ", "Laptop mic")
                : desc.Contains("Headphone", StringComparison.OrdinalIgnoreCase) ? L("שקע האוזניות של הלפטופ", "Laptop headphone jack")
                : L("רמקולי הלפטופ", "Laptop speakers");
            return new(name, L("מובנה בלפטופ", "Built in"), "laptop", false);
        }

        var clean = HandsFreeSuffix().Replace(a, "").Trim();
        var kind = Earbuds().IsMatch(clean) || Earbuds().IsMatch(desc) ? "earbuds" : "other";
        var sub = handsFree ? L("פרופיל שיחה, איכות נמוכה", "Call profile, low quality") : desc.Length > 0 && clean.Length > 0 ? desc : null;
        return new(clean.Length > 0 ? clean : desc, sub, kind, handsFree);
    }

    [GeneratedRegex(@"^\d+\s*-\s*")] private static partial Regex PortNumber();
    [GeneratedRegex(@"NVIDIA High Definition Audio|Intel\(R\) Display Audio|AMD High Definition Audio", RegexOptions.IgnoreCase)] private static partial Regex DisplayAudio();
    [GeneratedRegex(@"^(High Definition Audio Device|Realtek.*|Conexant.*|Senary.*)$", RegexOptions.IgnoreCase)] private static partial Regex BuiltInAudio();
    [GeneratedRegex(@"\s*Hands-Free( AG)?\s*$", RegexOptions.IgnoreCase)] private static partial Regex HandsFreeSuffix();
    [GeneratedRegex(@"buds|airpods|headset|headphone|earphone", RegexOptions.IgnoreCase)] private static partial Regex Earbuds();
}
