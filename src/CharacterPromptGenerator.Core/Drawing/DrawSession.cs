using CharacterPromptGenerator.Core.Localization;
using CharacterPromptGenerator.Core.Models;
using CharacterPromptGenerator.Core.Storage;

namespace CharacterPromptGenerator.Core.Drawing;

/// <summary>
/// 一個畫面上的抽題狀態：目前結果、鎖定、對應的歷史紀錄與收藏。
/// 不碰任何 UI；需要使用者確認的動作（例如有鎖定時全部重抽）由呼叫端先問過再呼叫。
/// 寫入歷史失敗時例外會往外丟，但畫面上的結果已經更新，與 Windows 版行為一致。
/// </summary>
public sealed class DrawSession(AppStore store, DrawEngine engine)
{
    public const int MaxCount = 99;

    List<DrawSlot> _slots = new();

    public DrawSession(AppStore store) : this(store, new DrawEngine()) { }

    public AppStore Store { get; } = store;
    public IReadOnlyList<DrawSlot> Slots => _slots;
    public DateTime? GeneratedAt { get; private set; }
    public string? CurrentHistoryId { get; private set; }
    /// <summary>最近一次動作產生的提示（數量不足、沒有勾選分類、從歷史載入…）。</summary>
    public IReadOnlyList<string> Warnings { get; private set; } = Array.Empty<string>();

    public bool HasResult => _slots.Count > 0;
    public int LockedCount => _slots.Count(s => s.Locked);
    public bool IsFavorite => CurrentRecord?.Favorite == true;

    SavedResult? CurrentRecord =>
        CurrentHistoryId is null ? null : Store.History.FirstOrDefault(h => h.Id == CurrentHistoryId);

    // ── 抽題設定 ───────────────────────────────────────────

    /// <summary>依目前設定產生的抽題需求：勾選且數量大於 0 的分類。</summary>
    public List<DrawRequest> Requests() =>
        Store.Library.OrderedCategories()
            .Select(c => (c.Id, Setting: Store.GetDrawSetting(c)))
            .Where(x => x.Setting.Enabled && x.Setting.Count > 0)
            .Select(x => new DrawRequest(x.Id, Math.Min(x.Setting.Count, MaxCount)))
            .ToList();

    /// <summary>修改單一分類的勾選與數量並存檔。數量限制在 0–99。</summary>
    public void SetDrawSetting(string categoryId, bool enabled, int count)
    {
        Store.Settings.DrawSettings[categoryId] = new DrawSetting { Enabled = enabled, Count = Math.Clamp(count, 0, MaxCount) };
        Store.SaveSettings();
    }

    /// <summary>一次勾選或取消全部分類（數量不變），只存檔一次。</summary>
    public void SetAllEnabled(bool enabled)
    {
        foreach (var cat in Store.Library.Categories)
        {
            var s = Store.GetDrawSetting(cat);
            Store.Settings.DrawSettings[cat.Id] = new DrawSetting { Enabled = enabled, Count = s.Count };
        }
        Store.SaveSettings();
    }

    /// <summary>恢復預設：清除所有分類的設定，回到詞庫中各分類的預設抽取數量，並取消目前方案。</summary>
    public void ResetToDefaults()
    {
        Store.Settings.DrawSettings.Clear();
        Store.Settings.LastPresetId = null;
        Store.SaveSettings();
    }

    // ── 抽題 ───────────────────────────────────────────────

    /// <summary>全部重新產生，鎖定也會被清掉。有鎖定時呼叫端應先向使用者確認。</summary>
    public void GenerateAll() => Apply(engine.Generate(Store.Library, Requests()));

    /// <summary>重抽未鎖定的結果；還沒有結果時等同全部產生。</summary>
    public void RerollUnlocked() =>
        Apply(HasResult ? engine.Reroll(Store.Library, Requests(), _slots) : engine.Generate(Store.Library, Requests()));

    /// <summary>
    /// 只重抽單一分類。分類在設定中沒有勾選時，沿用結果中目前的數量重抽。
    /// 分類已從詞庫刪除時回傳 false，結果不變。
    /// </summary>
    public bool RerollCategory(string categoryId)
    {
        if (Store.Library.FindCategory(categoryId) is null)
        {
            Warnings = [L.T("Draw_CategoryDeleted")];
            return false;
        }
        var reqs = Requests();
        if (!reqs.Any(r => r.CategoryId == categoryId))
            reqs.Add(new DrawRequest(categoryId, Math.Max(1, _slots.Count(s => s.CategoryId == categoryId))));
        Apply(engine.Reroll(Store.Library, reqs, _slots, categoryId));
        return true;
    }

    void Apply(DrawOutcome outcome)
    {
        var warnings = new List<string>(outcome.Warnings);
        if (Requests().Count == 0 && outcome.Slots.Count == 0)
            warnings.Insert(0, L.T("Draw_NothingSelected"));
        Warnings = warnings;

        _slots = outcome.Slots;
        GeneratedAt = DateTime.Now;
        CurrentHistoryId = null;
        if (_slots.Count == 0) return;

        var snap = PromptFormatter.Snapshot(_slots, GeneratedAt.Value);
        CurrentHistoryId = snap.Id;
        Store.AddHistory(snap);
    }

    /// <summary>從歷史載入：所有結果都設為鎖定，之後重抽只會補上新增的分類。</summary>
    public void LoadSaved(SavedResult saved)
    {
        _slots = saved.Items.Select(i => new DrawSlot
        {
            CategoryId = i.CategoryId,
            CategoryName = i.CategoryName,
            EntryId = i.EntryId,
            Text = i.Text,
            Emoji = i.Emoji,
            Notes = i.Notes,
            Locked = true,
        }).ToList();
        GeneratedAt = saved.CreatedAt;
        CurrentHistoryId = saved.Id;
        Warnings = [L.T("Draw_LoadedFromHistory")];
    }

    public void UnlockAll()
    {
        foreach (var s in _slots) s.Locked = false;
    }

    /// <summary>結果依分類分組，順序與畫面顯示相同。分類名稱優先用詞庫中目前的名稱。</summary>
    public List<(string CategoryId, string CategoryName, List<DrawSlot> Slots)> Groups() =>
        _slots.GroupBy(s => s.CategoryId)
            .Select(g => (g.Key, Store.Library.FindCategory(g.Key)?.Name ?? g.First().CategoryName, g.ToList()))
            .ToList();

    public string ToText() => PromptFormatter.ToText(_slots, GeneratedAt);

    /// <summary>
    /// 切換目前結果的收藏。對應的歷史紀錄已被淘汰時重新建立一筆並收藏。
    /// 存檔失敗時還原狀態再丟出例外。沒有結果時回傳 false。
    /// </summary>
    public bool ToggleFavorite()
    {
        if (!HasResult) return false;
        var record = CurrentRecord;
        if (record is null)
        {
            record = PromptFormatter.Snapshot(_slots, GeneratedAt ?? DateTime.Now);
            record.Favorite = true;
            CurrentHistoryId = record.Id;
            Store.AddHistory(record);
            return true;
        }
        record.Favorite = !record.Favorite;
        try
        {
            Store.SaveHistory();
        }
        catch
        {
            record.Favorite = !record.Favorite;
            throw;
        }
        return true;
    }
}
