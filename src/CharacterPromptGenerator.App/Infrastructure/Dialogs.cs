using System.Windows;
using CharacterPromptGenerator.App.Views;
using CharacterPromptGenerator.Core.Localization;
using Microsoft.Win32;

namespace CharacterPromptGenerator.App.Infrastructure;

/// <summary>對話框集中處理。ViewModel 直接呼叫這裡（務實的 MVVM，不另外抽象化）。</summary>
public static class Dialogs
{
    static string AppTitle => L.T("App_Title");

    public static Window? Owner =>
        Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current?.MainWindow;

    static MessageBoxResult Show(string message, string title, MessageBoxButton buttons, MessageBoxImage image)
    {
        var owner = Owner;
        return owner is null
            ? MessageBox.Show(message, title, buttons, image)
            : MessageBox.Show(owner, message, title, buttons, image);
    }

    public static void Info(string message, string? title = null) =>
        Show(message, title ?? AppTitle, MessageBoxButton.OK, MessageBoxImage.Information);

    public static void Error(string message, string? title = null) =>
        Show(message, title ?? AppTitle, MessageBoxButton.OK, MessageBoxImage.Error);

    public static bool Confirm(string message, string? title = null, bool warning = false) =>
        Show(message, title ?? L.T("Dialog_Confirm"), MessageBoxButton.YesNo, warning ? MessageBoxImage.Warning : MessageBoxImage.Question) == MessageBoxResult.Yes;

    public static string? Prompt(string title, string label, string initial = "")
    {
        var dlg = new InputDialog(title, label, initial) { Owner = Owner };
        return dlg.ShowDialog() == true ? dlg.Value : null;
    }

    public static string? OpenImportFile()
    {
        var dlg = new OpenFileDialog
        {
            Title = L.T("Dialog_ImportTitle"),
            Filter = L.T("Dialog_ImportFilter"),
            CheckFileExists = true,
        };
        return dlg.ShowDialog(Owner) == true ? dlg.FileName : null;
    }

    public static string? SaveFile(string defaultName, string filter)
    {
        var dlg = new SaveFileDialog { FileName = defaultName, Filter = filter, OverwritePrompt = true };
        return dlg.ShowDialog(Owner) == true ? dlg.FileName : null;
    }

    public static bool CopyToClipboard(string text)
    {
        // 剪貼簿被其他程式佔用時會丟 COMException，重試幾次
        for (var i = 0; i < 5; i++)
        {
            try
            {
                Clipboard.SetDataObject(text, copy: true);
                return true;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                Thread.Sleep(50);
            }
        }
        Error(L.T("Dialog_ClipboardBusy"));
        return false;
    }
}
