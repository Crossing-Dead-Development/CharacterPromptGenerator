using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using CharacterPromptGenerator.App.Infrastructure;
using CharacterPromptGenerator.Core.Localization;
using CharacterPromptGenerator.Core.Storage;

namespace CharacterPromptGenerator.App.ViewModels;

public sealed class BackupItem(BackupInfo info)
{
    public BackupInfo Info { get; } = info;
    public string TimeText => Info.Time.ToString("yyyy-MM-dd HH:mm:ss");
    public string ReasonText => Info.Reason switch
    {
        "manual" => L.T("Backup_Manual"),
        "before-import" => L.T("Backup_BeforeImport"),
        "before-replace" => L.T("Backup_BeforeReplace"),
        "before-restore" => L.T("Backup_BeforeRestore"),
        "before-delete-category" => L.T("Backup_BeforeDeleteCategory"),
        "before-bulk-delete" => L.T("Backup_BeforeBulkDelete"),
        "before-bulk-add" => L.T("Backup_BeforeBulkAdd"),
        "before-repair" => L.T("Backup_BeforeRepair"),
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
        ShowLicensesCommand = new RelayCommand(AppSession.ShowLicenses);
        session.Store.LibraryChanged += (_, _) => Refresh();
        Refresh();
    }

    DataPaths Paths => _session.Store.Paths;

    public string DataDir => Paths.DataDir;
    public string ModeText => Paths.IsPortable
        ? L.T("Data_ModePortable")
        : L.T("Data_ModeNormal");
    public string PortableButtonText => Paths.IsPortable ? L.T("Data_ToNormal") : L.T("Data_ToPortable");
    public string Stats
    {
        get
        {
            var lib = _session.Library;
            return L.F("Data_Stats", lib.Categories.Count, lib.Entries.Count, lib.Entries.Count(e => e.Enabled),
                _session.Store.Presets.Count, _session.Store.History.Count);
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
    public ICommand ShowLicensesCommand { get; }

    public string EmojiOverrideDir => EmojiImages.Library.OverrideDir ?? Path.Combine(Paths.DataDir, "EmojiOverrides");
    public string EmojiStatus => EmojiImages.Library.PackError is { } error
        ? "⚠ " + error + L.T("Data_EmojiFallback")
        : L.F("Data_EmojiStatus", EmojiImages.Library.PackCount);

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
            Dialogs.Info(L.F("Data_BackedUp", path));
        }
        catch (Exception ex)
        {
            Dialogs.Error(L.F("Data_BackupFailed", ex.Message));
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
                L.F("Data_ConfirmRestore", b.TimeText, b.ReasonText, lib.Categories.Count, lib.Entries.Count,
                    _session.Library.Categories.Count, _session.Library.Entries.Count),
                L.T("Data_RestoreTitle"), warning: true))
            return;
        try
        {
            _session.Store.ReplaceLibrary(lib, "before-restore");
            Dialogs.Info(L.T("Data_Restored"));
        }
        catch (Exception ex)
        {
            Dialogs.Error(L.F("Data_RestoreFailed", ex.Message));
        }
    }

    void TogglePortable()
    {
        var exeDir = AppSession.ExeDir;
        var flag = Path.Combine(exeDir, DataPaths.PortableFlagName);
        var target = Paths.IsPortable ? DataPaths.DefaultAppDataDir : DataPaths.PortableDir(exeDir);

        if (!Paths.IsPortable && !DataPaths.CanWrite(target))
        {
            Dialogs.Error(L.F("Data_CannotWrite", target));
            return;
        }

        var targetHasData = File.Exists(Path.Combine(target, "library.json"));
        var msg = L.T(Paths.IsPortable ? "Data_SwitchToNormal" : "Data_SwitchToPortable") +
                  L.F("Data_NewLocation", target) +
                  L.T(targetHasData ? "Data_TargetHasData" : "Data_WillCopy") +
                  L.T("Data_RestartNeeded");
        if (!Dialogs.Confirm(msg, L.T("Data_SwitchTitle"))) return;

        try
        {
            if (!targetHasData) CopyData(Paths.DataDir, target);
            if (Paths.IsPortable) File.Delete(flag);
            else File.WriteAllText(flag, L.T("Data_FlagContent"));
        }
        catch (Exception ex)
        {
            Dialogs.Error(L.F("Data_SwitchFailed", ex.Message));
            return;
        }

        if (Dialogs.Confirm(L.T("Data_RestartNow"))) AppSession.Restart();
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
