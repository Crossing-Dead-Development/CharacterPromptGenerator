using CharacterPromptGenerator.Mobile.ViewModels;

namespace CharacterPromptGenerator.Mobile.Views;

public partial class CategoryEditPage : ContentPage
{
    public const string Route = "categoryedit";

    public CategoryEditPage(CategoryEditViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
