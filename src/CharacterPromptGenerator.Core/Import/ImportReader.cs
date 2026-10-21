using System.Text.Json;
using CharacterPromptGenerator.Core.Models;
using CharacterPromptGenerator.Core.Text;

namespace CharacterPromptGenerator.Core.Import;

/// <summary>把 JSON / CSV / TXT 解析成 ImportDocument。只解析與檢查格式，不碰目前詞庫。</summary>
public static class ImportReader
{
    public static ImportFormat? DetectFormat(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".json" => ImportFormat.Json,
        ".csv" => ImportFormat.Csv,
        ".txt" => ImportFormat.Txt,
        _ => null,
    };

    public static ImportDocument ReadFile(string path)
    {
        var format = DetectFormat(path);
        if (format is null)
            return ImportDocument.Fatal(ImportFormat.Txt, "不支援的副檔名，請選擇 .json、.csv 或 .txt 檔案。");
        var (text, error) = TextFileLoader.Read(path);
        var doc = error is not null
            ? ImportDocument.Fatal(format.Value, error)
            : format switch
            {
                ImportFormat.Json => ReadJson(text!),
                ImportFormat.Csv => ReadCsv(text!),
                _ => ReadTxt(text!),
            };
        doc.SourcePath = path;
        return doc;
    }

    // ── JSON ───────────────────────────────────────────────

