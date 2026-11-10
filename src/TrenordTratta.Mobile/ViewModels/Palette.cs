namespace TrenordTratta.Mobile.ViewModels;

/// <summary>Colori semantici usati dai ViewModel (ritardo e affollamento).</summary>
public static class Palette
{
    public static readonly Color OnTime = Color.FromArgb("#1E8E3E");
    public static readonly Color Late = Color.FromArgb("#D93025");
    public static readonly Color Unknown = Color.FromArgb("#8A8F8A");

    public static readonly Color CrowdLow = Color.FromArgb("#1E8E3E");
    public static readonly Color CrowdMid = Color.FromArgb("#F2A600");
    public static readonly Color CrowdHigh = Color.FromArgb("#D93025");

    public const int CrowdMidFrom = 40;
    public const int CrowdHighFrom = 70;

    public static Color Delay(int? minutes, bool cancelled, int threshold) =>
        cancelled ? Late
        : minutes is null ? Unknown
        : minutes <= threshold ? OnTime
        : Late;

    public static Color Crowding(int percent) =>
        percent >= CrowdHighFrom ? CrowdHigh
        : percent >= CrowdMidFrom ? CrowdMid
        : CrowdLow;

    public static string DelayText(int? minutes, bool cancelled) =>
        cancelled ? "Soppresso"
        : minutes is null ? "—"
        : minutes <= 0 ? "In orario"
        : $"+{minutes}'";
}
