using System.Globalization;
using System.Text;

namespace CharacterPromptGenerator.Core.Emoji;

/// <summary>一段文字中的片段：一般文字，或一個 Emoji（完整的字素叢集，例如 ZWJ 組合、膚色、國旗）。</summary>
public readonly record struct TextSegment(string Text, bool IsEmoji);

/// <summary>
/// Emoji 的切分與正規化。資料中永遠保存原始的 Unicode 字串；這裡只負責「顯示時要找哪張圖」。
/// </summary>
public static class EmojiText
{
    const int VariationSelectorText = 0xFE0E;
    const int VariationSelectorEmoji = 0xFE0F;
    const int ZeroWidthJoiner = 0x200D;
    const int CombiningKeycap = 0x20E3;
    const int SkinToneFirst = 0x1F3FB;
    const int SkinToneLast = 0x1F3FF;

    /// <summary>BMP 中預設以 Emoji 樣式顯示的字元（Unicode 16.0 emoji-data.txt 的 Emoji_Presentation）。</summary>
    static readonly (int From, int To)[] BmpEmojiPresentation =
    [
        (0x231A, 0x231B), (0x23E9, 0x23EC), (0x23F0, 0x23F0), (0x23F3, 0x23F3), (0x25FD, 0x25FE), (0x2614, 0x2615),
        (0x2648, 0x2653), (0x267F, 0x267F), (0x2693, 0x2693), (0x26A1, 0x26A1), (0x26AA, 0x26AB), (0x26BD, 0x26BE),
        (0x26C4, 0x26C5), (0x26CE, 0x26CE), (0x26D4, 0x26D4), (0x26EA, 0x26EA), (0x26F2, 0x26F3), (0x26F5, 0x26F5),
        (0x26FA, 0x26FA), (0x26FD, 0x26FD), (0x2705, 0x2705), (0x270A, 0x270B), (0x2728, 0x2728), (0x274C, 0x274C),
        (0x274E, 0x274E), (0x2753, 0x2755), (0x2757, 0x2757), (0x2795, 0x2797), (0x27B0, 0x27B0), (0x27BF, 0x27BF),
        (0x2B1B, 0x2B1C), (0x2B50, 0x2B50), (0x2B55, 0x2B55),
    ];

    public static List<int> CodePoints(string s)
    {
        var list = new List<int>(s.Length);
        foreach (var rune in s.EnumerateRunes()) list.Add(rune.Value);
        return list;
    }

    /// <summary>圖庫檔名用的鍵：小寫十六進位碼點以 - 連接，去掉 FE0E/FE0F（各圖庫對變體選擇符的處理不一致）。</summary>
    public static string Key(string cluster) =>
        string.Join('-', CodePoints(cluster)
            .Where(cp => cp is not (VariationSelectorText or VariationSelectorEmoji))
            .Select(cp => cp.ToString("x")));

    /// <summary>依序嘗試的鍵：完整序列 → 去掉膚色（找不到該膚色時至少顯示預設膚色）。</summary>
    public static IEnumerable<string> CandidateKeys(string cluster)
    {
        var key = Key(cluster);
        if (key.Length == 0) yield break;
        yield return key;
        var cps = CodePoints(cluster);
        if (cps.Any(IsSkinTone))
        {
            var noTone = string.Join('-', cps
                .Where(cp => !IsSkinTone(cp) && cp is not (VariationSelectorText or VariationSelectorEmoji))
                .Select(cp => cp.ToString("x")));
            if (noTone.Length > 0 && noTone != key) yield return noTone;
        }
    }

    static bool IsSkinTone(int cp) => cp is >= SkinToneFirst and <= SkinToneLast;

    /// <summary>
    /// 是否「一定是 Emoji」：用來決定找不到圖片時要顯示替代圖示，還是當成一般文字。
    /// 一般符號（如 ©、☂ 沒有 FE0F）不算，避免一般文字中的符號被換成圖片。
    /// </summary>
    public static bool IsDefinitelyEmoji(string cluster)
    {
        var cps = CodePoints(cluster);
        if (cps.Count == 0) return false;
        if (cps.Contains(VariationSelectorText)) return false; // 明確要求文字樣式
        if (cps.Contains(VariationSelectorEmoji) || cps.Contains(CombiningKeycap)) return true;
        if (cps.Contains(ZeroWidthJoiner) && cps.Any(cp => cp >= 0x2000)) return true;
        var first = cps[0];
        if (first >= 0x1F000 && first <= 0x1FAFF) return true; // 主要 Emoji 區塊（含國旗的區域指示符號）
        if (first is >= 0xE0020 and <= 0xE007F) return true;   // 旗幟標籤序列
        foreach (var (from, to) in BmpEmojiPresentation)
            if (first >= from && first <= to) return true;
        return false;
    }

    /// <summary>把文字切成「一般文字」與「Emoji」片段，連續的一般文字會合併。</summary>
    public static List<TextSegment> Split(string? text)
    {
        var result = new List<TextSegment>();
        if (string.IsNullOrEmpty(text)) return result;
        var plain = new StringBuilder();
        var e = StringInfo.GetTextElementEnumerator(text);
        while (e.MoveNext())
        {
            var cluster = e.GetTextElement();
            if (IsDefinitelyEmoji(cluster))
            {
                if (plain.Length > 0) { result.Add(new TextSegment(plain.ToString(), false)); plain.Clear(); }
                result.Add(new TextSegment(cluster, true));
            }
            else
            {
                plain.Append(cluster);
            }
        }
        if (plain.Length > 0) result.Add(new TextSegment(plain.ToString(), false));
        return result;
    }

    /// <summary>切成字素叢集（Emoji 欄位逐個處理用）。</summary>
    public static List<string> Clusters(string? text)
    {
        var list = new List<string>();
        if (string.IsNullOrEmpty(text)) return list;
        var e = StringInfo.GetTextElementEnumerator(text);
        while (e.MoveNext()) list.Add(e.GetTextElement());
        return list;
    }

    /// <summary>
    /// 把 ZWJ 序列拆成組成元素（例如 👨‍👩‍👧 → 👨、👩、👧）。
    /// 圖庫沒有整個組合時，依 Unicode UTS #51 的建議改為並排顯示各元素。不是 ZWJ 序列時回傳空清單。
    /// </summary>
    public static List<string> ZwjComponents(string cluster)
    {
        var result = new List<string>();
        var cps = CodePoints(cluster);
        if (!cps.Contains(ZeroWidthJoiner)) return result;
        var current = new StringBuilder();
        foreach (var cp in cps)
        {
            if (cp == ZeroWidthJoiner)
            {
                if (current.Length > 0) result.Add(current.ToString());
                current.Clear();
            }
            else current.Append(char.ConvertFromUtf32(cp));
        }
        if (current.Length > 0) result.Add(current.ToString());
        return result;
    }

    /// <summary>「U+1F56F U+FE0F」格式，給提示訊息用。</summary>
    public static string Describe(string cluster) =>
        string.Join(' ', CodePoints(cluster).Select(cp => $"U+{cp:X4}"));
}
