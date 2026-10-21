using System.Text.Json;
using CharacterPromptGenerator.Core.Localization;
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
            return ImportDocument.Fatal(ImportFormat.Txt, L.T("Reader_BadExtension"));
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
            return ImportDocument.Fatal(ImportFormat.Json, ex.LineNumber is { } l
                ? L.F("Reader_JsonErrorAt", l + 1)
                : L.T("Reader_JsonError"));
        }

        using (json)
        {
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return ImportDocument.Fatal(ImportFormat.Json, L.T("Reader_JsonNotObject"));

            var doc = new ImportDocument { Format = ImportFormat.Json };

            if (!TryGet(root, "schemaVersion", out var ver))
            {
                doc.Warnings.Add(new ImportIssue(null, L.T("Reader_NoSchema")));
            }
            else if (ver.ValueKind != JsonValueKind.Number || !ver.TryGetInt32(out var v) || v < 1)
            {
                return ImportDocument.Fatal(ImportFormat.Json, L.T("Reader_BadSchema"));
            }
            else if (v > LibraryData.CurrentSchemaVersion)
            {
                return ImportDocument.Fatal(ImportFormat.Json,
                    L.F("Reader_NewerSchema", v, LibraryData.CurrentSchemaVersion));
            }

            if (!TryGet(root, "entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
            {
                return ImportDocument.Fatal(ImportFormat.Json,
                    L.T("Reader_NoEntries"));
            }

            var fileCats = new Dictionary<string, ImportCategory>();
            if (TryGet(root, "categories", out var cats))
            {
                if (cats.ValueKind != JsonValueKind.Array)
                    return ImportDocument.Fatal(ImportFormat.Json, L.T("Reader_CategoriesNotArray"));
                var i = 0;
                foreach (var c in cats.EnumerateArray())
                {
                    i++;
                    var where = L.F("Reader_CategoryAt", i);
                    if (c.ValueKind != JsonValueKind.Object) { doc.Errors.Add(new ImportIssue(null, L.F("Reader_CategoryNotObject", where))); continue; }
                    var name = TextNormalizer.Clean(GetString(c, "name"));
                    if (name.Length == 0) { doc.Errors.Add(new ImportIssue(null, L.F("Reader_CategoryNoName", where))); continue; }
                    var id = TextNormalizer.CleanOptional(GetString(c, "id"));
                    if (id is not null && fileCats.ContainsKey(id))
                    {
                        doc.Errors.Add(new ImportIssue(null, L.F("Reader_CategoryDupId", where, name, id)));
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
                if (e.ValueKind != JsonValueKind.Object) { doc.Errors.Add(new ImportIssue(index, L.F("Reader_EntryNotObject", index))); continue; }

                var textValue = TextNormalizer.Clean(GetString(e, "text"));
                if (textValue.Length == 0) { doc.Errors.Add(new ImportIssue(index, L.F("Reader_EntryNoText", index))); continue; }

                var catId = TextNormalizer.CleanOptional(GetString(e, "categoryId"));
                var catName = TextNormalizer.Clean(GetString(e, "category"));
                if (catId is not null && fileCats.TryGetValue(catId, out var fc))
                {
                    catName = fc.Name;
                }
                else if (catName.Length == 0)
                {
                    doc.Errors.Add(new ImportIssue(index, catId is null
                        ? L.F("Reader_EntryNoCategory", index, textValue)
                        : L.F("Reader_EntryUnknownCategory", index, textValue, catId)));
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
        if (records.Count == 0) return ImportDocument.Fatal(ImportFormat.Csv, L.T("Reader_CsvEmpty"));

        var doc = new ImportDocument { Format = ImportFormat.Csv };
        var header = records[0];
        var columns = new Dictionary<string, int>();
        for (var i = 0; i < header.Fields.Count; i++)
        {
            var name = TextNormalizer.Key(header.Fields[i]);
            if (HeaderAliases.TryGetValue(name, out var canonical))
            {
                if (!columns.TryAdd(canonical, i))
                    doc.Warnings.Add(new ImportIssue(header.Line, L.F("Reader_CsvDupColumn", canonical)));
            }
            else if (name.Length > 0)
            {
                doc.Warnings.Add(new ImportIssue(header.Line, L.F("Reader_CsvUnknownColumn", header.Fields[i])));
            }
        }
        if (!columns.ContainsKey("category") || !columns.ContainsKey("text"))
        {
            return ImportDocument.Fatal(ImportFormat.Csv,
                L.F("Reader_CsvBadHeader", string.Join(", ", header.Fields)));
        }

        string Field(CsvRecord r, string col) =>
            columns.TryGetValue(col, out var idx) && idx < r.Fields.Count ? r.Fields[idx] : "";

        foreach (var r in records.Skip(1))
        {
            if (r.Fields.All(string.IsNullOrWhiteSpace)) continue;
            if (r.Fields.Count > header.Fields.Count)
            {
                doc.Errors.Add(new ImportIssue(r.Line, L.F("Reader_CsvTooManyFields", r.Fields.Count, header.Fields.Count)));
                continue;
            }
            var cat = TextNormalizer.Clean(Field(r, "category"));
            var txt = TextNormalizer.Clean(Field(r, "text"));
            if (cat.Length == 0) { doc.Errors.Add(new ImportIssue(r.Line, L.T("Reader_CsvNoCategory"))); continue; }
            if (txt.Length == 0) { doc.Errors.Add(new ImportIssue(r.Line, L.T("Reader_CsvNoText"))); continue; }
            var enabledRaw = Field(r, "enabled");
            var enabled = ParseBool(enabledRaw);
            if (enabled is null && !string.IsNullOrWhiteSpace(enabledRaw))
            {
                doc.Errors.Add(new ImportIssue(r.Line, L.F("Reader_CsvBadEnabled", enabledRaw)));
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
                    doc.Errors.Add(new ImportIssue(lineNo, L.T("Reader_TxtEmptyCategory")));
                    current = null;
                }
                else current = name;
                continue;
            }
            if (current is null)
            {
                doc.Errors.Add(new ImportIssue(lineNo, L.F("Reader_TxtNoCategory", TextNormalizer.Clean(line))));
                continue;
            }
            doc.Rows.Add(new ImportRow { Line = lineNo, CategoryName = current, Text = TextNormalizer.Clean(line) });
        }
        if (doc.Rows.Count == 0 && doc.Errors.Count == 0)
            return ImportDocument.Fatal(ImportFormat.Txt, L.T("Reader_TxtEmpty"));
        return doc;
    }
}
