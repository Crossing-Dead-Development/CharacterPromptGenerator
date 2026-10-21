using System.Text;

namespace CharacterPromptGenerator.Core.Import;

/// <summary>讀取匯入用的文字檔：辨識 BOM、嚴格 UTF-8 解碼，失敗時給出可讀的錯誤，而不是產生亂碼資料。</summary>
public static class TextFileLoader
{
    const char ReplacementChar = (char)0xFFFD;
    public const long MaxBytes = 50L * 1024 * 1024;

    public static (string? Text, string? Error) Read(string path)
    {
        byte[] bytes;
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists) return (null, "找不到檔案。");
            if (info.Length > MaxBytes) return (null, $"檔案太大（{info.Length / 1024 / 1024} MB），上限為 50 MB。");
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception ex)
        {
            return (null, $"無法讀取檔案：{ex.Message}");
        }
        return Decode(bytes);
    }

    public static (string? Text, string? Error) Decode(byte[] bytes)
    {
        if (bytes.Length == 0) return (null, "檔案是空的。");

        string text;
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            if (!TryStrictUtf8(bytes.AsSpan(3), out text)) return (null, EncodingError);
        }
        else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            text = Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        }
        else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            text = Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }
        else
        {
            if (Array.IndexOf(bytes, (byte)0) >= 0)
                return (null, "檔案含有二進位內容，看起來不是文字檔（若是 Excel 檔，請先另存為 CSV UTF-8）。");
            if (!TryStrictUtf8(bytes, out text)) return (null, EncodingError);
        }

        if (string.IsNullOrWhiteSpace(text)) return (null, "檔案沒有任何內容。");
        if (text.Contains(ReplacementChar))
            return (null, "檔案中含有無法辨識的字元（" + ReplacementChar + "），可能已經是亂碼。請確認來源檔案並以 UTF-8 重新存檔。");
        return (text, null);
    }

    const string EncodingError =
        "檔案不是 UTF-8 編碼（可能是 Big5 或 ANSI），直接匯入會變成亂碼。\n" +
        "請用記事本「另存新檔 → 編碼選 UTF-8」，或在 Excel 選「CSV UTF-8（逗號分隔）」後再匯入。";

    static bool TryStrictUtf8(ReadOnlySpan<byte> bytes, out string text)
    {
        try
        {
            text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            text = "";
            return false;
        }
    }
}
