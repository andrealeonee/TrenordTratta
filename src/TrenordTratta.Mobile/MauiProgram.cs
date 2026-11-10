using Microsoft.Extensions.Logging;
using TrenordTratta.Core;
using TrenordTratta.Core.Settings;
using TrenordTratta.Mobile.Services;
using TrenordTratta.Mobile.ViewModels;
using TrenordTratta.Mobile.Views;

namespace TrenordTratta.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "miotreno.db3");
        builder.Services.AddTrenordTrattaCore(dbPath);
        builder.Services.AddSingleton<ISettingsService, MauiSettingsService>();

        builder.Services.AddSingleton<AppShell>();

        // Tab: singleton, mantengono lo stato tra un cambio tab e l'altro
        builder.Services.AddSingleton<DeparturesViewModel>();
        builder.Services.AddSingleton<DeparturesPage>();
        builder.Services.AddSingleton<HistoryViewModel>();
        builder.Services.AddSingleton<HistoryPage>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddSingleton<SettingsPage>();

        // Pagine di navigazione: nuove a ogni apertura
        builder.Services.AddTransient<TrainDetailViewModel>();
        builder.Services.AddTransient<TrainDetailPage>();
        builder.Services.AddTransient<RouteEditViewModel>();
        builder.Services.AddTransient<RouteEditPage>();

#if DEBUG
        builder.Logging.AddDebug();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
#endif

        return builder.Build();
    }
}
