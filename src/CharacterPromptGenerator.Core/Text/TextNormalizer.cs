using System.Text;

namespace CharacterPromptGenerator.Core.Text;

public static class TextNormalizer
{
    /// <summary>去除首尾空白並把連續空白壓成單一半形空白；不動 Emoji 序列。</summary>
    public static string Clean(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var sb = new StringBuilder(text.Length);
        bool pendingSpace = false;
        foreach (var ch in text.Trim())
        {
            // 只把真正的空白（含全形空白、換行）視為空白；ZWJ、變體選擇符等不是 WhiteSpace，會被保留
            if (char.IsWhiteSpace(ch))
            {
                pendingSpace = true;
                continue;
            }
            if (pendingSpace && sb.Length > 0) sb.Append(' ');
            pendingSpace = false;
            sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>重複判斷用的比對鍵：Clean + NFC + 不分大小寫。</summary>
    public static string Key(string? text)
    {
        var cleaned = Clean(text);
        if (cleaned.Length == 0) return "";
        return cleaned.Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    public static string? CleanOptional(string? text)
    {
        var c = Clean(text);
        return c.Length == 0 ? null : c;
    }

    /// <summary>把 "a|b | c" 或清單整理成去重的標籤。</summary>
    public static List<string> ParseTags(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
        return CleanTags(raw.Split('|'));
    }

    public static List<string> CleanTags(IEnumerable<string?> tags)
    {
        var result = new List<string>();
        var seen = new HashSet<string>();
        foreach (var t in tags)
        {
            var c = Clean(t);
            if (c.Length > 0 && seen.Add(Key(c))) result.Add(c);
        }
        return result;
    }

    public static string JoinTags(IEnumerable<string> tags) => string.Join("|", tags);
}
