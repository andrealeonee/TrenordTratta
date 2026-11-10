using TrenordTratta.Core.Api;
using TrenordTratta.Core.Api.Trenord;
using TrenordTratta.Core.Api.ViaggiaTreno;

namespace TrenordTratta.Core.Tests;

internal sealed class FakeViaggiaTreno : IViaggiaTrenoClient
{
    public List<VtDeparture> Departures { get; } = [];
    public Dictionary<string, VtTrainStatus> Status { get; } = [];
    public bool FailDepartures { get; set; }

    public Task<IReadOnlyList<VtDeparture>> GetDeparturesAsync(string stationCode, DateTimeOffset when, CancellationToken ct = default) =>
        FailDepartures
            ? throw new ApiException("ViaggiaTreno", "boom")
            : Task.FromResult<IReadOnlyList<VtDeparture>>(Departures);

    public Task<VtTrainStatus?> GetTrainStatusAsync(string originCode, string trainNumber, long departureDateMs, CancellationToken ct = default) =>
        Task.FromResult(Status.GetValueOrDefault(trainNumber));

    public Task<IReadOnlyList<VtStation>> SearchStationsAsync(string text, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<VtStation>>([]);
}

internal sealed class FakeTrenord : ITrenordClient
{
    public Dictionary<string, string> Json { get; } = [];

    public Task<IReadOnlyList<TrenordResult>> GetTrainAsync(string trainNumber, CancellationToken ct = default) =>
        Json.TryGetValue(trainNumber, out var body)
            ? Task.FromResult(TrenordClient.Parse(body))
            : throw new ApiException("Trenord", "HTTP 404");
}
