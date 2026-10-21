using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using CharacterPromptGenerator.App.Infrastructure;
using CharacterPromptGenerator.Core.Storage;

namespace CharacterPromptGenerator.App.ViewModels;

public sealed class BackupItem(BackupInfo info)
{
    public BackupInfo Info { get; } = info;
    public string TimeText => Info.Time.ToString("yyyy-MM-dd HH:mm:ss");
    public string ReasonText => Info.Reason switch
    {
        "manual" => "手動備份",
        "before-import" => "合併匯入前",
        "before-replace" => "取代詞庫前",
        "before-restore" => "還原備份前",
        "before-delete-category" => "刪除分類前",
        "before-bulk-delete" => "大量刪除前",
        "before-bulk-add" => "大量新增前",
        "before-repair" => "自動修復前",
        var r => r,
    };
    public string SizeText => $"{Info.Size / 1024.0:0.#} KB";
}

public sealed class DataViewModel : ObservableObject
{
    readonly AppSession _session;
    BackupItem? _selectedBackup;

    public DataViewModel(AppSession session)
    {
        _session = session;
        OpenDataFolderCommand = new RelayCommand(() => AppSession.OpenFolder(Paths.DataDir));
        OpenBackupFolderCommand = new RelayCommand(() => AppSession.OpenFolder(Paths.BackupDir));
        BackupNowCommand = new RelayCommand(BackupNow);
        RestoreCommand = new RelayCommand(Restore, () => SelectedBackup is not null);
        RefreshCommand = new RelayCommand(Refresh);
        ImportCommand = new RelayCommand(_session.RunImport);
        ExportJsonCommand = new RelayCommand(_session.ExportJson);
        ExportCsvCommand = new RelayCommand(_session.ExportCsv);
        TogglePortableCommand = new RelayCommand(TogglePortable);
        OpenEmojiOverrideCommand = new RelayCommand(() => AppSession.OpenFolder(EmojiOverrideDir));
        session.Store.LibraryChanged += (_, _) => Refresh();
        Refresh();
    }

    DataPaths Paths => _session.Store.Paths;

    public string DataDir => Paths.DataDir;
    public string ModeText => Paths.IsPortable
        ? "可攜模式：資料存放在 EXE 旁的 Data 資料夾。"
        : "一般模式：資料存放在使用者的 AppData 資料夾，更新或移動程式都不會影響詞庫。";
    public string PortableButtonText => Paths.IsPortable ? "切換回一般模式（AppData）" : "切換為可攜模式（EXE 旁 Data\\）";
    public string Stats
    {
        get
        {
            var lib = _session.Library;
            return $"分類 {lib.Categories.Count} 個 · 詞條 {lib.Entries.Count} 筆（啟用 {lib.Entries.Count(e => e.Enabled)}）· " +
                   $"方案 {_session.Store.Presets.Count} 組 · 歷史 {_session.Store.History.Count} 筆";
        }
    }
    public string LoadMessages => string.Join("\n", _session.Store.LoadMessages);

    public ObservableCollection<BackupItem> Backups { get; } = new();
    public BackupItem? SelectedBackup
    {
        get => _selectedBackup;
        set { Set(ref _selectedBackup, value); CommandManager.InvalidateRequerySuggested(); }
    }

    public ICommand OpenDataFolderCommand { get; }
    public ICommand OpenBackupFolderCommand { get; }
    public ICommand BackupNowCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand ExportJsonCommand { get; }
    public ICommand ExportCsvCommand { get; }
    public ICommand TogglePortableCommand { get; }
    public ICommand OpenEmojiOverrideCommand { get; }

    public string EmojiOverrideDir => EmojiImages.Library.OverrideDir ?? Path.Combine(Paths.DataDir, "EmojiOverrides");
    public string EmojiStatus => EmojiImages.Library.PackError is { } error
        ? "⚠ " + error + "\nEmoji 目前以「?」替代圖示顯示。"
        : $"Microsoft Fluent Emoji 3D：{EmojiImages.Library.PackCount} 張圖片（MIT 授權，見 THIRD-PARTY-NOTICES.md）。\n" +
          "不使用 Windows 字型，所以 Windows 10 與 11 顯示相同。";

