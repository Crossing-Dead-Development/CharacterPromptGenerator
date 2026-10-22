using System.IO.Compression;
using System.Text;
using CharacterPromptGenerator.Core.Drawing;
using CharacterPromptGenerator.Core.Import;
using CharacterPromptGenerator.Core.Library;
using CharacterPromptGenerator.Core.Localization;
using CharacterPromptGenerator.Core.Models;
using CharacterPromptGenerator.Core.Storage;

namespace CharacterPromptGenerator.Tests;

/// <summary>行動版 Phase 3 加進 Core 的功能：詞條搜尋篩選、匯出位元組、完整備份 zip。</summary>
public class LibraryQueryTests
{
    static LibraryData Lib3()
    {
        var lib = Lib.Small(3);
        lib.Categories[0].SortOrder = 5; // A 排在 B 後面
        lib.Entries.Add(new Entry { Id = "x", CategoryId = "A", Text = "Moon Knight", Notes = "月光", Tags = ["騎士"], Enabled = false });
        return lib;
    }

    [Fact]
    public void Filter_OrdersByCategory_ThenOriginalOrder()
    {
        var q = new LibraryQuery(Lib3());
        Assert.Equal(["b1", "b2", "a1", "a2", "a3", "x"], q.Filter(null, null, EntryStatusFilter.All).Select(e => e.Id));
        Assert.Equal(6, q.Total);
    }

    [Theory]
    [InlineData("moon", "x")]          // 不分大小寫
    [InlineData("  MOON   knight ", "x")] // 多餘空白
    [InlineData("月光", "x")]           // 備註
    [InlineData("騎士", "x")]           // 標籤
    [InlineData("🕯️", "b1")]           // Emoji 欄位
    public void Search_MatchesTextNotesTagsAndEmoji(string search, string expectedId) =>
        Assert.Equal(expectedId, Assert.Single(new LibraryQuery(Lib3()).Filter(null, search, EntryStatusFilter.All)).Id);

    [Fact]
    public void CategoryAndStatusFilters_Combine()
    {
        var q = new LibraryQuery(Lib3());
        Assert.Equal(["a1", "a2", "a3"], q.Filter("A", "", EntryStatusFilter.Enabled).Select(e => e.Id));
        Assert.Equal("x", Assert.Single(q.Filter("A", null, EntryStatusFilter.Disabled)).Id);
        Assert.Empty(q.Filter("B", null, EntryStatusFilter.Disabled));
    }
}

public class ExportBytesTests
{
    [Fact]
    public void Csv_HasBom_AndReimportsIdentically()
    {
        var lib = Lib.Small();
        var bytes = LibraryExporter.ToCsvBytes(lib);
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes.Take(3));
        var doc = ImportReader.ReadBytes("x.csv", bytes);
        Assert.False(doc.IsFatal);
        Assert.Equal(lib.Entries.Select(e => (e.Text, e.Emoji)), doc.Rows.Select(r => (r.Text, r.Emoji)));
    }

    [Fact]
    public void Json_HasNoBom_AndKeepsIds()
    {
        var lib = Lib.Small();
        var bytes = LibraryExporter.ToJsonBytes(lib);
        Assert.NotEqual(0xEF, bytes[0]);
        var plan = ImportPlanner.Build(new LibraryData(), ImportReader.ReadBytes("x.json", bytes), new ImportOptions { Mode = ImportMode.Replace });
        Assert.Equal(lib.Entries.Select(e => e.Id), plan.ResultLibrary.Entries.Select(e => e.Id));
        Assert.Equal(lib.Categories.Select(c => c.Id), plan.ResultLibrary.Categories.Select(c => c.Id));
    }
}

public class FullBackupTests
{
    static AppStore Populated(TempDir dir)
    {
        var store = AppStore.Open(new DataPaths(dir.Path), new FileSeed());
        var session = new DrawSession(store);
        session.GenerateAll();
        session.ToggleFavorite();
        new PresetManager(store).SaveAs("備份測試方案 ✨");
        store.Settings.Language = "en";
        store.SaveSettings();
        return store;
    }

    static byte[] Backup(AppStore store)
    {
        using var ms = new MemoryStream();
        FullBackup.Write(store, ms, "android", "0.1.0");
        return ms.ToArray();
    }

    [Fact]
    public void Write_ContainsTheSameJsonFilesAsTheDataFolder()
    {
        using var dir = new TempDir();
        var store = Populated(dir);
        using var zip = new ZipArchive(new MemoryStream(Backup(store)));
        Assert.Equal(["history.json", "library.json", "manifest.json", "presets.json", "settings.json"], zip.Entries.Select(e => e.FullName).Order());
        using var reader = new StreamReader(zip.GetEntry("library.json")!.Open());
        Assert.Equal(File.ReadAllText(store.Paths.LibraryFile), reader.ReadToEnd());
    }

