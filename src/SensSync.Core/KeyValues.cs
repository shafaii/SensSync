using System.Text;

namespace SensSync.Core;

public sealed record KeyValueEntry(string Key, string? Value, IReadOnlyList<KeyValueEntry>? Children);

/// <summary>A bounded reader for Valve text KeyValues. Keeps duplicate keys so callers can reject ambiguity.</summary>
public static class KeyValues
{
    public static IReadOnlyList<KeyValueEntry> Parse(string text)
    {
        if (text.Length > 2 * 1024 * 1024) throw new FormatException("Configuration is too large.");
        return new Parser(text).ReadEntries(false, 0);
    }

    public static KeyValueEntry? Single(IReadOnlyList<KeyValueEntry> entries, string key)
    {
        var matches = entries.Where(x => x.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matches.Length > 1) throw new FormatException($"The setting '{key}' appears more than once.");
        return matches.SingleOrDefault();
    }

    private sealed class Parser(string text)
    {
        private int position;
        private int tokens;

        public IReadOnlyList<KeyValueEntry> ReadEntries(bool nested, int depth)
        {
            if (depth > 32) throw new FormatException("Configuration nesting is too deep.");
            var entries = new List<KeyValueEntry>();
            while (true)
            {
                SkipSpace();
                if (position == text.Length)
                {
                    if (nested) throw new FormatException("Configuration ends before a closing brace.");
                    return entries;
                }
                if (text[position] == '}')
                {
                    if (!nested) throw new FormatException("Unexpected closing brace.");
                    position++;
                    return entries;
                }
                string key = ReadToken();
                SkipSpace();
                if (position < text.Length && text[position] == '{')
                {
                    position++;
                    entries.Add(new(key, null, ReadEntries(true, depth + 1)));
                }
                else entries.Add(new(key, ReadToken(), null));
            }
        }

        private string ReadToken()
        {
            SkipSpace();
            if (++tokens > 100_000) throw new FormatException("Configuration contains too many tokens.");
            if (position >= text.Length || text[position] is '{' or '}')
                throw new FormatException("Expected a key or value.");
            if (text[position] != '"')
            {
                int start = position;
                while (position < text.Length && !char.IsWhiteSpace(text[position]) && text[position] is not ('{' or '}'))
                    position++;
                return text[start..position];
            }
            position++;
            var value = new StringBuilder();
            while (position < text.Length)
            {
                char c = text[position++];
                if (c == '"') return value.ToString();
                if (c == '\\' && position < text.Length && text[position] is '\\' or '"') c = text[position++];
                value.Append(c);
            }
            throw new FormatException("Unterminated quoted value.");
        }

        private void SkipSpace()
        {
            while (position < text.Length)
            {
                if (char.IsWhiteSpace(text[position]) || text[position] == '\uFEFF') { position++; continue; }
                if (text[position] == '/' && position + 1 < text.Length && text[position + 1] == '/')
                {
                    while (position < text.Length && text[position] != '\n') position++;
                    continue;
                }
                break;
            }
        }
    }
}
