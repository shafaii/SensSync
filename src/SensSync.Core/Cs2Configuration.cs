using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SensSync.Core;

public sealed record Cs2Settings(double Sensitivity, double Yaw, double? Pitch, double? VerticalScale);
public sealed record Cs2Snapshot(string Path, string Sha256, Cs2Settings Settings, DateTimeOffset ReadAt);
public sealed record ConfigReadResult(Cs2Snapshot? Snapshot, string? Error)
{
    public bool Success => Snapshot is not null;
}

public static class Cs2Configuration
{
    public const int MaximumBytes = 2 * 1024 * 1024;

    public static Cs2Settings Parse(string text)
    {
        var roots = KeyValues.Parse(text);
        if (roots.Count != 1 || !roots[0].Key.Equals("config", StringComparison.OrdinalIgnoreCase))
            throw new FormatException("Expected a CS2 config root.");
        var config = roots[0].Children ?? throw new FormatException("Expected a config object.");
        var values = KeyValues.Single(config, "convars")?.Children
            ?? throw new FormatException("The convars object is missing.");
        double sensitivity = ReadNumber(values, "sensitivity", true)!.Value;
        double yaw = ReadNumber(values, "m_yaw", true)!.Value;
        if (sensitivity <= 0 || yaw <= 0) throw new FormatException("Sensitivity and yaw must be greater than zero.");
        return new(sensitivity, yaw, ReadNumber(values, "m_pitch", false), ReadNumber(values, "sensitivity_y_scale", false));
    }

    public static ConfigReadResult Read(string path)
    {
        try
        {
            byte[] bytes = ReadBoundedBytes(path);
            return new(new(Path.GetFullPath(path), Convert.ToHexString(SHA256.HashData(bytes)), Parse(Decode(bytes)), DateTimeOffset.UtcNow), null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException or ArgumentException or NotSupportedException)
        {
            return new(null, e is FileNotFoundException or DirectoryNotFoundException
                ? "Configuration not found. Launch CS2 once, or choose an existing configuration."
                : $"Cannot read saved CS2 settings: {e.Message}");
        }
    }

    public static byte[] ReadBoundedBytes(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length > MaximumBytes) throw new FormatException("Configuration is larger than 2 MiB.");
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = stream.Read(chunk)) != 0)
        {
            if (buffer.Length + read > MaximumBytes) throw new FormatException("Configuration is larger than 2 MiB.");
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    public static string Decode(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE, 0x00, 0x00 }) ||
            bytes.AsSpan().StartsWith(new byte[] { 0x00, 0x00, 0xFE, 0xFF }))
            throw new FormatException("UTF-32 configurations are not supported.");
        if (bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE })) return new UnicodeEncoding(false, true, true).GetString(bytes, 2, bytes.Length - 2);
        if (bytes.AsSpan().StartsWith(new byte[] { 0xFE, 0xFF })) return new UnicodeEncoding(true, true, true).GetString(bytes, 2, bytes.Length - 2);
        int offset = bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }) ? 3 : 0;
        return new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
    }

    private static double? ReadNumber(IReadOnlyList<KeyValueEntry> values, string key, bool required)
    {
        var entry = KeyValues.Single(values, key);
        if (entry is null && !required) return null;
        if (entry is null) throw new FormatException($"Required setting '{key}' is missing; no default has been assumed.");
        if (!double.TryParse(entry.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value))
            throw new FormatException($"Setting '{key}' is not a finite number.");
        return value;
    }
}
