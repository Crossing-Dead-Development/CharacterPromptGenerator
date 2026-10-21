using CharacterPromptGenerator.Core.Localization;
using CharacterPromptGenerator.Core.Models;
using CharacterPromptGenerator.Core.Text;

namespace CharacterPromptGenerator.Core.Library;

/// <summary>
/// 對 LibraryData 的所有編輯操作。這些方法會直接修改傳入的物件，
/// 所以呼叫端（AppStore.MutateLibrary）應該傳入複本，成功儲存後才換掉正式資料。
/// 不合法的操作丟出 LibraryValidationException，訊息可直接顯示給使用者。
/// </summary>
public static class LibraryEditor
{
    // ── 分類 ───────────────────────────────────────────────

    public static Category AddCategory(LibraryData lib, string name, string description = "", int defaultDrawCount = 1)
    {
        name = TextNormalizer.Clean(name);
        if (name.Length == 0) throw new LibraryValidationException(L.T("Lib_CategoryNameEmpty"));
        EnsureCategoryNameFree(lib, name, null);
        var cat = new Category
        {
            Id = IdGenerator.NewUnique("cat", lib.Categories.Select(c => c.Id).ToHashSet()),
            Name = name,
            Description = description?.Trim() ?? "",
            Enabled = true,
            SortOrder = lib.Categories.Count == 0 ? 0 : lib.Categories.Max(c => c.SortOrder) + 1,
            DefaultDrawCount = Math.Max(0, defaultDrawCount),
        };
        lib.Categories.Add(cat);
        return cat;
    }

    public static void UpdateCategory(LibraryData lib, string id, string name, string description, bool enabled, int defaultDrawCount)
    {
        var cat = lib.FindCategory(id) ?? throw new LibraryValidationException(L.T("Lib_CategoryNotFound"));
        name = TextNormalizer.Clean(name);
        if (name.Length == 0) throw new LibraryValidationException(L.T("Lib_CategoryNameEmpty"));
        EnsureCategoryNameFree(lib, name, id);
        if (defaultDrawCount < 0) throw new LibraryValidationException(L.T("Lib_NegativeCount"));
        cat.Name = name;
        cat.Description = description?.Trim() ?? "";
        cat.Enabled = enabled;
        cat.DefaultDrawCount = defaultDrawCount;
    }

    /// <summary>刪除分類及其所有詞條（不留孤兒），回傳一併刪除的詞條數。</summary>
    public static int DeleteCategory(LibraryData lib, string id)
    {
        var cat = lib.FindCategory(id) ?? throw new LibraryValidationException(L.T("Lib_CategoryNotFound"));
        var removed = lib.Entries.RemoveAll(e => e.CategoryId == id);
        lib.Categories.Remove(cat);
        NormalizeSortOrder(lib);
        return removed;
    }

    public static void MoveCategory(LibraryData lib, string id, int delta)
    {
        var ordered = lib.OrderedCategories().ToList();
        var idx = ordered.FindIndex(c => c.Id == id);
        if (idx < 0) return;
        var target = Math.Clamp(idx + delta, 0, ordered.Count - 1);
        if (target == idx) return;
        var item = ordered[idx];
        ordered.RemoveAt(idx);
        ordered.Insert(target, item);
        for (var i = 0; i < ordered.Count; i++) ordered[i].SortOrder = i;
    }

    public static void NormalizeSortOrder(LibraryData lib)
    {
        var i = 0;
        foreach (var c in lib.OrderedCategories().ToList()) c.SortOrder = i++;
    }

    static void EnsureCategoryNameFree(LibraryData lib, string name, string? exceptId)
    {
        var key = TextNormalizer.Key(name);
        if (lib.Categories.Any(c => c.Id != exceptId && TextNormalizer.Key(c.Name) == key))
            throw new LibraryValidationException(L.F("Lib_CategoryExists", name));
    }

    // ── 詞條 ───────────────────────────────────────────────

    public static Entry AddEntry(LibraryData lib, string categoryId, string text, string? emoji = null,
        string? notes = null, IEnumerable<string>? tags = null, bool enabled = true)
    {
        if (lib.FindCategory(categoryId) is null) throw new LibraryValidationException(L.T("Lib_ChooseCategory"));
        text = TextNormalizer.Clean(text);
        if (text.Length == 0) throw new LibraryValidationException(L.T("Lib_EntryEmpty"));
        var dup = FindDuplicate(lib, categoryId, text, null);
        if (dup is not null) throw new LibraryValidationException(L.F("Lib_EntryDup", dup.Text));
        var entry = new Entry
        {
            Id = IdGenerator.NewUnique("e", lib.Entries.Select(e => e.Id).ToHashSet()),
            CategoryId = categoryId,
            Text = text,
            Emoji = TextNormalizer.CleanOptional(emoji),
            Notes = notes?.Trim() ?? "",
            Tags = TextNormalizer.CleanTags(tags ?? Array.Empty<string>()),
            Enabled = enabled,
        };
        lib.Entries.Add(entry);
        return entry;
    }

