using CharacterPromptGenerator.Core.Localization;
using CharacterPromptGenerator.Core.Models;
using CharacterPromptGenerator.Core.Text;

namespace CharacterPromptGenerator.Core.Library;

public sealed class LibraryValidationException(string message) : Exception(message);

public static class LibraryValidator
{
    /// <summary>嚴格檢查：寫入前用。回傳所有問題，空清單代表通過。</summary>
    public static List<string> Validate(LibraryData lib)
    {
        var errors = new List<string>();
        if (lib.SchemaVersion != LibraryData.CurrentSchemaVersion)
            errors.Add(L.F("Valid_Schema", LibraryData.CurrentSchemaVersion, lib.SchemaVersion));

        var catIds = new HashSet<string>();
        var catNames = new HashSet<string>();
        foreach (var c in lib.Categories)
        {
            if (string.IsNullOrWhiteSpace(c.Id)) errors.Add(L.F("Valid_CategoryNoId", c.Name));
            else if (!catIds.Add(c.Id)) errors.Add(L.F("Valid_CategoryDupId", c.Id));
            if (string.IsNullOrWhiteSpace(c.Name)) errors.Add(L.F("Valid_CategoryNoName", c.Id));
            else if (!catNames.Add(TextNormalizer.Key(c.Name))) errors.Add(L.F("Valid_CategoryDupName", c.Name));
            if (c.DefaultDrawCount < 0) errors.Add(L.F("Valid_CategoryNegative", c.Name));
        }

        var entryIds = new HashSet<string>();
        foreach (var e in lib.Entries)
        {
            if (string.IsNullOrWhiteSpace(e.Id)) errors.Add(L.F("Valid_EntryNoId", e.Text));
            else if (!entryIds.Add(e.Id)) errors.Add(L.F("Valid_EntryDupId", e.Id));
            if (!catIds.Contains(e.CategoryId)) errors.Add(L.F("Valid_EntryBadCategory", e.Text, e.CategoryId));
            if (string.IsNullOrWhiteSpace(e.Text)) errors.Add(L.F("Valid_EntryEmpty", e.Id));
            if (e.Tags is null) errors.Add(L.F("Valid_EntryNullTags", e.Id));
        }
        return errors;
    }

    public static void EnsureValid(LibraryData lib)
    {
        var errors = Validate(lib);
        if (errors.Count > 0)
            throw new LibraryValidationException(L.T("Valid_Failed") + string.Join("\n", errors.Take(20)));
    }

    /// <summary>
    /// 載入時的寬鬆修復：補 ID、修正孤兒詞條（移到修復分類，而不是刪除）。
    /// 回傳修復紀錄，原資料就地修改。
    /// </summary>
    public static List<string> Repair(LibraryData lib)
    {
        var notes = new List<string>();
        lib.Categories ??= new();
        lib.Entries ??= new();
        lib.Categories.RemoveAll(c => c is null);
        lib.Entries.RemoveAll(e => e is null);

        var catIds = new HashSet<string>();
        foreach (var c in lib.Categories)
        {
            c.Name = TextNormalizer.Clean(c.Name);
            c.Description ??= "";
            if (c.Name.Length == 0) { c.Name = L.T("Repair_Unnamed"); notes.Add(L.T("Repair_UnnamedNote")); }
            if (string.IsNullOrWhiteSpace(c.Id) || !catIds.Add(c.Id))
            {
                var old = c.Id;
                c.Id = IdGenerator.NewUnique("cat", catIds);
                catIds.Add(c.Id);
                if (!string.IsNullOrWhiteSpace(old))
                {
                    // 重複的分類 ID：之後指向舊 ID 的詞條仍歸第一個分類，避免改動使用者資料過多
                    notes.Add(L.F("Repair_DupId", c.Name, old, c.Id));
                }
            }
            if (c.DefaultDrawCount < 0) c.DefaultDrawCount = 0;
        }

        // 分類名稱重複時加後綴，保證之後以名稱比對匯入不會模稜兩可
        var names = new HashSet<string>();
        foreach (var c in lib.OrderedCategories().ToList())
        {
            var baseName = c.Name;
            var n = 2;
            while (!names.Add(TextNormalizer.Key(c.Name))) c.Name = $"{baseName} ({n++})";
            if (c.Name != baseName) notes.Add(L.F("Repair_DupName", baseName, c.Name));
        }

        var entryIds = new HashSet<string>();
        Category? rescue = null;
        var removedEmpty = 0;
        foreach (var e in lib.Entries.ToList())
        {
            e.Text = TextNormalizer.Clean(e.Text);
            if (e.Text.Length == 0) { lib.Entries.Remove(e); removedEmpty++; continue; }
            e.Emoji = TextNormalizer.CleanOptional(e.Emoji);
            e.Notes ??= "";
            e.Tags = TextNormalizer.CleanTags(e.Tags ?? new List<string>());
            if (string.IsNullOrWhiteSpace(e.Id) || !entryIds.Add(e.Id))
            {
                e.Id = IdGenerator.NewUnique("e", entryIds);
                entryIds.Add(e.Id);
            }
            if (!catIds.Contains(e.CategoryId))
            {
                if (rescue is null)
                {
                    rescue = new Category
                    {
                        Id = IdGenerator.NewUnique("cat", catIds),
                        Name = UniqueName(lib, L.T("Repair_RescueName")),
                        Description = L.T("Repair_RescueDescription"),
                        Enabled = false,
                        SortOrder = lib.Categories.Count == 0 ? 0 : lib.Categories.Max(c => c.SortOrder) + 1,
                        DefaultDrawCount = 0,
                    };
                    lib.Categories.Add(rescue);
                    catIds.Add(rescue.Id);
                }
                e.CategoryId = rescue.Id;
            }
        }
        if (removedEmpty > 0) notes.Add(L.F("Repair_RemovedEmpty", removedEmpty));
        if (rescue is not null)
            notes.Add(L.F("Repair_Rescued", lib.Entries.Count(x => x.CategoryId == rescue.Id), rescue.Name));
        return notes;
    }

    public static string UniqueName(LibraryData lib, string name)
    {
        var existing = lib.Categories.Select(c => TextNormalizer.Key(c.Name)).ToHashSet();
        var candidate = name;
        var n = 2;
        while (existing.Contains(TextNormalizer.Key(candidate))) candidate = $"{name} ({n++})";
        return candidate;
    }
}
