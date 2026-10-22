using CharacterPromptGenerator.Core.Models;
using CharacterPromptGenerator.Core.Storage;

namespace CharacterPromptGenerator.Core.Drawing;

/// <summary>
/// 歷史紀錄與收藏的修改。每個動作都會存檔；存檔失敗時把記憶體中的紀錄還原成動作之前的樣子，
/// 再把例外丟給呼叫端顯示。紀錄本身是文字快照，與詞庫無關。
/// </summary>
public sealed class HistoryManager(AppStore store)
{
    public IReadOnlyList<SavedResult> All => store.History;

    public SavedResult? Find(string id) => store.History.FirstOrDefault(h => h.Id == id);

    /// <param name="favoritesOnly">true 時只回傳收藏。順序與檔案相同（新的在前）。</param>
    public List<SavedResult> List(bool favoritesOnly) =>
        store.History.Where(h => !favoritesOnly || h.Favorite).ToList();

    public bool ToggleFavorite(string id)
    {
        var record = Find(id);
        if (record is null) return false;
        record.Favorite = !record.Favorite;
        Save(() => record.Favorite = !record.Favorite);
        return true;
    }

    public bool Delete(string id)
    {
        var idx = store.History.FindIndex(h => h.Id == id);
        if (idx < 0) return false;
        var record = store.History[idx];
        store.History.RemoveAt(idx);
        Save(() => store.History.Insert(idx, record));
        return true;
    }

    /// <summary>清除所有未收藏的紀錄，回傳清除的筆數。收藏永遠保留。</summary>
    public int ClearNonFavorites()
    {
        var before = store.History.ToList();
        var removed = store.History.RemoveAll(h => !h.Favorite);
        if (removed == 0) return 0;
        Save(() =>
        {
            store.History.Clear();
            store.History.AddRange(before);
        });
        return removed;
    }

    void Save(Action rollback)
    {
        try
        {
            store.SaveHistory();
        }
        catch
        {
            rollback();
            throw;
        }
    }
}
