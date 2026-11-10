using TrenordTratta.Mobile.Views;

namespace TrenordTratta.Mobile;

public partial class AppShell : Shell
{
    public const string TrainRoute = "train";
    public const string RouteEditRoute = "route-edit";

    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(TrainRoute, typeof(TrainDetailPage));
        Routing.RegisterRoute(RouteEditRoute, typeof(RouteEditPage));
    }
}
