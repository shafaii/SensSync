using System.Text;

namespace SensSync.Tests;

internal sealed class TestFiles : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "SensSync.Tests", Guid.NewGuid().ToString("N"));
    public static string Standard => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "standard.vcfg"));

    public string Write(string relative, string text, Encoding? encoding = null)
    {
        string path = Path.GetFullPath(Path.Combine(Root, relative));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text, encoding ?? new UTF8Encoding(false));
        return path;
    }

    public void Dispose()
    {
        string expected = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "SensSync.Tests")) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(Root).StartsWith(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unsafe test cleanup path.");
        if (Directory.Exists(Root)) Directory.Delete(Root, true);
    }
}
