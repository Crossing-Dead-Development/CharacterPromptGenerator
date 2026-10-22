using CharacterPromptGenerator.Mobile.ViewModels;

namespace CharacterPromptGenerator.Mobile.Views;

public partial class ImportPreviewPage : ContentPage
{
    public const string Route = "importpreview";

    public ImportPreviewPage(ImportPreviewViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
