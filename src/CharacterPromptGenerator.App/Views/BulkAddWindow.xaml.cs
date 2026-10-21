using System.Windows;
using System.Windows.Controls;
using CharacterPromptGenerator.App.Infrastructure;
using CharacterPromptGenerator.Core.Library;

namespace CharacterPromptGenerator.App.Views;

public partial class BulkAddWindow : Window
{
    readonly AppSession _session;
    readonly string _categoryId;
    LibraryEditor.BulkAddPreview? _preview;

    public BulkAddWindow(AppSession session, string categoryId)
    {
        InitializeComponent();
        _session = session;
        _categoryId = categoryId;
        CategoryRun.Text = session.Library.FindCategory(categoryId)?.Name ?? "?";
        UpdatePreview();
        Loaded += (_, _) => InputBox.Focus();
    }

    void Input_TextChanged(object sender, TextChangedEventArgs e) => UpdatePreview();

    void UpdatePreview()
    {
        _preview = LibraryEditor.PreviewBulkAdd(_session.Library, _categoryId, InputBox.Text);
        PreviewText.Text = $"將新增 {_preview.ToAdd.Count} 筆";
        var dupParts = new List<string>();
        if (_preview.DuplicateExisting.Count > 0)
            dupParts.Add($"⚠ 已存在而略過 {_preview.DuplicateExisting.Count} 筆：{string.Join("、", _preview.DuplicateExisting.Take(15))}{(_preview.DuplicateExisting.Count > 15 ? "…" : "")}");
        if (_preview.DuplicateInInput.Count > 0)
            dupParts.Add($"⚠ 貼上內容中重複 {_preview.DuplicateInInput.Count} 筆：{string.Join("、", _preview.DuplicateInInput.Take(15))}{(_preview.DuplicateInInput.Count > 15 ? "…" : "")}");
        DuplicateText.Text = string.Join("\n", dupParts);
        AddButton.IsEnabled = _preview.ToAdd.Count > 0;
        AddButton.Content = _preview.ToAdd.Count > 0 ? $"新增 {_preview.ToAdd.Count} 筆" : "新增";
    }

    void Add_Click(object sender, RoutedEventArgs e)
    {
        if (_preview is null || _preview.ToAdd.Count == 0) return;
        var texts = _preview.ToAdd;
        var added = 0;
        if (_session.TryMutate(lib => added = LibraryEditor.BulkAdd(lib, _categoryId, texts),
                backupReason: texts.Count >= 50 ? "before-bulk-add" : null))
        {
            Dialogs.Info($"已新增 {added} 筆詞條。");
            DialogResult = true;
        }
    }
}
