namespace CharacterPromptGenerator.Core.Models;

/// <summary>抽題方案：每個分類是否啟用與抽取數量。</summary>
public sealed class Preset
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<PresetItem> Items { get; set; } = new();

    public Preset Clone() => new()
    {
        Id = Id,
        Name = Name,
        Items = Items.Select(i => new PresetItem { CategoryId = i.CategoryId, Enabled = i.Enabled, Count = i.Count }).ToList(),
    };
}

public sealed class PresetItem
{
    public string CategoryId { get; set; } = "";
    public bool Enabled { get; set; }
    public int Count { get; set; }
}

public sealed class PresetFile
{
    public int SchemaVersion { get; set; } = 1;
    public List<Preset> Presets { get; set; } = new();
}

/// <summary>歷史／收藏：保存當時的文字快照，與詞庫解耦。</summary>
public sealed class SavedResult
{
    public string Id { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public bool Favorite { get; set; }
    public string Note { get; set; } = "";
    public List<SavedItem> Items { get; set; } = new();
}

public sealed class SavedItem
{
    public string CategoryId { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string? EntryId { get; set; }
    public string Text { get; set; } = "";
    public string? Emoji { get; set; }
    public string Notes { get; set; } = "";
}

public sealed class HistoryFile
{
    public int SchemaVersion { get; set; } = 1;
    public List<SavedResult> Results { get; set; } = new();
}

/// <summary>主畫面目前的抽題設定與介面偏好。</summary>
public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    public Dictionary<string, DrawSetting> DrawSettings { get; set; } = new();
    public string? LastPresetId { get; set; }
    public double DisplayFontSize { get; set; } = 30;
    public bool DisplayShowNotes { get; set; } = true;
    public bool DisplayShowControls { get; set; } = true;
    public int HistoryLimit { get; set; } = 300;
}

public sealed class DrawSetting
{
    public bool Enabled { get; set; }
    public int Count { get; set; }
}
