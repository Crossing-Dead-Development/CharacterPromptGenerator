namespace CharacterPromptGenerator.Core.Models;

/// <summary>詞庫分類。Id 一經建立即不變，名稱可隨意改。</summary>
public sealed class Category
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public int SortOrder { get; set; }
    public int DefaultDrawCount { get; set; } = 1;

    public Category Clone() => (Category)MemberwiseClone();
}

/// <summary>單一詞條。</summary>
public sealed class Entry
{
    public string Id { get; set; } = "";
    public string CategoryId { get; set; } = "";
    public string Text { get; set; } = "";
    public string? Emoji { get; set; }
    public string Notes { get; set; } = "";
    public List<string> Tags { get; set; } = new();
    public bool Enabled { get; set; } = true;

    public Entry Clone()
    {
        var c = (Entry)MemberwiseClone();
        c.Tags = new List<string>(Tags);
        return c;
    }
}

/// <summary>library.json 的最外層結構。</summary>
public sealed class LibraryData
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public DateTime? ExportedAt { get; set; }
    public List<Category> Categories { get; set; } = new();
    public List<Entry> Entries { get; set; } = new();

    public LibraryData Clone() => new()
    {
        SchemaVersion = SchemaVersion,
        ExportedAt = ExportedAt,
        Categories = Categories.Select(c => c.Clone()).ToList(),
        Entries = Entries.Select(e => e.Clone()).ToList(),
    };

    public Category? FindCategory(string id) => Categories.FirstOrDefault(c => c.Id == id);

    public IEnumerable<Category> OrderedCategories() =>
        Categories.OrderBy(c => c.SortOrder).ThenBy(c => c.Name, StringComparer.Ordinal);
}
