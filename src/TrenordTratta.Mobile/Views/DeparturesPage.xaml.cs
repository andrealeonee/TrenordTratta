using TrenordTratta.Mobile.ViewModels;

namespace TrenordTratta.Mobile.Views;

public partial class DeparturesPage : AutoRefreshPage
{
    private readonly DeparturesViewModel _vm;

    public DeparturesPage(DeparturesViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override int RefreshSeconds => _vm.AutoRefreshSeconds;

    protected override Task OnRefreshTickAsync() => _vm.RefreshCommand.ExecuteAsync(null);

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.OnAppearingAsync();
    }
}
