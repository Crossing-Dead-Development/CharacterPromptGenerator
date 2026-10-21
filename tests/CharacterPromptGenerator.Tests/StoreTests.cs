using CharacterPromptGenerator.Core.Drawing;
using CharacterPromptGenerator.Core.Import;
using CharacterPromptGenerator.Core.Library;
using CharacterPromptGenerator.Core.Storage;

namespace CharacterPromptGenerator.Tests;

public class StoreTests
{
    [Fact]
    public void FirstRun_SeedsSampleLibrary_WithEnoughData()
    {
        using var dir = new TempDir();
        var store = AppStore.Open(new DataPaths(dir.Path), new FileSeed());
        Assert.True(store.FirstRun);
        Assert.True(store.Library.Entries.Count >= 2000);
        Assert.Equal(21, store.Library.Categories.Count);
        Assert.All(store.Library.Categories, c => Assert.True(store.Library.Entries.Count(e => e.CategoryId == c.Id) >= 10));
        Assert.Equal(3, store.Presets.Count);
        Assert.Empty(LibraryValidator.Validate(store.Library));
    }

    [Fact]
    public void SecondRun_DoesNotOverwriteUserChanges_AndNewEntryIsDrawable()
    {
        using var dir = new TempDir();
        var paths = new DataPaths(dir.Path);
        var store = AppStore.Open(paths, new FileSeed());
        var catId = store.Library.Categories[0].Id;
        store.MutateLibrary(lib => LibraryEditor.AddEntry(lib, catId, "我的新詞 🧪", "🧪"));
        store.MutateLibrary(lib => LibraryEditor.DeleteEntries(lib, lib.Entries.Where(e => e.CategoryId == catId && e.Text != "我的新詞 🧪").Select(e => e.Id).ToList()));

        var reopened = AppStore.Open(paths, new FileSeed());
        Assert.False(reopened.FirstRun);
        var only = Assert.Single(reopened.Library.Entries, e => e.CategoryId == catId);
        Assert.Equal("我的新詞 🧪", only.Text);
        Assert.Equal("🧪", only.Emoji);

        var drawn = new DrawEngine(new Random(1)).Generate(reopened.Library, [new DrawRequest(catId, 1)]);
        Assert.Equal("我的新詞 🧪", drawn.Slots.Single().Text);
    }

    [Fact]
    public void LibraryFile_StoresChineseAndEmojiReadably_AndRoundTrips()
    {
        using var dir = new TempDir();
        var paths = new DataPaths(dir.Path);
        var store = AppStore.Open(paths, new FileSeed());
        const string tricky = "家庭 👨‍👩‍👧 \"引號\" \\ 反斜線";
        store.MutateLibrary(lib => LibraryEditor.AddEntry(lib, lib.Categories[0].Id, tricky, "👍🏽", "第一行\n第二行"));

        var raw = File.ReadAllText(paths.LibraryFile);
        Assert.Contains("🕯️", raw);
        Assert.Contains("👨‍👩‍👧", raw);
        Assert.DoesNotContain("\\uD83D", raw);

        var e = AppStore.Open(paths, new FileSeed()).Library.Entries.Single(x => x.Emoji == "👍🏽");
        Assert.Equal(tricky, e.Text);
        Assert.Equal("第一行\n第二行", e.Notes);
    }

    [Fact]
    public void FailedMutation_LeavesLibraryAndFileUntouched()
    {
        using var dir = new TempDir();
        var paths = new DataPaths(dir.Path);
        var store = AppStore.Open(paths, new FileSeed());
        var before = File.ReadAllText(paths.LibraryFile);
        var count = store.Library.Entries.Count;
        var catId = store.Library.Categories[0].Id;
        var existing = store.Library.Entries.First(e => e.CategoryId == catId).Text;

        Assert.Throws<LibraryValidationException>(() =>
            store.MutateLibrary(lib =>
            {
                LibraryEditor.AddEntry(lib, catId, "會被回滾的詞");
                LibraryEditor.AddEntry(lib, catId, existing); // 重複 → 丟例外
            }));
        Assert.Equal(count, store.Library.Entries.Count);
        Assert.Equal(before, File.ReadAllText(paths.LibraryFile));
    }

    [Fact]
    public void CorruptLibrary_RecoversFromBakAndKeepsCorruptCopy()
    {
        using var dir = new TempDir();
        var paths = new DataPaths(dir.Path);
        var store = AppStore.Open(paths, new FileSeed());
        store.MutateLibrary(lib => LibraryEditor.AddEntry(lib, lib.Categories[0].Id, "第一次修改"));
        store.MutateLibrary(lib => LibraryEditor.AddEntry(lib, lib.Categories[0].Id, "第二次修改"));
        File.WriteAllText(paths.LibraryFile, "{ 壞掉了");

        var reopened = AppStore.Open(paths, new FileSeed());
        Assert.Contains(reopened.Library.Entries, e => e.Text == "第一次修改");
        Assert.NotEmpty(Directory.GetFiles(dir.Path, "library.corrupt-*.json"));
        Assert.NotEmpty(reopened.LoadMessages);
    }

    [Fact]
    public void NewerSchemaVersion_RefusesToOpen()
    {
        using var dir = new TempDir();
        var paths = new DataPaths(dir.Path);
        File.WriteAllText(paths.LibraryFile, """{"schemaVersion": 99, "categories": [], "entries": []}""");
        Assert.Throws<StoreLoadException>(() => AppStore.Open(paths, new FileSeed()));
        Assert.Contains("99", File.ReadAllText(paths.LibraryFile));
    }

