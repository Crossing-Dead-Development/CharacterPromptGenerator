using CharacterPromptGenerator.Core.Library;
using CharacterPromptGenerator.Core.Localization;
using CharacterPromptGenerator.Core.Models;
using CharacterPromptGenerator.Core.Text;

namespace CharacterPromptGenerator.Core.Import;

public enum ImportMode
{
    /// <summary>合併：保留所有現有資料，只新增不重複的詞條。</summary>
    Merge,
    /// <summary>取代：整個詞庫換成檔案內容。</summary>
    Replace,
}

public sealed class ImportOptions
{
    public ImportMode Mode { get; init; } = ImportMode.Merge;
    /// <summary>合併時，遇到不存在的分類是否建立。不建立時，該分類的詞條會被略過。</summary>
    public bool AllowNewCategories { get; init; } = true;
}

public sealed record PlannedCategory(string Id, string Name, int EntryCount);
public sealed record PlannedEntry(int Line, string CategoryName, string Text, string? Emoji, string Notes, string TagsText, bool InNewCategory);
public sealed record SkippedRow(int Line, string CategoryName, string Text, string Reason);

/// <summary>匯入預覽與結果。ResultLibrary 是套用後的完整詞庫，套用時直接整份替換（原子操作）。</summary>
public sealed class ImportPlan
{
    public ImportMode Mode { get; init; }
    public required LibraryData BaseLibrary { get; init; }
    public required LibraryData ResultLibrary { get; init; }
    public List<PlannedCategory> NewCategories { get; } = new();
    public List<PlannedEntry> Added { get; } = new();
    public List<SkippedRow> Skipped { get; } = new();
    public List<ImportIssue> Errors { get; } = new();
    public List<ImportIssue> Warnings { get; } = new();
    public int IdConflictsResolved { get; set; }
    /// <summary>取代模式會移除的現有詞條數。</summary>
    public int RemovedEntries { get; set; }
    public string? BlockReason { get; set; }
    public bool CanApply => BlockReason is null;
}

