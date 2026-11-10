using TrenordTratta.Mobile.ViewModels;

namespace TrenordTratta.Mobile.Views;

public partial class TrainDetailPage : AutoRefreshPage
{
    private readonly TrainDetailViewModel _vm;

    public TrainDetailPage(TrainDetailViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override int RefreshSeconds => _vm.AutoRefreshSeconds;

    protected override Task OnRefreshTickAsync() => _vm.RefreshCommand.ExecuteAsync(null);
}