    [Fact]
    public void OrphanEntries_AreRescuedNotDropped()
    {
        using var dir = new TempDir();
        var paths = new DataPaths(dir.Path);
        File.WriteAllText(paths.LibraryFile, """
            {"schemaVersion":1,"categories":[{"id":"c1","name":"A"}],
             "entries":[{"id":"e1","categoryId":"c1","text":"ok"},{"id":"e2","categoryId":"ghost","text":"孤兒"}]}
            """);
        var store = AppStore.Open(paths, new FileSeed());
        var orphan = store.Library.Entries.Single(e => e.Text == "孤兒");
        Assert.NotNull(store.Library.FindCategory(orphan.CategoryId));
        Assert.Empty(LibraryValidator.Validate(store.Library));
    }

    [Fact]
    public void ApplyImport_BacksUpAndIsImmediatelyAvailable()
    {
        using var dir = new TempDir();
        var paths = new DataPaths(dir.Path);
        var store = AppStore.Open(paths, new FileSeed());
        var doc = ImportReader.ReadTxt("[匯入的新分類]\n🧵 縫紉機\n");
        var plan = ImportPlanner.Build(store.Library, doc, new ImportOptions());
        store.ApplyImport(plan);

        Assert.Contains(store.ListBackups(), b => b.Reason == "before-import");
        var cat = store.Library.Categories.Single(c => c.Name == "匯入的新分類");
        var drawn = new DrawEngine().Generate(store.Library, [new DrawRequest(cat.Id, 1)]);
        Assert.Equal("🧵 縫紉機", drawn.Slots.Single().Text);
        Assert.Contains(AppStore.Open(paths, new FileSeed()).Library.Entries, e => e.Text == "🧵 縫紉機");
    }

    [Fact]
    public void StalePlan_IsRejected()
    {
        using var dir = new TempDir();
        var store = AppStore.Open(new DataPaths(dir.Path), new FileSeed());
        var plan = ImportPlanner.Build(store.Library, ImportReader.ReadTxt("[X]\nY\n"), new ImportOptions());
        store.MutateLibrary(lib => LibraryEditor.AddCategory(lib, "期間的修改"));
        Assert.Throws<LibraryValidationException>(() => store.ApplyImport(plan));
        Assert.DoesNotContain(store.Library.Categories, c => c.Name == "X");
    }

    [Fact]
    public void BackupAndRestore_Works()
    {
        using var dir = new TempDir();
        var store = AppStore.Open(new DataPaths(dir.Path), new FileSeed());
        var count = store.Library.Entries.Count;
        var backup = store.CreateManualBackup();
        store.MutateLibrary(lib => LibraryEditor.DeleteEntries(lib, lib.Entries.Select(e => e.Id).ToList()));
        Assert.Empty(store.Library.Entries);
        store.RestoreBackup(backup);
        Assert.Equal(count, store.Library.Entries.Count);
        Assert.Contains(store.ListBackups(), b => b.Reason == "before-restore");
    }

    [Fact]
    public void History_KeepsSnapshotAfterEntryDeleted()
    {
        using var dir = new TempDir();
        var paths = new DataPaths(dir.Path);
        var store = AppStore.Open(paths, new FileSeed());
        var catId = store.Library.Categories[0].Id;
        var slots = new DrawEngine(new Random(1)).Generate(store.Library, [new DrawRequest(catId, 2)]).Slots;
        var snap = PromptFormatter.Snapshot(slots, DateTime.Now);
        snap.Favorite = true;
        store.AddHistory(snap);
        store.MutateLibrary(lib => LibraryEditor.DeleteCategory(lib, catId));

        var reopened = AppStore.Open(paths, new FileSeed());
        var saved = Assert.Single(reopened.History);
        Assert.True(saved.Favorite);
        Assert.Equal(slots.Select(s => s.Text), saved.Items.Select(i => i.Text));
        Assert.Contains(slots[0].Text, PromptFormatter.ToText(saved));
    }

    [Fact]
    public void History_PrunesOnlyNonFavorites()
    {
        using var dir = new TempDir();
        var store = AppStore.Open(new DataPaths(dir.Path), new EmptySeed());
        store.Settings.HistoryLimit = 10;
        var fav = PromptFormatter.Snapshot([], DateTime.Now);
        fav.Favorite = true;
        store.AddHistory(fav);
        for (var i = 0; i < 30; i++) store.AddHistory(PromptFormatter.Snapshot([], DateTime.Now));
        Assert.Equal(11, store.History.Count);
        Assert.Contains(fav, store.History);
    }

    [Fact]
    public void DeleteCategory_RemovesItsEntries_NoOrphans()
    {
        var lib = Lib.Small(4);
        var removed = LibraryEditor.DeleteCategory(lib, "A");
        Assert.Equal(4, removed);
        Assert.Empty(LibraryValidator.Validate(lib));
    }

    [Fact]
    public void BulkAddPreview_FlagsDuplicates()
    {
        var lib = Lib.Small(2);
        var p = LibraryEditor.PreviewBulkAdd(lib, "A", "詞1\n新詞\n\n 新詞 \n另一個\n");
        Assert.Equal(["新詞", "另一個"], p.ToAdd);
        Assert.Equal(["詞1"], p.DuplicateExisting);
        Assert.Equal(["新詞"], p.DuplicateInInput);
    }
}
