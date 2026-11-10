using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Timeout;
using TrenordTratta.Core.Api.Trenord;
using TrenordTratta.Core.Api.ViaggiaTreno;
using TrenordTratta.Core.Data;
using TrenordTratta.Core.Services;

namespace TrenordTratta.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTrenordTrattaCore(this IServiceCollection services, string databasePath)
    {
        services.AddHttpClient<ITrenordClient, TrenordClient>(c => Configure(c, TrenordClient.BaseAddress))
            .AddResilienceHandler("trenord", ConfigureResilience);

        services.AddHttpClient<IViaggiaTrenoClient, ViaggiaTrenoClient>(c => Configure(c, ViaggiaTrenoClient.BaseAddress))
            .AddResilienceHandler("viaggiatreno", ConfigureResilience);

        services.AddSingleton(new DatabaseOptions { Path = databasePath });
        services.AddSingleton<AppDatabase>();
        services.AddSingleton<IRouteRepository, RouteRepository>();
        services.AddSingleton<ITripRepository, TripRepository>();
        services.AddSingleton<IRouteService, RouteService>();
        services.AddSingleton<IDepartureService, DepartureService>();
        services.AddSingleton<DirectionResolver>();
        return services;
    }

    private static void Configure(HttpClient client, string baseAddress)
    {
        client.BaseAddress = new Uri(baseAddress);
        // Il timeout lo gestisce la pipeline Polly (per tentativo e totale).
        client.Timeout = Timeout.InfiniteTimeSpan;
        // Accept generico: con solo application/json RESTEasy risponde 406 agli endpoint text/plain
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        // Alcuni endpoint rifiutano richieste senza User-Agent
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Linux; Android 14) MioTreno/1.0");
    }

    /// <summary>
    /// Timeout totale 30s → retry esponenziale con jitter (3 tentativi) → timeout per tentativo 8s.
    /// Si ritenta anche su 403 e 429: il repo di riferimento lo fa, indizio di rate limiting.
    /// </summary>
    private static void ConfigureResilience(ResiliencePipelineBuilder<HttpResponseMessage> builder)
    {
        builder
            .AddTimeout(TimeSpan.FromSeconds(30))
            .AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromMilliseconds(800),
                ShouldHandle = args => ValueTask.FromResult(args.Outcome switch
                {
                    { Exception: HttpRequestException or TimeoutRejectedException } => true,
                    { Result: { } r } => IsTransient(r.StatusCode),
                    _ => false,
                }),
            })
            .AddTimeout(TimeSpan.FromSeconds(8));
    }

    internal static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.Forbidden
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.RequestTimeout
            || (int)status >= 500;
}
