using CharacterPromptGenerator.Core.Models;
using CharacterPromptGenerator.Core.Storage;
using CharacterPromptGenerator.Core.Text;

namespace CharacterPromptGenerator.Core.Drawing;

/// <summary>
/// 抽題方案的新增、覆寫、改名、刪除與套用。每個動作都會存檔；
/// 存檔失敗時把記憶體中的方案還原成動作之前的樣子，再把例外丟給呼叫端顯示。
/// </summary>
public sealed class PresetManager(AppStore store)
{
    public IReadOnlyList<Preset> Presets => store.Presets;

    public Preset? Find(string? id) => id is null ? null : store.Presets.FirstOrDefault(p => p.Id == id);

    /// <summary>以目前抽題設定另存新方案，並設為目前方案。名稱清理後為空白時回傳 null。</summary>
    public Preset? SaveAs(string? name)
    {
        name = TextNormalizer.Clean(name);
        if (name.Length == 0) return null;
        var preset = store.PresetFromSettings(IdGenerator.New("preset"), name);
        store.Presets.Add(preset);
        Save(() => store.Presets.Remove(preset));
        store.Settings.LastPresetId = preset.Id;
        store.SaveSettings();
        return preset;
    }

    /// <summary>用目前抽題設定覆寫既有方案（保留 Id 與名稱）。</summary>
    public bool Overwrite(string id)
    {
        var idx = store.Presets.FindIndex(p => p.Id == id);
        if (idx < 0) return false;
        var old = store.Presets[idx];
        store.Presets[idx] = store.PresetFromSettings(old.Id, old.Name);
        Save(() => store.Presets[idx] = old);
        return true;
    }

    /// <summary>改名。名稱為空白或沒有改變時不做任何事並回傳 false。</summary>
    public bool Rename(string id, string? name)
    {
        var preset = Find(id);
        name = TextNormalizer.Clean(name);
        if (preset is null || name.Length == 0 || name == preset.Name) return false;
        var old = preset.Name;
        preset.Name = name;
        Save(() => preset.Name = old);
        return true;
    }

    public bool Delete(string id)
    {
        var idx = store.Presets.FindIndex(p => p.Id == id);
        if (idx < 0) return false;
        var preset = store.Presets[idx];
        store.Presets.RemoveAt(idx);
        Save(() => store.Presets.Insert(idx, preset));
        if (store.Settings.LastPresetId == id)
        {
            store.Settings.LastPresetId = null;
            store.SaveSettings();
        }
        return true;
    }

    /// <summary>把方案套用到目前抽題設定。方案沒有列到的分類會被取消勾選。</summary>
    public bool Apply(string id)
    {
        var preset = Find(id);
        if (preset is null) return false;
        store.ApplyPresetToSettings(preset);
        store.SaveSettings();
        return true;
    }

    void Save(Action rollback)
    {
        try
        {
            store.SavePresets();
        }
        catch
        {
            rollback();
            throw;
        }
    }
}
