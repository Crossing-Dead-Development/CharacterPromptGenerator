using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using CharacterPromptGenerator.Core.Drawing;
using CharacterPromptGenerator.Core.Import;
using CharacterPromptGenerator.Core.Localization;
using CharacterPromptGenerator.Core.Models;
using CharacterPromptGenerator.Core.Storage;

namespace CharacterPromptGenerator.Tests;

/// <summary>為行動版加進 Core 的功能：Source Generator 序列化、位元組匯入、抽題工作階段、方案管理。</summary>
public class JsonSourceGenTests
{
    static readonly JsonSerializerOptions Reflection = new(JsonOptions.Default) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };

    static LibraryData FixtureLibrary() =>
        ImportPlanner.Build(new LibraryData(), ImportReader.ReadJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "SeedData", "default_library.json"))),
            new ImportOptions { Mode = ImportMode.Replace }).ResultLibrary;

    [Fact]
    public void Library_OutputIsIdenticalToReflectionSerializer()
    {
        var lib = FixtureLibrary();
        lib.ExportedAt = new DateTime(2026, 10, 22, 12, 34, 56);
        Assert.Equal(JsonSerializer.Serialize(lib, Reflection), JsonOptions.Serialize(lib));
    }

    [Fact]
    public void OtherDataFiles_OutputIsIdenticalToReflectionSerializer()
    {
        var presets = new PresetFile { Presets = [new Preset { Id = "p", Name = "方案 ✨", Items = [new PresetItem { CategoryId = "A", Enabled = true, Count = 2 }] }] };
        var settings = new AppSettings { DrawSettings = { ["A"] = new DrawSetting { Enabled = true, Count = 3 } }, LastPresetId = "p", Language = "en" };
        var history = new HistoryFile
        {
            Results = [new SavedResult
            {
                Id = "h", CreatedAt = new DateTime(2026, 1, 2, 3, 4, 5), Favorite = true, Note = "備註",
                Items = [new SavedItem { CategoryId = "A", CategoryName = "分類", EntryId = null, Text = "家庭 👨‍👩‍👧", Emoji = "🕯️" }],
            }],
        };
        Assert.Equal(JsonSerializer.Serialize(presets, Reflection), JsonOptions.Serialize(presets));
        Assert.Equal(JsonSerializer.Serialize(settings, Reflection), JsonOptions.Serialize(settings));
        Assert.Equal(JsonSerializer.Serialize(history, Reflection), JsonOptions.Serialize(history));
    }

    [Fact]
    public void Deserialize_AcceptsPascalCaseCommentsAndTrailingCommas()
    {
        const string json = """
            { // 手動編輯過的設定檔
              "SchemaVersion": 1,
              "drawSettings": { "cat-1": { "enabled": true, "count": 4, }, },
              "HistoryLimit": 50,
            }
            """;
        var s = JsonOptions.Deserialize<AppSettings>(json)!;
        Assert.Equal(50, s.HistoryLimit);
        Assert.Equal(4, s.DrawSettings["cat-1"].Count);
    }

    [Fact]
    public void UnregisteredType_IsRejectedInsteadOfFallingBackToReflection() =>
        Assert.ThrowsAny<Exception>(() => JsonOptions.Serialize(new Uri("https://example.com")));
}

public class ReadBytesTests
{
    [Fact]
    public void Json_MatchesReadJson()
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "SeedData", "default_library.json"));
        var fromBytes = ImportReader.ReadBytes("library.JSON", Encoding.UTF8.GetBytes(text));
        var fromText = ImportReader.ReadJson(text);
        Assert.False(fromBytes.IsFatal);
        Assert.Equal(fromText.Rows.Count, fromBytes.Rows.Count);
        Assert.Equal("library.JSON", fromBytes.SourcePath);
    }

    [Fact]
    public void Csv_WithBom_IsDecoded()
    {
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("category,text,emoji\r\n分類,蠟燭,🕯️\r\n")).ToArray();
        var doc = ImportReader.ReadBytes("x.csv", bytes);
        Assert.False(doc.IsFatal);
        Assert.Equal("蠟燭", Assert.Single(doc.Rows).Text);
    }

    [Fact]
    public void UnknownExtension_IsFatal() =>
        Assert.True(ImportReader.ReadBytes("photo.png", [1, 2, 3]).IsFatal);

    [Fact]
    public void InvalidUtf8_IsFatal() =>
        Assert.True(ImportReader.ReadBytes("words.txt", [0x61, 0xC3, 0x28]).IsFatal);

    [Fact]
    public void Empty_IsFatal() =>
        Assert.True(ImportReader.ReadBytes("words.txt", []).IsFatal);
}

public class DrawSessionTests
{
    static (AppStore Store, DrawSession Session, TempDir Dir) Create(int aCount = 5)
    {
        var dir = new TempDir();
        var store = AppStore.Open(new DataPaths(dir.Path), new EmptySeed());
        store.ReplaceLibrary(Lib.Small(aCount), "test");
        var session = new DrawSession(store, new DrawEngine(new Random(7)));
        session.SetDrawSetting("A", true, 2);
        session.SetDrawSetting("B", true, 1);
        return (store, session, dir);
    }

