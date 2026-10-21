using System.Windows;
using System.Windows.Controls;
using CharacterPromptGenerator.App.Infrastructure;
using CharacterPromptGenerator.Core.Library;
using CharacterPromptGenerator.Core.Localization;

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
        PreviewText.Text = L.F("Bulk_WillAdd", _preview.ToAdd.Count);
        var dupParts = new List<string>();
        if (_preview.DuplicateExisting.Count > 0)
            dupParts.Add(L.F("Bulk_DupExisting", _preview.DuplicateExisting.Count, Sample(_preview.DuplicateExisting)));
        if (_preview.DuplicateInInput.Count > 0)
            dupParts.Add(L.F("Bulk_DupInput", _preview.DuplicateInInput.Count, Sample(_preview.DuplicateInInput)));
        DuplicateText.Text = string.Join("\n", dupParts);
        AddButton.IsEnabled = _preview.ToAdd.Count > 0;
        AddButton.Content = _preview.ToAdd.Count > 0 ? L.F("Bulk_AddCount", _preview.ToAdd.Count) : L.T("Bulk_Add");
    }

    static string Sample(IReadOnlyList<string> items) =>
        string.Join(L.T("Common_ListSep"), items.Take(15)) + (items.Count > 15 ? L.T("Common_Ellipsis") : "");

    void Add_Click(object sender, RoutedEventArgs e)
    {
        if (_preview is null || _preview.ToAdd.Count == 0) return;
        var texts = _preview.ToAdd;
        var added = 0;
        if (_session.TryMutate(lib => added = LibraryEditor.BulkAdd(lib, _categoryId, texts),
                backupReason: texts.Count >= 50 ? "before-bulk-add" : null))
        {
            Dialogs.Info(L.F("Bulk_Added", added));
            DialogResult = true;
        }
    }
}