public static class ImportPlanner
{
    public static ImportPlan Build(LibraryData current, ImportDocument doc, ImportOptions options)
    {
        var mode = options.Mode;
        if (doc.IsFatal)
            return new ImportPlan { Mode = mode, BaseLibrary = current, ResultLibrary = current, BlockReason = doc.FatalError };

        var result = mode == ImportMode.Merge ? current.Clone() : new LibraryData();
        var plan = new ImportPlan { Mode = mode, BaseLibrary = current, ResultLibrary = result };
        plan.Errors.AddRange(doc.Errors);
        plan.Warnings.AddRange(doc.Warnings);
        if (mode == ImportMode.Replace) plan.RemovedEntries = current.Entries.Count;

        var allowNew = mode == ImportMode.Replace || options.AllowNewCategories;
        var catIds = result.Categories.Select(c => c.Id).ToHashSet();
        var byName = new Dictionary<string, Category>();
        foreach (var c in result.Categories) byName.TryAdd(TextNormalizer.Key(c.Name), c);
        var byFileId = new Dictionary<string, Category>();
        var fileDefs = doc.Categories.Where(c => c.Id is not null).ToDictionary(c => c.Id!, c => c);
        var newCatIds = new HashSet<string>();
        var nextSort = result.Categories.Count == 0 ? 0 : result.Categories.Max(c => c.SortOrder) + 1;

        Category? Resolve(string? fileId, string name, ImportCategory? def)
        {
            if (fileId is not null && byFileId.TryGetValue(fileId, out var mapped)) return mapped;
            var key = TextNormalizer.Key(name);

            Category? target = null;
            if (fileId is not null && mode == ImportMode.Merge && result.FindCategory(fileId) is { } sameId)
            {
                target = sameId;
                if (TextNormalizer.Key(sameId.Name) != key)
                    plan.Warnings.Add(new ImportIssue(null, L.F("Plan_SameId", name, sameId.Name)));
            }
            else if (byName.TryGetValue(key, out var sameName))
            {
                target = sameName;
            }
            else
            {
                if (!allowNew) return null;
                var id = def?.Id;
                if (id is null || catIds.Contains(id))
                {
                    if (id is not null) plan.IdConflictsResolved++;
                    id = IdGenerator.NewUnique("cat", catIds);
                }
                target = new Category
                {
                    Id = id,
                    Name = name,
                    Description = def?.Description ?? "",
                    Enabled = def?.Enabled ?? true,
                    SortOrder = nextSort++,
                    DefaultDrawCount = def?.DefaultDrawCount ?? 1,
                };
                result.Categories.Add(target);
                catIds.Add(id);
                newCatIds.Add(id);
            }
            if (fileId is not null) byFileId[fileId] = target;
            byName.TryAdd(key, target);
            return target;
        }

        // 1. 檔案中定義的分類（含沒有詞條的空分類）
        var orderedDefs = doc.Categories
            .Select((c, i) => (c, i))
            .OrderBy(x => x.c.SortOrder ?? int.MaxValue).ThenBy(x => x.i)
            .Select(x => x.c);
        foreach (var def in orderedDefs) Resolve(def.Id, def.Name, def);

        // 2. 詞條
        var existingKeys = result.Entries.Select(e => (e.CategoryId, TextNormalizer.Key(e.Text))).ToHashSet();
        var fileKeys = new HashSet<(string, string)>();
        var entryIds = result.Entries.Select(e => e.Id).ToHashSet();

        foreach (var row in doc.Rows)
        {
            ImportCategory? def = row.CategoryId is not null && fileDefs.TryGetValue(row.CategoryId, out var d) ? d : null;
            var cat = Resolve(row.CategoryId, row.CategoryName, def);
            if (cat is null)
            {
                plan.Skipped.Add(new SkippedRow(row.Line, row.CategoryName, row.Text, L.T("Plan_SkipNoCategory")));
                continue;
            }

            var key = (cat.Id, TextNormalizer.Key(row.Text));
            if (fileKeys.Contains(key))
            {
                plan.Skipped.Add(new SkippedRow(row.Line, cat.Name, row.Text, L.T("Plan_SkipDupInFile")));
                continue;
            }
            fileKeys.Add(key);
            if (existingKeys.Contains(key))
            {
                plan.Skipped.Add(new SkippedRow(row.Line, cat.Name, row.Text, L.T("Plan_SkipDupInLibrary")));
                continue;
            }

            var id = row.EntryId;
            if (id is null || entryIds.Contains(id))
            {
                if (id is not null) plan.IdConflictsResolved++;
                id = IdGenerator.NewUnique("e", entryIds);
            }
            entryIds.Add(id);
            existingKeys.Add(key);

            var entry = new Entry
            {
                Id = id,
                CategoryId = cat.Id,
                Text = TextNormalizer.Clean(row.Text),
                Emoji = TextNormalizer.CleanOptional(row.Emoji),
                Notes = row.Notes ?? "",
                Tags = TextNormalizer.CleanTags(row.Tags),
                Enabled = row.Enabled,
            };
            result.Entries.Add(entry);
            plan.Added.Add(new PlannedEntry(row.Line, cat.Name, entry.Text, entry.Emoji, entry.Notes,
                TextNormalizer.JoinTags(entry.Tags), newCatIds.Contains(cat.Id)));
        }

        foreach (var id in newCatIds)
        {
            var c = result.FindCategory(id)!;
            plan.NewCategories.Add(new PlannedCategory(c.Id, c.Name, result.Entries.Count(e => e.CategoryId == id)));
        }
        if (plan.IdConflictsResolved > 0)
            plan.Warnings.Add(new ImportIssue(null, L.F("Plan_IdConflicts", plan.IdConflictsResolved)));

        // 3. 套用前的完整性檢查
        var validation = LibraryValidator.Validate(result);
        if (validation.Count > 0)
            plan.BlockReason = L.T("Plan_Invalid") + string.Join("\n", validation.Take(10));
        else if (mode == ImportMode.Replace && result.Entries.Count == 0)
            plan.BlockReason = L.T("Plan_ReplaceEmpty");
        else if (mode == ImportMode.Merge && plan.Added.Count == 0 && plan.NewCategories.Count == 0)
            plan.BlockReason = L.T("Plan_NothingNew");
        return plan;
    }
}