    public static void UpdateEntry(LibraryData lib, string entryId, string categoryId, string text, string? emoji,
        string? notes, IEnumerable<string>? tags, bool enabled)
    {
        var entry = lib.Entries.FirstOrDefault(e => e.Id == entryId) ?? throw new LibraryValidationException(L.T("Lib_EntryNotFound"));
        if (lib.FindCategory(categoryId) is null) throw new LibraryValidationException(L.T("Lib_TargetMissing"));
        text = TextNormalizer.Clean(text);
        if (text.Length == 0) throw new LibraryValidationException(L.T("Lib_EntryEmpty"));
        var dup = FindDuplicate(lib, categoryId, text, entryId);
        if (dup is not null) throw new LibraryValidationException(L.F("Lib_TargetDup", dup.Text));
        entry.CategoryId = categoryId;
        entry.Text = text;
        entry.Emoji = TextNormalizer.CleanOptional(emoji);
        entry.Notes = notes?.Trim() ?? "";
        entry.Tags = TextNormalizer.CleanTags(tags ?? Array.Empty<string>());
        entry.Enabled = enabled;
    }

    public static int DeleteEntries(LibraryData lib, IEnumerable<string> ids)
    {
        var set = ids.ToHashSet();
        return lib.Entries.RemoveAll(e => set.Contains(e.Id));
    }

    public static int SetEntriesEnabled(LibraryData lib, IEnumerable<string> ids, bool enabled)
    {
        var set = ids.ToHashSet();
        var n = 0;
        foreach (var e in lib.Entries.Where(e => set.Contains(e.Id)))
        {
            e.Enabled = enabled;
            n++;
        }
        return n;
    }

    /// <summary>把詞條移到另一分類；目標分類已有同名詞條者略過。</summary>
    public static (int Moved, int Skipped) MoveEntries(LibraryData lib, IEnumerable<string> ids, string targetCategoryId)
    {
        if (lib.FindCategory(targetCategoryId) is null) throw new LibraryValidationException(L.T("Lib_TargetMissing"));
        var set = ids.ToHashSet();
        int moved = 0, skipped = 0;
        foreach (var e in lib.Entries.Where(e => set.Contains(e.Id)).ToList())
        {
            if (e.CategoryId == targetCategoryId) continue;
            if (FindDuplicate(lib, targetCategoryId, e.Text, e.Id) is not null) { skipped++; continue; }
            e.CategoryId = targetCategoryId;
            moved++;
        }
        return (moved, skipped);
    }

    public static Entry? FindDuplicate(LibraryData lib, string categoryId, string text, string? exceptEntryId)
    {
        var key = TextNormalizer.Key(text);
        return lib.Entries.FirstOrDefault(e => e.CategoryId == categoryId && e.Id != exceptEntryId && TextNormalizer.Key(e.Text) == key);
    }

    // ── 多行貼上 ───────────────────────────────────────────

    public sealed record BulkAddPreview(List<string> ToAdd, List<string> DuplicateExisting, List<string> DuplicateInInput);

    public static BulkAddPreview PreviewBulkAdd(LibraryData lib, string categoryId, string multiline)
    {
        var existing = lib.Entries.Where(e => e.CategoryId == categoryId).Select(e => TextNormalizer.Key(e.Text)).ToHashSet();
        var seen = new HashSet<string>();
        var add = new List<string>();
        var dupExisting = new List<string>();
        var dupInput = new List<string>();
        foreach (var raw in multiline.Replace("\r\n", "\n").Split('\n'))
        {
            var text = TextNormalizer.Clean(raw);
            if (text.Length == 0) continue;
            var key = TextNormalizer.Key(text);
            if (existing.Contains(key)) dupExisting.Add(text);
            else if (!seen.Add(key)) dupInput.Add(text);
            else add.Add(text);
        }
        return new BulkAddPreview(add, dupExisting, dupInput);
    }

    public static int BulkAdd(LibraryData lib, string categoryId, IEnumerable<string> texts)
    {
        var n = 0;
        foreach (var t in texts)
        {
            if (FindDuplicate(lib, categoryId, t, null) is not null) continue;
            AddEntry(lib, categoryId, t);
            n++;
        }
        return n;
    }

    // ── 統計 ───────────────────────────────────────────────

    public static (int Total, int Drawable) CountEntries(LibraryData lib, Category cat)
    {
        int total = 0, enabled = 0;
        foreach (var e in lib.Entries)
        {
            if (e.CategoryId != cat.Id) continue;
            total++;
            if (e.Enabled) enabled++;
        }
        return (total, cat.Enabled ? enabled : 0);
    }
}
