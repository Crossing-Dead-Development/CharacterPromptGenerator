using System.Text;
using CharacterPromptGenerator.Core.Localization;

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
            if (!info.Exists) return (null, L.T("File_NotFound"));
            if (info.Length > MaxBytes) return (null, L.F("File_TooLarge", info.Length / 1024 / 1024));
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception ex)
        {
            return (null, L.F("File_Unreadable", ex.Message));
        }
        return Decode(bytes);
    }

    public static (string? Text, string? Error) Decode(byte[] bytes)
    {
        if (bytes.Length == 0) return (null, L.T("File_Empty"));

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
                return (null, L.T("File_Binary"));
            if (!TryStrictUtf8(bytes, out text)) return (null, EncodingError);
        }

        if (string.IsNullOrWhiteSpace(text)) return (null, L.T("File_NoContent"));
        if (text.Contains(ReplacementChar))
            return (null, L.F("File_ReplacementChar", ReplacementChar));
        return (text, null);
    }

    static string EncodingError => L.T("File_NotUtf8");

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
