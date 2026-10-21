using System.Text;
using CharacterPromptGenerator.Core.Localization;

namespace CharacterPromptGenerator.Core.Import;

public sealed record CsvRecord(int Line, List<string> Fields);

public sealed class CsvParseException(int line, string message) : Exception(message)
{
    public int Line { get; } = line;
}

/// <summary>
/// RFC 4180 CSV 解析：支援雙引號欄位、欄位內逗號／換行、"" 跳脫、CRLF/LF/CR 換行。
/// 回傳每筆紀錄及其起始的實體行號。
/// </summary>
public static class CsvParser
{
    public const char Bom = (char)0xFEFF;

    public static List<CsvRecord> Parse(string text)
    {
        if (text.Length > 0 && text[0] == Bom) text = text[1..];

        var records = new List<CsvRecord>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var line = 1;
        var recordStart = 1;
        var inQuotes = false;
        var fieldWasQuoted = false;
        var afterQuote = false; // 剛關閉引號，只允許接逗號或換行
        var quoteStartLine = 0;

        void EndField()
        {
            fields.Add(field.ToString());
            field.Clear();
            fieldWasQuoted = false;
            afterQuote = false;
        }

        void EndRecord()
        {
            EndField();
            // 整列空白（只有一個空欄位）視為空行，略過
            if (!(fields.Count == 1 && fields[0].Length == 0))
                records.Add(new CsvRecord(recordStart, fields));
            fields = new List<string>();
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                        afterQuote = true;
                    }
                }
                else
                {
                    if (c == '\n' || (c == '\r' && !(i + 1 < text.Length && text[i + 1] == '\n'))) line++;
                    field.Append(c);
                }
                continue;
            }

            switch (c)
            {
                case ',':
                    EndField();
                    break;
                case '\r':
                case '\n':
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    EndRecord();
                    line++;
                    recordStart = line;
                    break;
                case '"':
                    if (field.Length == 0 && !fieldWasQuoted && !afterQuote)
                    {
                        inQuotes = true;
                        fieldWasQuoted = true;
                        quoteStartLine = line;
                    }
                    else
                    {
                        throw new CsvParseException(line, L.F("Csv_BadQuote", line));
                    }
                    break;
                default:
                    if (afterQuote)
                    {
                        // 允許 "abc"   , 之類的尾端空白
                        if (c == ' ' || c == '\t') break;
                        throw new CsvParseException(line, L.F("Csv_AfterQuote", line));
                    }
                    field.Append(c);
                    break;
            }
        }

        if (inQuotes)
            throw new CsvParseException(quoteStartLine, L.F("Csv_Unclosed", quoteStartLine));

        if (field.Length > 0 || fields.Count > 0 || fieldWasQuoted) EndRecord();
        return records;
    }

    public static string Escape(string? value)
    {
        value ??= "";
        var needs = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0 || value.StartsWith(' ') || value.EndsWith(' ');
        return needs ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }
}
