using CharacterPromptGenerator.Mobile.ViewModels;

namespace CharacterPromptGenerator.Mobile.Views;

public partial class BackupsPage : ContentPage
{
    public const string Route = "backups";

    readonly BackupsViewModel _vm;

    public BackupsPage(BackupsViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Load();
    }

    void OnBackupTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is BackupRow row) _vm.RestoreCommand.Execute(row);
    }
}