    [Fact]
    public void Requests_FollowSettings_AndCountIsClamped()
    {
        var (_, s, dir) = Create();
        using var _d = dir;
        s.SetDrawSetting("B", false, 1);
        s.SetDrawSetting("A", true, 500);
        var r = Assert.Single(s.Requests());
        Assert.Equal(new DrawRequest("A", DrawSession.MaxCount), r);
    }

    [Fact]
    public void Settings_ArePersisted()
    {
        var (store, s, dir) = Create();
        using var _d = dir;
        s.SetDrawSetting("A", false, 3);
        var reopened = AppStore.Open(store.Paths, new EmptySeed());
        Assert.False(reopened.Settings.DrawSettings["A"].Enabled);
        Assert.Equal(3, reopened.Settings.DrawSettings["A"].Count);
    }

    [Fact]
    public void ResetToDefaults_UsesCategoryDefaults()
    {
        var (store, s, dir) = Create();
        using var _d = dir;
        store.MutateLibrary(lib => lib.FindCategory("A")!.DefaultDrawCount = 3);
        store.Settings.LastPresetId = "p";
        s.ResetToDefaults();
        Assert.Null(store.Settings.LastPresetId);
        Assert.Equal([new DrawRequest("A", 3), new DrawRequest("B", 1)], s.Requests());
    }

    [Fact]
    public void GenerateAll_ProducesResult_AndWritesHistory()
    {
        var (store, s, dir) = Create();
        using var _d = dir;
        s.GenerateAll();
        Assert.Equal(3, s.Slots.Count);
        Assert.Equal(s.CurrentHistoryId, Assert.Single(store.History).Id);
        Assert.Empty(s.Warnings);
        Assert.Equal(["A", "B"], s.Groups().Select(g => g.CategoryId));
    }

    [Fact]
    public void RerollUnlocked_KeepsLockedSlots()
    {
        var (_, s, dir) = Create(aCount: 30);
        using var _d = dir;
        s.GenerateAll();
        var keep = s.Slots[0];
        keep.Locked = true;
        for (var i = 0; i < 10; i++)
        {
            s.RerollUnlocked();
            Assert.Equal(keep.EntryId, s.Slots[0].EntryId);
            Assert.True(s.Slots[0].Locked);
            Assert.Equal(3, s.Slots.Count);
        }
        Assert.Equal(1, s.LockedCount);
        s.UnlockAll();
        Assert.Equal(0, s.LockedCount);
    }

    [Fact]
    public void RerollUnlocked_WithoutResult_Generates()
    {
        var (_, s, dir) = Create();
        using var _d = dir;
        s.RerollUnlocked();
        Assert.True(s.HasResult);
    }

    [Fact]
    public void RerollCategory_OnlyChangesThatCategory()
    {
        var (_, s, dir) = Create(aCount: 30);
        using var _d = dir;
        s.GenerateAll();
        var b = s.Slots.Single(x => x.CategoryId == "B").EntryId;
        Assert.True(s.RerollCategory("A"));
        Assert.Equal(b, s.Slots.Single(x => x.CategoryId == "B").EntryId);
        Assert.Equal(2, s.Slots.Count(x => x.CategoryId == "A"));
    }

    [Fact]
    public void RerollCategory_UncheckedCategory_KeepsCurrentCount()
    {
        var (_, s, dir) = Create(aCount: 30);
        using var _d = dir;
        s.SetDrawSetting("A", true, 3);
        s.GenerateAll();
        s.SetDrawSetting("A", false, 3);
        Assert.True(s.RerollCategory("A"));
        Assert.Equal(3, s.Slots.Count(x => x.CategoryId == "A"));
    }

    [Fact]
    public void RerollCategory_DeletedCategory_ReturnsFalseAndKeepsResult()
    {
        var (store, s, dir) = Create();
        using var _d = dir;
        s.GenerateAll();
        var before = s.Slots.Select(x => x.EntryId).ToList();
        store.MutateLibrary(lib => Core.Library.LibraryEditor.DeleteCategory(lib, "B"));
        Assert.False(s.RerollCategory("B"));
        Assert.Equal(before, s.Slots.Select(x => x.EntryId));
        Assert.Equal(L.T("Draw_CategoryDeleted"), Assert.Single(s.Warnings));
    }

    [Fact]
    public void NothingSelected_GivesWarning_AndNoHistory()
    {
        var (store, s, dir) = Create();
        using var _d = dir;
        s.SetAllEnabled(false);
        s.GenerateAll();
        Assert.False(s.HasResult);
        Assert.Equal(L.T("Draw_NothingSelected"), s.Warnings[0]);
        Assert.Empty(store.History);
    }

    [Fact]
    public void NotEnoughEntries_GivesWarning()
    {
        var (_, s, dir) = Create(aCount: 1);
        using var _d = dir;
        s.GenerateAll();
        Assert.Single(s.Warnings);
        Assert.Single(s.Slots, x => x.CategoryId == "A");
    }

