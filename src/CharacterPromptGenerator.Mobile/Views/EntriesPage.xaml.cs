using CharacterPromptGenerator.Mobile.ViewModels;

namespace CharacterPromptGenerator.Mobile.Views;

public partial class EntriesPage : ContentPage
{
    public const string Route = "entries";

    readonly EntriesViewModel _vm;

    public EntriesPage(EntriesViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Load();
    }

    void OnEntryTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is EntryItem item) _vm.TapCommand.Execute(item);
    }
}
