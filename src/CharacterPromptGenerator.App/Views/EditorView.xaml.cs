using System.Windows.Controls;
using CharacterPromptGenerator.App.ViewModels;

namespace CharacterPromptGenerator.App.Views;

public partial class EditorView : UserControl
{
    public EditorView() => InitializeComponent();

    // DataGrid.SelectedItems 不能直接繫結，在這裡轉交給 ViewModel
    void EntriesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is EditorViewModel vm)
            vm.SetSelection(EntriesGrid.SelectedItems.OfType<EntryRow>().ToList());
    }
}
