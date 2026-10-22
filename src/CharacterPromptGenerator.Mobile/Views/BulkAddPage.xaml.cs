using CharacterPromptGenerator.Mobile.ViewModels;

namespace CharacterPromptGenerator.Mobile.Views;

public partial class BulkAddPage : ContentPage
{
    public const string Route = "bulkadd";

    public BulkAddPage(BulkAddViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
