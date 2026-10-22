using CharacterPromptGenerator.Mobile.ViewModels;

namespace CharacterPromptGenerator.Mobile.Views;

public partial class EntryEditPage : ContentPage
{
    public const string Route = "entryedit";

    public EntryEditPage(EntryEditViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
