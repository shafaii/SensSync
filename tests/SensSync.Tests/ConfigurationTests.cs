using System.Security.Cryptography;
using System.Text;
using SensSync.Core;

namespace SensSync.Tests;

public sealed class ConfigurationTests
{
    [Fact]
    public void ReadsOnlyExactMouseSettingsWithinConvars()
    {
        var settings = Cs2Configuration.Parse(TestFiles.Standard);
        Assert.Equal(1, settings.Sensitivity);
        Assert.Equal(0.022, settings.Yaw);
        Assert.Equal(0.022, settings.Pitch);
        Assert.Equal(1, settings.VerticalScale);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void AcceptsLineEndingsWhitespaceAndScientificNumbers(string ending)
    {
        string text = TestFiles.Standard.Replace("\r\n", "\n").Replace("\n", ending)
            .Replace("\"m_yaw\" \"0.022\"", "\"m_yaw\"\t\"4.4e-2\"");
        Assert.Equal(0.044, Cs2Configuration.Parse(text).Yaw);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\"other\" {}")]
    [InlineData("\"config\" {}")]
    [InlineData("\"config\" { \"convars\" { \"sensitivity\" \"1\" } }")]
    [InlineData("\"config\" { \"convars\" { \"m_yaw\" \"0.022\" } }")]
    [InlineData("\"config\" { \"convars\" { \"sensitivity\" \"1\" \"m_yaw\" \"0.022\" }")]
    [InlineData("\"config\" { \"convars\" { \"sensitivity\" \"1\" \"m_yaw\" \"0.022\" } } trailing")]
    public void MissingMalformedOrUnknownConfigurationFails(string text)
        => Assert.Throws<FormatException>(() => Cs2Configuration.Parse(text));

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-0.1")]
    [InlineData("0")]
    [InlineData("nonsense")]
    [InlineData("1,5")]
    public void InvalidFileNumberFails(string number)
        => Assert.Throws<FormatException>(() => Cs2Configuration.Parse(TestFiles.Standard.Replace("\"1.0\"", $"\"{number}\"")));

    [Fact]
    public void DuplicateRelevantKeysAreAmbiguousEvenWhenValuesAgree()
    {
        var text = TestFiles.Standard.Replace("\"sensitivity\" \"1.0\"", "\"sensitivity\" \"1.0\" \"sensitivity\" \"1.0\"");
        Assert.Throws<FormatException>(() => Cs2Configuration.Parse(text));
    }

    [Fact]
    public void DoesNotTakeNestedOrSimilarlyNamedValuesAsMissingSensitivity()
    {
        var text = TestFiles.Standard.Replace("\"sensitivity\" \"1.0\"", "\"nested\" { \"sensitivity\" \"1.0\" }");
        Assert.Throws<FormatException>(() => Cs2Configuration.Parse(text));
    }

    [Theory]
    [InlineData("utf8")]
    [InlineData("utf8bom")]
    [InlineData("utf16le")]
    [InlineData("utf16be")]
    public void ReadsSupportedEncodingsWithoutChangingOriginalBytes(string kind)
    {
        using var files = new TestFiles();
        Encoding encoding = kind switch
        {
            "utf8bom" => new UTF8Encoding(true), "utf16le" => new UnicodeEncoding(false, true),
            "utf16be" => new UnicodeEncoding(true, true), _ => new UTF8Encoding(false)
        };
        string path = files.Write("配置.vcfg", TestFiles.Standard + "\n// café", encoding);
        byte[] before = File.ReadAllBytes(path);
        var read = Cs2Configuration.Read(path);
        Assert.True(read.Success, read.Error);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(before)), read.Snapshot!.Sha256);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void MissingUnreadableAndOversizedFilesReturnAnError()
    {
        using var files = new TestFiles();
        Assert.False(Cs2Configuration.Read(Path.Combine(files.Root, "missing.vcfg")).Success);
        string path = files.Write("oversized.vcfg", new string(' ', Cs2Configuration.MaximumBytes + 1));
        Assert.False(Cs2Configuration.Read(path).Success);
        path = files.Write("locked.vcfg", TestFiles.Standard);
        using var locked = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
        Assert.False(Cs2Configuration.Read(path).Success);
    }

    [Fact]
    public void InvalidEncodingIsReported()
    {
        using var files = new TestFiles();
        string path = files.Write("invalid.vcfg", "");
        File.WriteAllBytes(path, [0xFF, 0xFF, 0xFF]);
        Assert.False(Cs2Configuration.Read(path).Success);
    }
}
