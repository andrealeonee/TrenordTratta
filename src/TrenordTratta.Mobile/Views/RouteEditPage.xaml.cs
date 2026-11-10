using TrenordTratta.Mobile.ViewModels;

namespace TrenordTratta.Mobile.Views;

public partial class RouteEditPage : ContentPage
{
    public RouteEditPage(RouteEditViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
