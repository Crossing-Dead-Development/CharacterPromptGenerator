using CharacterPromptGenerator.Core.Models;
using CharacterPromptGenerator.Core.Text;

namespace CharacterPromptGenerator.Core.Library;

public enum EntryStatusFilter { All, Enabled, Disabled }

/// <summary>
/// 詞條搜尋與篩選。建立時為每個詞條算好搜尋鍵（文字、Emoji、備註、標籤，NFC＋不分大小寫），
/// 之後每次輸入只做字串比對；詞庫修改後重新建立即可。
/// </summary>
public sealed class LibraryQuery
{
    readonly List<(Entry Entry, string Key)> _items;

    public LibraryQuery(LibraryData lib)
    {
        var order = lib.OrderedCategories().Select((c, i) => (c.Id, i)).ToDictionary(x => x.Id, x => x.i);
        // 依分類順序排列，同分類內保持詞庫中的原始順序
        _items = lib.Entries
            .Select((e, i) => (e, i))
            .OrderBy(x => order.GetValueOrDefault(x.e.CategoryId, int.MaxValue)).ThenBy(x => x.i)
            .Select(x => (x.e, SearchKey(x.e)))
            .ToList();
    }

    public int Total => _items.Count;

    public static string SearchKey(Entry e) =>
        TextNormalizer.Key($"{e.Text} {e.Emoji} {e.Notes} {TextNormalizer.JoinTags(e.Tags)}");

    /// <param name="categoryId">null 表示全部分類。</param>
    /// <param name="search">空白表示不篩選；比對規則與重複判斷相同（不分大小寫，多餘空白不影響）。</param>
    public List<Entry> Filter(string? categoryId, string? search, EntryStatusFilter status)
    {
        var key = TextNormalizer.Key(search);
        var result = new List<Entry>();
        foreach (var (e, k) in _items)
        {
            if (categoryId is not null && e.CategoryId != categoryId) continue;
            if (status == EntryStatusFilter.Enabled && !e.Enabled) continue;
            if (status == EntryStatusFilter.Disabled && e.Enabled) continue;
            if (key.Length > 0 && !k.Contains(key, StringComparison.Ordinal)) continue;
            result.Add(e);
        }
        return result;
    }
}
