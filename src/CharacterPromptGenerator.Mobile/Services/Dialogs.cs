using CharacterPromptGenerator.Core.Localization;

namespace CharacterPromptGenerator.Mobile.Services;

/// <summary>系統原生的提示、確認、輸入與選單對話框。</summary>
public static class Dialogs
{
    static Page Page => Shell.Current?.CurrentPage ?? Application.Current!.Windows[0].Page!;

    public static Task Info(string message, string? title = null) =>
        Page.DisplayAlertAsync(title ?? "", message, L.T("Common_Ok"));

    public static Task Error(string message) =>
        Page.DisplayAlertAsync(L.T("Common_Error"), message, L.T("Common_Ok"));

    public static Task<bool> Confirm(string message, string? title = null) =>
        Page.DisplayAlertAsync(title ?? "", message, L.T("Common_Ok"), L.T("Common_Cancel"));

    /// <summary>文字輸入；取消時回傳 null。</summary>
    public static Task<string?> Prompt(string title, string label, string initial = "") =>
        Page.DisplayPromptAsync(title, label, L.T("Common_Ok"), L.T("Common_Cancel"), initialValue: initial, maxLength: 200);

    /// <summary>從清單選一項；取消時回傳 -1。</summary>
    public static async Task<int> Choose(string title, IReadOnlyList<string> options)
    {
        var picked = await Page.DisplayActionSheetAsync(title, L.T("Common_Cancel"), null, options.ToArray());
        return picked is null ? -1 : options.ToList().IndexOf(picked);
    }
}