    public void Refresh()
    {
        Backups.Clear();
        foreach (var b in _session.Store.ListBackups()) Backups.Add(new BackupItem(b));
        OnPropertyChanged(nameof(Stats));
    }

    void BackupNow()
    {
        try
        {
            var path = _session.Store.CreateManualBackup();
            Refresh();
            Dialogs.Info($"已備份目前詞庫：\n{path}\n\n手動備份不會被自動清除。");
        }
        catch (Exception ex)
        {
            Dialogs.Error($"備份失敗：{ex.Message}");
        }
    }

    void Restore()
    {
        if (SelectedBackup is not { } b) return;
        Core.Models.LibraryData lib;
        try
        {
            lib = _session.Store.ReadBackup(b.Info.Path);
        }
        catch (Exception ex)
        {
            Dialogs.Error(ex.Message);
            return;
        }
        if (!Dialogs.Confirm(
                $"用這份備份取代目前的詞庫？\n\n備份時間：{b.TimeText}（{b.ReasonText}）\n" +
                $"備份內容：{lib.Categories.Count} 個分類、{lib.Entries.Count} 筆詞條\n" +
                $"目前詞庫：{_session.Library.Categories.Count} 個分類、{_session.Library.Entries.Count} 筆詞條\n\n" +
                "還原前會先自動備份目前的詞庫。", "還原備份", warning: true))
            return;
        try
        {
            _session.Store.ReplaceLibrary(lib, "before-restore");
            Dialogs.Info("已還原。");
        }
        catch (Exception ex)
        {
            Dialogs.Error($"還原失敗，目前詞庫沒有被修改：{ex.Message}");
        }
    }

    void TogglePortable()
    {
        var exeDir = AppSession.ExeDir;
        var flag = Path.Combine(exeDir, DataPaths.PortableFlagName);
        var target = Paths.IsPortable ? DataPaths.DefaultAppDataDir : DataPaths.PortableDir(exeDir);

        if (!Paths.IsPortable && !DataPaths.CanWrite(target))
        {
            Dialogs.Error($"無法寫入 {target}。\n程式放在 Program Files 等受保護的位置時無法使用可攜模式，請把整個程式資料夾移到其他位置。");
            return;
        }

        var targetHasData = File.Exists(Path.Combine(target, "library.json"));
        var msg = (Paths.IsPortable ? "切換回一般模式。\n" : "切換為可攜模式。\n") +
                  $"新的資料位置：{target}\n\n" +
                  (targetHasData
                      ? "新位置已經有詞庫資料，將直接使用那份資料（不會覆蓋）。目前位置的資料保持不動。"
                      : "會把目前的詞庫、方案、設定、歷史與備份複製過去。目前位置的資料保持不動。") +
                  "\n\n切換後需要重新啟動程式。要繼續嗎？";
        if (!Dialogs.Confirm(msg, "切換資料位置")) return;

        try
        {
            if (!targetHasData) CopyData(Paths.DataDir, target);
            if (Paths.IsPortable) File.Delete(flag);
            else File.WriteAllText(flag, "此檔案存在時，程式會把資料存放在同一層的 Data 資料夾。刪除即可恢復一般模式。");
        }
        catch (Exception ex)
        {
            Dialogs.Error($"切換失敗：{ex.Message}");
            return;
        }

        if (Dialogs.Confirm("已完成設定。現在重新啟動程式嗎？"))
        {
            var exe = Environment.ProcessPath;
            if (exe is not null) Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
            Application.Current.Shutdown();
        }
    }

    static void CopyData(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var name in new[] { "library.json", "presets.json", "settings.json", "history.json" })
        {
            var src = Path.Combine(from, name);
            if (File.Exists(src)) File.Copy(src, Path.Combine(to, name), overwrite: false);
        }
        var backupFrom = Path.Combine(from, "Backups");
        if (!Directory.Exists(backupFrom)) return;
        var backupTo = Path.Combine(to, "Backups");
        Directory.CreateDirectory(backupTo);
        foreach (var f in Directory.GetFiles(backupFrom, "*.json"))
        {
            var dest = Path.Combine(backupTo, Path.GetFileName(f));
            if (!File.Exists(dest)) File.Copy(f, dest);
        }
    }
}