    public static ImportDocument ReadJson(string text)
    {
        JsonDocument json;
        try
        {
            json = JsonDocument.Parse(text, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
        }
        catch (JsonException ex)
        {
            var line = ex.LineNumber is { } l ? $"第 {l + 1} 行附近" : "";
            return ImportDocument.Fatal(ImportFormat.Json, $"JSON 格式錯誤{(line.Length > 0 ? "（" + line + "）" : "")}，檔案可能損壞或不完整。");
        }

        using (json)
        {
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return ImportDocument.Fatal(ImportFormat.Json, "JSON 最外層必須是物件（{ ... }），包含 schemaVersion、categories、entries。");

            var doc = new ImportDocument { Format = ImportFormat.Json };

            if (!TryGet(root, "schemaVersion", out var ver))
            {
                doc.Warnings.Add(new ImportIssue(null, "檔案沒有 schemaVersion，以版本 1 的格式讀取。"));
            }
            else if (ver.ValueKind != JsonValueKind.Number || !ver.TryGetInt32(out var v) || v < 1)
            {
                return ImportDocument.Fatal(ImportFormat.Json, "schemaVersion 必須是正整數。");
            }
            else if (v > LibraryData.CurrentSchemaVersion)
            {
                return ImportDocument.Fatal(ImportFormat.Json,
                    $"這個檔案的格式版本為 {v}，比目前程式支援的 {LibraryData.CurrentSchemaVersion} 還新，請先更新程式。");
            }

            if (!TryGet(root, "entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
            {
                return ImportDocument.Fatal(ImportFormat.Json,
                    "找不到 entries 陣列。這看起來不是詞庫檔（可能是歷史紀錄或設定檔）。");
            }

            var fileCats = new Dictionary<string, ImportCategory>();
            if (TryGet(root, "categories", out var cats))
            {
                if (cats.ValueKind != JsonValueKind.Array)
                    return ImportDocument.Fatal(ImportFormat.Json, "categories 必須是陣列。");
                var i = 0;
                foreach (var c in cats.EnumerateArray())
                {
                    i++;
                    var where = $"categories 第 {i} 項";
                    if (c.ValueKind != JsonValueKind.Object) { doc.Errors.Add(new ImportIssue(null, $"{where}不是物件，已略過。")); continue; }
                    var name = TextNormalizer.Clean(GetString(c, "name"));
                    if (name.Length == 0) { doc.Errors.Add(new ImportIssue(null, $"{where}缺少 name，已略過。")); continue; }
                    var id = TextNormalizer.CleanOptional(GetString(c, "id"));
                    if (id is not null && fileCats.ContainsKey(id))
                    {
                        doc.Errors.Add(new ImportIssue(null, $"{where}「{name}」的 id {id} 與前面的分類重複，已略過。"));
                        continue;
                    }
                    var ic = new ImportCategory
                    {
                        Id = id,
                        Name = name,
                        Description = GetString(c, "description") ?? "",
                        Enabled = GetBool(c, "enabled") ?? true,
                        SortOrder = GetInt(c, "sortOrder"),
                        DefaultDrawCount = GetInt(c, "defaultDrawCount") is { } d ? Math.Max(0, d) : null,
                    };
                    doc.Categories.Add(ic);
                    if (id is not null) fileCats[id] = ic;
                }
            }

            var index = 0;
            foreach (var e in entries.EnumerateArray())
            {
                index++;
                if (e.ValueKind != JsonValueKind.Object) { doc.Errors.Add(new ImportIssue(index, $"entries 第 {index} 項不是物件。")); continue; }

                var textValue = TextNormalizer.Clean(GetString(e, "text"));
                if (textValue.Length == 0) { doc.Errors.Add(new ImportIssue(index, $"entries 第 {index} 項缺少 text。")); continue; }

                var catId = TextNormalizer.CleanOptional(GetString(e, "categoryId"));
                var catName = TextNormalizer.Clean(GetString(e, "category"));
                if (catId is not null && fileCats.TryGetValue(catId, out var fc))
                {
                    catName = fc.Name;
                }
                else if (catName.Length == 0)
                {
                    doc.Errors.Add(new ImportIssue(index, catId is null
                        ? $"entries 第 {index} 項「{textValue}」沒有 categoryId 或 category。"
                        : $"entries 第 {index} 項「{textValue}」的 categoryId {catId} 不在 categories 裡。"));
                    continue;
                }
                else
                {
                    catId = null; // 以名稱指定分類
                }

                List<string> tags;
                if (TryGet(e, "tags", out var t))
                {
                    if (t.ValueKind == JsonValueKind.Array)
                        tags = TextNormalizer.CleanTags(t.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()));
                    else if (t.ValueKind == JsonValueKind.String)
                        tags = TextNormalizer.ParseTags(t.GetString());
                    else tags = new();
                }
                else tags = new();

                doc.Rows.Add(new ImportRow
                {
                    Line = index,
                    CategoryId = catId,
                    CategoryName = catName,
                    EntryId = TextNormalizer.CleanOptional(GetString(e, "id")),
                    Text = textValue,
                    Emoji = TextNormalizer.CleanOptional(GetString(e, "emoji")),
                    Notes = GetString(e, "notes")?.Trim() ?? "",
                    Tags = tags,
                    Enabled = GetBool(e, "enabled") ?? true,
                });
            }
            return doc;
        }
    }

    static bool TryGet(JsonElement obj, string name, out JsonElement value)
    {
        foreach (var p in obj.EnumerateObject())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = p.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    static string? GetString(JsonElement obj, string name) =>
        TryGet(obj, name, out var v) ? v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            _ => null,
        } : null;

    static bool? GetBool(JsonElement obj, string name) =>
        TryGet(obj, name, out var v) ? v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => ParseBool(v.GetString()),
            _ => null,
        } : null;

    static int? GetInt(JsonElement obj, string name) =>
        TryGet(obj, name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;

    static bool? ParseBool(string? s) => TextNormalizer.Key(s) switch
    {
        "" => null,
        "true" or "1" or "yes" or "y" or "是" or "啟用" => true,
        "false" or "0" or "no" or "n" or "否" or "停用" => false,
        _ => null,
    };

    // ── CSV ────────────────────────────────────────────────

    static readonly Dictionary<string, string> HeaderAliases = new()
    {
        ["category"] = "category", ["分類"] = "category",
        ["text"] = "text", ["詞條"] = "text", ["文字"] = "text", ["內容"] = "text",
        ["emoji"] = "emoji", ["表情"] = "emoji",
        ["notes"] = "notes", ["note"] = "notes", ["備註"] = "notes",
        ["tags"] = "tags", ["標籤"] = "tags",
        ["enabled"] = "enabled", ["啟用"] = "enabled",
    };

    public static ImportDocument ReadCsv(string text)
    {
        List<CsvRecord> records;
        try
        {
            records = CsvParser.Parse(text);
        }
        catch (CsvParseException ex)
        {
            return ImportDocument.Fatal(ImportFormat.Csv, ex.Message);
        }
        if (records.Count == 0) return ImportDocument.Fatal(ImportFormat.Csv, "CSV 沒有任何內容。");

        var doc = new ImportDocument { Format = ImportFormat.Csv };
        var header = records[0];
        var columns = new Dictionary<string, int>();
        for (var i = 0; i < header.Fields.Count; i++)
        {
            var name = TextNormalizer.Key(header.Fields[i]);
            if (HeaderAliases.TryGetValue(name, out var canonical))
            {
                if (!columns.TryAdd(canonical, i))
                    doc.Warnings.Add(new ImportIssue(header.Line, $"欄位 {canonical} 重複出現，只使用第一個。"));
            }
            else if (name.Length > 0)
            {
                doc.Warnings.Add(new ImportIssue(header.Line, $"忽略不認識的欄位「{header.Fields[i]}」。"));
            }
        }
        if (!columns.ContainsKey("category") || !columns.ContainsKey("text"))
        {
            return ImportDocument.Fatal(ImportFormat.Csv,
                "CSV 第一行必須是標頭，且包含 category 與 text 欄位。\n" +
                $"目前讀到的標頭：{string.Join(", ", header.Fields)}\n" +
                "正確範例：category,text,emoji,notes,tags");
        }

        string Field(CsvRecord r, string col) =>
            columns.TryGetValue(col, out var idx) && idx < r.Fields.Count ? r.Fields[idx] : "";

        foreach (var r in records.Skip(1))
        {
            if (r.Fields.All(string.IsNullOrWhiteSpace)) continue;
            if (r.Fields.Count > header.Fields.Count)
            {
                doc.Errors.Add(new ImportIssue(r.Line, $"欄位數（{r.Fields.Count}）比標頭（{header.Fields.Count}）多，可能有沒加引號的逗號。"));
                continue;
            }
            var cat = TextNormalizer.Clean(Field(r, "category"));
            var txt = TextNormalizer.Clean(Field(r, "text"));
            if (cat.Length == 0) { doc.Errors.Add(new ImportIssue(r.Line, "category 欄位是空的。")); continue; }
            if (txt.Length == 0) { doc.Errors.Add(new ImportIssue(r.Line, "text 欄位是空的。")); continue; }
            var enabledRaw = Field(r, "enabled");
            var enabled = ParseBool(enabledRaw);
            if (enabled is null && !string.IsNullOrWhiteSpace(enabledRaw))
            {
                doc.Errors.Add(new ImportIssue(r.Line, $"enabled 欄位「{enabledRaw}」看不懂，請填 true 或 false。"));
                continue;
            }
            doc.Rows.Add(new ImportRow
            {
                Line = r.Line,
                CategoryName = cat,
                Text = txt,
                Emoji = TextNormalizer.CleanOptional(Field(r, "emoji")),
                Notes = Field(r, "notes").Trim(),
                Tags = TextNormalizer.ParseTags(Field(r, "tags")),
                Enabled = enabled ?? true,
            });
        }
        return doc;
    }

    // ── TXT ────────────────────────────────────────────────

    public static ImportDocument ReadTxt(string text)
    {
        if (text.Length > 0 && text[0] == CsvParser.Bom) text = text[1..];
        var doc = new ImportDocument { Format = ImportFormat.Txt };
        string? current = null;
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var lineNo = i + 1;
            var line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var name = TextNormalizer.Clean(line[1..^1]);
                if (name.Length == 0)
                {
                    doc.Errors.Add(new ImportIssue(lineNo, "分類名稱是空的：[]。"));
                    current = null;
                }
                else current = name;
                continue;
            }
            if (current is null)
            {
                doc.Errors.Add(new ImportIssue(lineNo, $"「{TextNormalizer.Clean(line)}」前面沒有 [分類名稱]，不知道要放到哪個分類。"));
                continue;
            }
            doc.Rows.Add(new ImportRow { Line = lineNo, CategoryName = current, Text = TextNormalizer.Clean(line) });
        }
        if (doc.Rows.Count == 0 && doc.Errors.Count == 0)
            return ImportDocument.Fatal(ImportFormat.Txt, "TXT 中沒有任何詞條。格式：先寫一行 [分類名稱]，下面每行一個詞條。");
        return doc;
    }
}