    [Fact]
    public void RoundTrip_RestoresEverything_IntoAnotherDevice()
    {
        using var source = new TempDir();
        var original = Populated(source);
        var bytes = Backup(original);

        using var target = new TempDir();
        var other = AppStore.Open(new DataPaths(target.Path), new EmptySeed());
        other.Settings.Language = "zh-TW";
        var content = FullBackup.Read(new MemoryStream(bytes));
        Assert.Equal("android", content.Manifest.Platform);
        Assert.Equal(original.Library.Entries.Count, content.Manifest.Entries);
        other.RestoreFull(content);

        var reopened = AppStore.Open(new DataPaths(target.Path), new EmptySeed());
        Assert.Equal(original.Library.Entries.Select(e => (e.Id, e.Text, e.Emoji)), reopened.Library.Entries.Select(e => (e.Id, e.Text, e.Emoji)));
        Assert.Equal(original.Presets.Select(p => p.Name), reopened.Presets.Select(p => p.Name));
        Assert.Equal(original.History.Select(h => (h.Id, h.Favorite)), reopened.History.Select(h => (h.Id, h.Favorite)));
        Assert.Equal(original.Settings.LastPresetId, reopened.Settings.LastPresetId);
        Assert.Equal("zh-TW", reopened.Settings.Language); // 語言是這台裝置的偏好
    }

    [Fact]
    public void Restore_KeepsSafetyCopyOfPreviousData()
    {
        using var dir = new TempDir();
        var store = Populated(dir);
        var before = store.Library.Entries.Count;
        using var empty = new TempDir();
        var emptyStore = AppStore.Open(new DataPaths(empty.Path), new EmptySeed());
        emptyStore.ReplaceLibrary(Lib.Small(), "test");

        store.RestoreFull(FullBackup.Read(new MemoryStream(Backup(emptyStore))));
        Assert.Equal(7, store.Library.Entries.Count);

        var safety = Assert.Single(store.ListFullBackups());
        Assert.Equal("before-restore", safety.Reason);
        Assert.True((DateTime.Now - safety.Time).Duration() < TimeSpan.FromMinutes(1));
        Assert.DoesNotContain(store.ListBackups(), b => b.Path == safety.Path); // 詞庫備份清單不含 zip
        using var stream = File.OpenRead(safety.Path);
        var previous = FullBackup.Read(stream);
        Assert.Equal(before, previous.Library.Entries.Count);
    }

    [Fact]
    public void SafetyBackups_ArePruned()
    {
        using var dir = new TempDir();
        var store = AppStore.Open(new DataPaths(dir.Path), new EmptySeed());
        for (var i = 0; i < AppStore.MaxSafetyFullBackups + 3; i++) store.CreateSafetyFullBackup("test");
        Assert.Equal(AppStore.MaxSafetyFullBackups, Directory.GetFiles(store.Paths.BackupDir, "full_*.zip").Length);
    }

    static byte[] Zip(params (string Name, string Content)[] files)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (name, content) in files)
            {
                using var s = zip.CreateEntry(name).Open();
                s.Write(Encoding.UTF8.GetBytes(content));
            }
        return ms.ToArray();
    }

    const string Manifest = """{ "format": "CharacterPromptGenerator.FullBackup", "schemaVersion": 1 }""";
    const string GoodLibrary = """{ "schemaVersion": 1, "categories": [ { "id": "c", "name": "分類" } ], "entries": [ { "id": "e", "categoryId": "c", "text": "詞" } ] }""";

    static string ReadError(byte[] bytes) =>
        Assert.Throws<LibraryValidationException>(() => FullBackup.Read(new MemoryStream(bytes))).Message;

    [Fact]
    public void NotAZip_IsRejected() =>
        Assert.Equal(L.T("FullBackup_NotBackup"), ReadError(Encoding.UTF8.GetBytes(GoodLibrary)));

    [Fact]
    public void ZipWithoutManifest_IsRejected() =>
        Assert.Equal(L.T("FullBackup_NotBackup"), ReadError(Zip(("library.json", GoodLibrary))));

    [Fact]
    public void NewerBackupFormat_IsRejected() =>
        Assert.Contains("2", ReadError(Zip(("manifest.json", Manifest.Replace("1 }", "2 }")), ("library.json", GoodLibrary))));

    [Fact]
    public void NewerLibrarySchema_IsRejected() =>
        Assert.Equal(L.F("Store_NewerSchema", 9, 1), ReadError(Zip(("manifest.json", Manifest), ("library.json", GoodLibrary.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 9")))));

    [Fact]
    public void MissingLibrary_IsRejected() =>
        Assert.Equal(L.F("FullBackup_Missing", "library.json"), ReadError(Zip(("manifest.json", Manifest))));

    [Fact]
    public void CorruptOptionalFile_RejectsWholeBackup() =>
        Assert.Contains("history.json", ReadError(Zip(("manifest.json", Manifest), ("library.json", GoodLibrary), ("history.json", "{ broken"))));

    [Fact]
    public void MinimalBackup_WithOnlyLibrary_IsAccepted()
    {
        var content = FullBackup.Read(new MemoryStream(Zip(("manifest.json", Manifest), ("library.json", GoodLibrary))));
        Assert.Equal("詞", Assert.Single(content.Library.Entries).Text);
        Assert.Empty(content.Presets);
        Assert.Empty(content.History);
        Assert.Null(content.Settings);
    }

    [Fact]
    public void FailedRead_DoesNotTouchCurrentData()
    {
        using var dir = new TempDir();
        var store = Populated(dir);
        var json = File.ReadAllText(store.Paths.LibraryFile);
        ReadError(Zip(("manifest.json", Manifest), ("library.json", "{ nope")));
        Assert.Equal(json, File.ReadAllText(store.Paths.LibraryFile));
    }
}
