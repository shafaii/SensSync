namespace SensSync.Core;

/// <summary>Output-only horizontal hipfire models. No game configuration access.</summary>
public sealed record ConversionTarget(string Name, double Slope, double Offset, double Minimum, double Maximum, string Notes)
{
    public static ConversionTarget Valorant { get; } = new("Valorant", LinearSensitivityModel.ValorantYawCandidate, 0, 0, double.PositiveInfinity,
        "Candidate yaw 0.07 retained from M1. Physical in-game validation remains outstanding.");
    public static ConversionTarget Cs2 { get; } = new("Counter-Strike 2", LinearSensitivityModel.DefaultCs2Yaw, 0, 0, double.PositiveInfinity,
        "Assumes target CS2 m_yaw = 0.022. A custom source yaw is preserved in cm/360, not assumed for the target.");
    public static ConversionTarget MarvelRivals { get; } = new("Marvel Rivals", 0.0175, 0, 0, double.PositiveInfinity,
        "Horizontal mouse sensitivity; yaw 0.0175 from KovaaK's converter. Validate with a measured in-game turn. No settings are read or applied.");
    public static ConversionTarget Battlefield6 { get; } = new("Battlefield 6 (experimental)", 0.0017362342283, 0.002314981532, 0, 100,
        "Experimental infantry hipfire menu value (0-100), not a config-file value. Uses KovaaK's affine model; live-build physical validation is outstanding. ADS and vehicles are excluded. The game's slider may round the result.");
    public static IReadOnlyList<ConversionTarget> All { get; } = Array.AsReadOnly(new[] { Valorant, Cs2, MarvelRivals, Battlefield6 });

    public double FromCmPer360(double cm, double dpi)
    {
        LinearSensitivityModel.RequirePositive(cm, nameof(cm));
        LinearSensitivityModel.RequirePositive(dpi, nameof(dpi));
        double value = Offset == 0
            ? new LinearSensitivityModel(Slope).FromCmPer360(cm, dpi)
            : (914.4 / dpi / cm - Offset) / Slope;
        if (!double.IsFinite(value) || value < Minimum || value > Maximum || (Offset == 0 && value <= 0))
            throw new ArgumentException($"{Name}: this match is outside the supported sensitivity range" +
                (double.IsFinite(Maximum) ? $" ({Minimum:0}-{Maximum:0})." : "."));
        return value;
    }

    public override string ToString() => Name;
}
