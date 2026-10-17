namespace SensSync.Core;

/// <summary>Horizontal hipfire only. Yaw is degrees per mouse count at setting 1.</summary>
public sealed class LinearSensitivityModel
{
    public const double DefaultCs2Yaw = 0.022;
    public const double ValorantYawCandidate = 0.07;
    public double Yaw { get; }

    public LinearSensitivityModel(double yaw)
    {
        RequirePositive(yaw, nameof(yaw));
        Yaw = yaw;
    }

    public double ToCmPer360(double sensitivity, double dpi)
    {
        RequirePositive(sensitivity, nameof(sensitivity));
        RequirePositive(dpi, nameof(dpi));
        return ValidResult(914.4 / dpi / sensitivity / Yaw);
    }

    public double FromCmPer360(double cm, double dpi)
    {
        RequirePositive(cm, nameof(cm));
        RequirePositive(dpi, nameof(dpi));
        return ValidResult(914.4 / dpi / cm / Yaw);
    }

    public double ConvertTo(double sensitivity, double sourceDpi, LinearSensitivityModel target, double targetDpi)
    {
        ArgumentNullException.ThrowIfNull(target);
        return target.FromCmPer360(ToCmPer360(sensitivity, sourceDpi), targetDpi);
    }

    public static void RequirePositive(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0)
            throw new ArgumentOutOfRangeException(name, "Enter a finite number greater than zero.");
    }

    private static double ValidResult(double value)
    {
        if (!double.IsFinite(value) || value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), "These values exceed the supported numeric range.");
        return value;
    }
}
