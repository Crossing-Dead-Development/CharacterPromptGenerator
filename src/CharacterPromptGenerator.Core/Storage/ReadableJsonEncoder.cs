using System.Text;
using System.Text.Encodings.Web;

namespace CharacterPromptGenerator.Core.Storage;

/// <summary>
/// 只跳脫 JSON 規範必須跳脫的字元。內建的 UnsafeRelaxedJsonEscaping 仍會把 Emoji 寫成 🕯，
/// 使用者打開詞庫檔時無法閱讀；這個編碼器讓中文與 Emoji 原樣寫入 UTF-8 檔案。
/// </summary>
public sealed class ReadableJsonEncoder : JavaScriptEncoder
{
    public static readonly ReadableJsonEncoder Instance = new();

    const int LineSeparator = 0x2028;
    const int ParagraphSeparator = 0x2029;

    public override int MaxOutputCharactersPerInputCharacter => 6;

    static bool Needs(int c) =>
        c < 0x20 || c == '"' || c == '\\' || c == LineSeparator || c == ParagraphSeparator || (c >= 0xD800 && c <= 0xDFFF);

    public override unsafe int FindFirstCharacterToEncode(char* text, int textLength)
    {
        for (var i = 0; i < textLength; i++)
        {
            var c = text[i];
            if (char.IsHighSurrogate(c) && i + 1 < textLength && char.IsLowSurrogate(text[i + 1]))
            {
                i++; // 完整的代理對（Emoji 等）不跳脫
                continue;
            }
            if (Needs(c)) return i;
        }
        return -1;
    }

    public override bool WillEncode(int unicodeScalar) => Needs(unicodeScalar);

    public override unsafe bool TryEncodeUnicodeScalar(int unicodeScalar, char* buffer, int bufferLength, out int numberOfCharactersWritten)
    {
        var output = new Span<char>(buffer, bufferLength);
        if (!Needs(unicodeScalar))
        {
            numberOfCharactersWritten = 0;
            return Rune.TryCreate(unicodeScalar, out var rune) && rune.TryEncodeToUtf16(output, out numberOfCharactersWritten);
        }

        var escaped = unicodeScalar switch
        {
            '"' => "\\\"",
            '\\' => "\\\\",
            '\n' => "\\n",
            '\r' => "\\r",
            '\t' => "\\t",
            _ => $"\\u{unicodeScalar:X4}",
        };
        if (escaped.Length > bufferLength)
        {
            numberOfCharactersWritten = 0;
            return false;
        }
        escaped.AsSpan().CopyTo(output);
        numberOfCharactersWritten = escaped.Length;
        return true;
    }
}