    [Fact]
    public void LoadSaved_LocksEverything_AndRerollOnlyFillsNewCategories()
    {
        var (store, s, dir) = Create(aCount: 30);
        using var _d = dir;
        s.SetDrawSetting("B", false, 1);
        s.GenerateAll();
        var saved = store.History[0];
        var other = new DrawSession(store, new DrawEngine(new Random(3)));
        other.LoadSaved(saved);
        Assert.Equal(saved.Id, other.CurrentHistoryId);
        Assert.All(other.Slots, x => Assert.True(x.Locked));

        other.SetDrawSetting("B", true, 1);
        other.RerollUnlocked();
        Assert.Equal(saved.Items.Select(i => i.EntryId), other.Slots.Where(x => x.CategoryId == "A").Select(x => x.EntryId));
        Assert.Single(other.Slots, x => x.CategoryId == "B");
    }

    [Fact]
    public void ToggleFavorite_PersistsAndToggles()
    {
        var (store, s, dir) = Create();
        using var _d = dir;
        Assert.False(s.ToggleFavorite());
        s.GenerateAll();
        Assert.True(s.ToggleFavorite());
        Assert.True(s.IsFavorite);
        Assert.True(AppStore.Open(store.Paths, new EmptySeed()).History.Single().Favorite);
        s.ToggleFavorite();
        Assert.False(s.IsFavorite);
    }

    [Fact]
    public void ToggleFavorite_AfterHistoryCleared_CreatesNewFavoriteRecord()
    {
        var (store, s, dir) = Create();
        using var _d = dir;
        s.GenerateAll();
        store.History.Clear();
        store.SaveHistory();
        s.ToggleFavorite();
        var record = Assert.Single(store.History);
        Assert.True(record.Favorite);
        Assert.Equal(record.Id, s.CurrentHistoryId);
    }

    [Fact]
    public void ToText_ContainsCategoriesAndEmoji()
    {
        var (_, s, dir) = Create(aCount: 1);
        using var _d = dir;
        s.SetDrawSetting("B", true, 2);
        s.GenerateAll();
        var text = s.ToText();
        Assert.Contains("分類A", text);
        Assert.Contains("🕯️ 蠟燭", text);
        Assert.Contains("🦋 飛蛾", text);
    }
}

public class PresetManagerTests
{
    static (AppStore Store, PresetManager Presets, TempDir Dir) Create()
    {
        var dir = new TempDir();
        var store = AppStore.Open(new DataPaths(dir.Path), new EmptySeed());
        store.ReplaceLibrary(Lib.Small(), "test");
        var session = new DrawSession(store);
        session.SetDrawSetting("A", true, 2);
        session.SetDrawSetting("B", false, 1);
        return (store, new PresetManager(store), dir);
    }

    [Fact]
    public void SaveAs_StoresCurrentSettings_AndBecomesCurrent()
    {
        var (store, pm, dir) = Create();
        using var _d = dir;
        var p = pm.SaveAs("  我的方案  ")!;
        Assert.Equal("我的方案", p.Name);
        Assert.Equal(p.Id, store.Settings.LastPresetId);
        Assert.Contains(p.Items, i => i is { CategoryId: "A", Enabled: true, Count: 2 });
        Assert.Contains(p.Items, i => i is { CategoryId: "B", Enabled: false });
        Assert.Single(AppStore.Open(store.Paths, new EmptySeed()).Presets, x => x.Name == "我的方案");
    }

    [Fact]
    public void SaveAs_BlankName_DoesNothing()
    {
        var (store, pm, dir) = Create();
        using var _d = dir;
        Assert.Null(pm.SaveAs("   "));
        Assert.Empty(store.Presets);
    }

    [Fact]
    public void Apply_Overwrite_Rename_Delete()
    {
        var (store, pm, dir) = Create();
        using var _d = dir;
        var p = pm.SaveAs("方案")!;
        var session = new DrawSession(store);

        session.SetDrawSetting("A", false, 9);
        Assert.True(pm.Apply(p.Id));
        Assert.Equal(2, store.Settings.DrawSettings["A"].Count);
        Assert.True(store.Settings.DrawSettings["A"].Enabled);

        session.SetDrawSetting("B", true, 2);
        Assert.True(pm.Overwrite(p.Id));
        Assert.Contains(pm.Find(p.Id)!.Items, i => i is { CategoryId: "B", Enabled: true, Count: 2 });

        Assert.False(pm.Rename(p.Id, "方案"));
        Assert.True(pm.Rename(p.Id, "新名字"));
        Assert.Equal("新名字", AppStore.Open(store.Paths, new EmptySeed()).Presets.Single().Name);

        Assert.True(pm.Delete(p.Id));
        Assert.Empty(store.Presets);
        Assert.Null(store.Settings.LastPresetId);
        Assert.False(pm.Delete(p.Id));
        Assert.False(pm.Apply(p.Id));
    }
}
