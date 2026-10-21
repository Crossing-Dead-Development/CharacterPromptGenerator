using CharacterPromptGenerator.Core.Localization;
using CharacterPromptGenerator.Core.Models;
using CharacterPromptGenerator.Core.Text;

namespace CharacterPromptGenerator.Core.Drawing;

public sealed record DrawRequest(string CategoryId, int Count);

/// <summary>一個抽題結果。內容是快照，不會因詞庫之後被修改或刪除而改變。</summary>
public sealed class DrawSlot
{
    public string CategoryId { get; init; } = "";
    public string CategoryName { get; init; } = "";
    public string? EntryId { get; init; }
    public string Text { get; init; } = "";
    public string? Emoji { get; init; }
    public string Notes { get; init; } = "";
    public bool Locked { get; set; }

    public static DrawSlot From(Category cat, Entry e) => new()
    {
        CategoryId = cat.Id,
        CategoryName = cat.Name,
        EntryId = e.Id,
        Text = e.Text,
        Emoji = e.Emoji,
        Notes = e.Notes,
    };

    public DrawSlot Copy() => new()
    {
        CategoryId = CategoryId, CategoryName = CategoryName, EntryId = EntryId,
        Text = Text, Emoji = Emoji, Notes = Notes, Locked = Locked,
    };

    /// <summary>同分類內用來判斷「同一個詞」的鍵。</summary>
    internal string Key => TextNormalizer.Key(Text);
}

public sealed class DrawOutcome
{
    public List<DrawSlot> Slots { get; } = new();
    public List<string> Warnings { get; } = new();
}

public sealed class DrawEngine(Random rng)
{
    public DrawEngine() : this(Random.Shared) { }

    /// <summary>全部重新產生：不保留任何舊結果。</summary>
    public DrawOutcome Generate(LibraryData lib, IReadOnlyList<DrawRequest> requests) =>
        Reroll(lib, requests, Array.Empty<DrawSlot>());

    /// <summary>
    /// 重抽：鎖定的結果原地保留，其餘依需求數量補抽。
    /// <paramref name="onlyCategoryId"/> 不為 null 時只重抽該分類，其他分類維持原狀。
    /// </summary>
    public DrawOutcome Reroll(LibraryData lib, IReadOnlyList<DrawRequest> requests,
        IReadOnlyList<DrawSlot> current, string? onlyCategoryId = null)
    {
        var outcome = new DrawOutcome();
        var reqByCat = new Dictionary<string, int>();
        foreach (var r in requests)
            if (r.Count > 0) reqByCat[r.CategoryId] = r.Count; // 同分類重複的需求以最後一個為準

        // 輸出順序：詞庫分類順序，之後是已不存在於詞庫、但仍有鎖定結果的分類
        var order = new List<string>();
        foreach (var c in lib.OrderedCategories()) order.Add(c.Id);
        foreach (var s in current)
            if (!order.Contains(s.CategoryId)) order.Add(s.CategoryId);

        foreach (var catId in order)
        {
            var existing = current.Where(s => s.CategoryId == catId).ToList();
            if (onlyCategoryId is not null && catId != onlyCategoryId)
            {
                outcome.Slots.AddRange(existing.Select(s => s.Copy()));
                continue;
            }

            var cat = lib.FindCategory(catId);
            var target = cat is not null && cat.Enabled && reqByCat.TryGetValue(catId, out var n) ? n : 0;
            var locked = existing.Where(s => s.Locked).ToList();
            var need = Math.Max(0, target - locked.Count);

            var picks = new List<DrawSlot>();
            if (need > 0 && cat is not null)
            {
                var lockedIds = locked.Where(s => s.EntryId is not null).Select(s => s.EntryId!).ToHashSet();
                var lockedKeys = locked.Select(s => s.Key).ToHashSet();
                var previousIds = existing.Where(s => !s.Locked && s.EntryId is not null).Select(s => s.EntryId!).ToHashSet();

                // 候選：啟用中、未被鎖定佔用、且文字不重複（同分類若有重複文字只取一個）
                var seenKeys = new HashSet<string>(lockedKeys);
                var pool = new List<Entry>();
                foreach (var e in lib.Entries)
                {
                    if (e.CategoryId != catId || !e.Enabled || lockedIds.Contains(e.Id)) continue;
                    if (!seenKeys.Add(TextNormalizer.Key(e.Text))) continue;
                    pool.Add(e);
                }

                // 優先抽與上一輪不同的詞，不夠時才允許抽回上一輪的詞
                var fresh = pool.Where(e => !previousIds.Contains(e.Id)).ToList();
                var repeat = pool.Where(e => previousIds.Contains(e.Id)).ToList();
                Shuffle(fresh);
                Shuffle(repeat);
                foreach (var e in fresh.Concat(repeat).Take(need)) picks.Add(DrawSlot.From(cat, e));

                if (picks.Count < need)
                {
                    outcome.Warnings.Add(pool.Count == 0
                        ? L.F("Engine_NoEntries", cat.Name, need)
                        : L.F("Engine_NotEnough", cat.Name, need, pool.Count, picks.Count));
                }
            }

            // 鎖定項保留原位置，未鎖定的位置依序換成新結果，多出的新結果接在後面
            var queue = new Queue<DrawSlot>(picks);
            foreach (var s in existing)
            {
                if (s.Locked) outcome.Slots.Add(s.Copy());
                else if (queue.Count > 0) outcome.Slots.Add(queue.Dequeue());
            }
            outcome.Slots.AddRange(queue);
        }
        return outcome;
    }

    void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
