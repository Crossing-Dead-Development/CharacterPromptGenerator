namespace CharacterPromptGenerator.Core.Import;

public enum ImportFormat { Json, Csv, Txt }

/// <summary>解析後、尚未與目前詞庫比對的一筆詞條。</summary>
public sealed class ImportRow
{
    /// <summary>來源行號（CSV/TXT 為實體行號；JSON 為 entries 陣列的索引 +1）。</summary>
    public int Line { get; init; }
    public string CategoryName { get; set; } = "";
    /// <summary>僅 JSON：檔案內的分類 ID。</summary>
    public string? CategoryId { get; set; }
    public string? EntryId { get; set; }
    public string Text { get; set; } = "";
    public string? Emoji { get; set; }
    public string Notes { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public bool Enabled { get; set; } = true;
}

/// <summary>檔案中明確定義的分類（JSON 才有完整欄位）。</summary>
public sealed class ImportCategory
{
    public string? Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public int? SortOrder { get; set; }
    public int? DefaultDrawCount { get; set; }
}

public sealed record ImportIssue(int? Line, string Message)
{
    public string LineText => Line is null ? "" : Line.Value.ToString();
}

public sealed class ImportDocument
{
    public ImportFormat Format { get; init; }
    public string? SourcePath { get; set; }
    public List<ImportCategory> Categories { get; } = new();
    public List<ImportRow> Rows { get; } = new();
    /// <summary>單列錯誤：該列會被略過，其他列仍可匯入。</summary>
    public List<ImportIssue> Errors { get; } = new();
    public List<ImportIssue> Warnings { get; } = new();
    /// <summary>整份檔案無法使用（空檔、編碼錯誤、JSON 語法錯誤、版本不符…）。</summary>
    public string? FatalError { get; set; }
    public bool IsFatal => FatalError is not null;

    public static ImportDocument Fatal(ImportFormat format, string message) => new() { Format = format, FatalError = message };
}
