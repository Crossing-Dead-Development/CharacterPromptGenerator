using CharacterPromptGenerator.Core.Drawing;
using CharacterPromptGenerator.Core.Storage;

namespace CharacterPromptGenerator.Tests;

/// <summary>行動版 Phase 4：歷史紀錄與收藏的管理。</summary>
public class HistoryManagerTests
{
    static (AppStore Store, HistoryManager History, DrawSession Draw, TempDir Dir) Create(int draws)
    {
        var dir = new TempDir();
        var store = AppStore.Open(new DataPaths(dir.Path), new EmptySeed());
        store.ReplaceLibrary(Lib.Small(30), "test");
        var draw = new DrawSession(store, new DrawEngine(new Random(5)));
        draw.SetDrawSetting("A", true, 2);
        for (var i = 0; i < draws; i++) draw.GenerateAll();
        return (store, new HistoryManager(store), draw, dir);
    }

    static AppStore Reopen(AppStore store) => AppStore.Open(store.Paths, new EmptySeed());

    [Fact]
    public void List_NewestFirst_AndFavoritesFilter()
    {
        var (store, h, _, dir) = Create(3);
        using var _d = dir;
        var newest = store.History[0];
        Assert.True(h.ToggleFavorite(store.History[2].Id));
        Assert.Equal(newest.Id, h.List(false)[0].Id);
        Assert.Equal(store.History[2].Id, Assert.Single(h.List(true)).Id);
    }

    [Fact]
    public void ToggleFavorite_Persists()
    {
        var (store, h, _, dir) = Create(1);
        using var _d = dir;
        var id = store.History[0].Id;
        h.ToggleFavorite(id);
        Assert.True(Reopen(store).History.Single().Favorite);
        h.ToggleFavorite(id);
        Assert.False(Reopen(store).History.Single().Favorite);
        Assert.False(h.ToggleFavorite("missing"));
    }

    [Fact]
    public void Delete_RemovesOnlyThatRecord()
    {
        var (store, h, _, dir) = Create(3);
        using var _d = dir;
        var victim = store.History[1].Id;
        Assert.True(h.Delete(victim));
        var reopened = Reopen(store);
        Assert.Equal(2, reopened.History.Count);
        Assert.DoesNotContain(reopened.History, r => r.Id == victim);
        Assert.False(h.Delete(victim));
    }

    [Fact]
    public void ClearNonFavorites_KeepsFavorites()
    {
        var (store, h, _, dir) = Create(4);
        using var _d = dir;
        var fav = store.History[3].Id;
        h.ToggleFavorite(fav);
        Assert.Equal(3, h.ClearNonFavorites());
        Assert.Equal(fav, Assert.Single(Reopen(store).History).Id);
        Assert.Equal(0, h.ClearNonFavorites());
    }

    [Fact]
    public void FailedSave_RollsBackMemory()
    {
        var (store, h, _, dir) = Create(2);
        using var _d = dir;
        var ids = store.History.Select(r => r.Id).ToList();
        // history.json 變成資料夾時無法寫入：模擬儲存空間或權限錯誤
        File.Delete(store.Paths.HistoryFile);
        Directory.CreateDirectory(store.Paths.HistoryFile);

        Assert.ThrowsAny<Exception>(() => h.Delete(ids[0]));
        Assert.Equal(ids, store.History.Select(r => r.Id));
        Assert.ThrowsAny<Exception>(() => h.ToggleFavorite(ids[1]));
        Assert.False(store.History[1].Favorite);
        Assert.ThrowsAny<Exception>(() => h.ClearNonFavorites());
        Assert.Equal(ids, store.History.Select(r => r.Id));
    }

    [Fact]
    public void Snapshot_IsUnaffectedByLaterLibraryChanges()
    {
        var (store, _, draw, dir) = Create(1);
        using var _d = dir;
        var texts = store.History[0].Items.Select(i => i.Text).ToList();
        store.MutateLibrary(lib => lib.Entries.Clear());
        Assert.Equal(texts, Reopen(store).History[0].Items.Select(i => i.Text));

        // 從歷史載入後仍能顯示與複製，即使詞條已經不存在
        draw.LoadSaved(store.History[0]);
        Assert.All(texts, t => Assert.Contains(t, draw.ToText()));
    }
}
