namespace TrenordTratta.Mobile.Views;

/// <summary>
/// Base per le pagine con auto-refresh: il timer gira solo mentre la pagina è visibile
/// e l'app è in primo piano (si ferma su Window.Stopped, riparte su Window.Resumed).
/// </summary>
public abstract class AutoRefreshPage : ContentPage
{
    private IDispatcherTimer? _timer;
    private Window? _window;
    private bool _visible;

    protected abstract int RefreshSeconds { get; }
    protected abstract Task OnRefreshTickAsync();

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _visible = true;
        AttachWindow();
        StartTimer();
    }

    protected override void OnDisappearing()
    {
        _visible = false;
        StopTimer();
        DetachWindow();
        base.OnDisappearing();
    }

    private void AttachWindow()
    {
        if (_window is not null || Window is null) return;
        _window = Window;
        _window.Resumed += OnWindowResumed;
        _window.Stopped += OnWindowStopped;
    }

    private void DetachWindow()
    {
        if (_window is null) return;
        _window.Resumed -= OnWindowResumed;
        _window.Stopped -= OnWindowStopped;
        _window = null;
    }

    private async void OnWindowResumed(object? sender, EventArgs e)
    {
        if (!_visible) return;
        StartTimer();
        await SafeTickAsync(); // tornando in app i dati sono vecchi: aggiorna subito
    }

    private void OnWindowStopped(object? sender, EventArgs e) => StopTimer();

    protected void StartTimer()
    {
        StopTimer();
        if (RefreshSeconds <= 0) return;

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(RefreshSeconds);
        _timer.IsRepeating = true;
        _timer.Tick += async (_, _) => await SafeTickAsync();
        _timer.Start();
    }

    private void StopTimer()
    {
        _timer?.Stop();
        _timer = null;
    }

    private async Task SafeTickAsync()
    {
        try { await OnRefreshTickAsync(); }
        catch { /* gli errori sono già gestiti e mostrati dal ViewModel */ }
    }
}
