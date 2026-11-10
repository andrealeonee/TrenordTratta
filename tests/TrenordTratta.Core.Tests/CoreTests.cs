using TrenordTratta.Core.Api.Trenord;
using TrenordTratta.Core.Api.ViaggiaTreno;
using TrenordTratta.Core.Data;
using TrenordTratta.Core.Models;
using TrenordTratta.Core.Services;
using TrenordTratta.Core.Settings;
using Xunit;

namespace TrenordTratta.Core.Tests;

public class ViaggiaTrenoDateTests
{
    [Fact]
    public void Format_winter_uses_plus_0100()
    {
        var when = new DateTimeOffset(2026, 11, 10, 18, 30, 0, TimeSpan.FromHours(1));
        Assert.Equal("Tue Nov 10 2026 18:30:00 GMT+0100", ViaggiaTrenoDate.Format(when));
    }

    [Fact]
    public void Format_summer_uses_plus_0200_and_converts_from_utc()
    {
        var when = new DateTimeOffset(2026, 7, 1, 6, 0, 0, TimeSpan.Zero);
        Assert.Equal("Wed Jul 01 2026 08:00:00 GMT+0200", ViaggiaTrenoDate.Format(when));
    }

    [Fact]
    public void ParseTime_handles_midnight_crossing()
    {
        var day = new DateTime(2026, 11, 10);
        var t = ViaggiaTrenoDate.ParseTime("00:10:00", day, reference: day.AddHours(23.8));
        Assert.Equal(day.AddDays(1).AddMinutes(10), t);
    }
}

public class StationNameMatcherTests
{
    [Theory]
    [InlineData("MILANO CADORNA", "Milano Cadorna")]
    [InlineData("Milano Cadorna FN", "MILANO CADORNA")]
    [InlineData("MARIANO C.", "Mariano Comense")]
    [InlineData("Seveso", "SEVESO")]
    public void Matches(string a, string b) => Assert.True(StationNameMatcher.Matches(a, b));

    [Theory]
    [InlineData("MILANO BOVISA", "Milano Cadorna")]
    [InlineData("MILANO", "Milano Cadorna")]
    [InlineData("", "Seveso")]
    public void DoesNotMatch(string a, string b) => Assert.False(StationNameMatcher.Matches(a, b));
}

public class TrenordParsingTests
{
    [Fact]
    public void Parses_tolerant_types_and_selects_today_journey()
    {
        const string json = """
        [ { "journey_list": [
            { "train": { "date": "20261109", "crowding": { "percentage": 10, "source": "X" } },
              "pass_list": [ { "station": { "station_id": 1, "station_ori_name": "MARIANO COMENSE" }, "type": "O", "dep_time": "07:00:00" } ] },
            { "train": { "date": "20261110", "crowding": { "percentage": "45", "source": "APC" } },
              "pass_list": [
                { "station": { "station_id": 1, "station_ori_name": "MARIANO COMENSE" }, "type": "O", "dep_time": "07:00:00",
                  "platform": 2, "cancelled": "false", "actual_data": { "dep_actual_time": "07:03:00" } } ] }
        ] } ]
        """;

        var results = TrenordClient.Parse(json);
        var journey = TrenordJourneySelector.Select(results, new DateTime(2026, 11, 10), "Mariano Comense");

        Assert.NotNull(journey);
        Assert.Equal(45, journey!.Train!.Crowding!.Percentage);
        Assert.Equal("2", journey.PassList![0].Platform);
        Assert.False(journey.PassList[0].Cancelled);
    }

    [Fact]
    public void Station_list_parsing()
    {
        var s = ViaggiaTrenoClient.ParseStations("MILANO CADORNA|N00001\nMILANO CENTRALE|S01700\n");
        Assert.Equal(2, s.Count);
        Assert.Equal("N00001", s[0].Code);
    }
}

public class DepartureServiceTests
{
    private static readonly DateTime Day = new(2026, 11, 10);
    private static readonly DateTime Now = Day.AddHours(7);
    private static readonly RouteLeg Leg = new("Mariano Comense", "MAR", "Milano Cadorna", "CAD");

    private static long Ms(DateTime romeLocal) =>
        new DateTimeOffset(romeLocal, ViaggiaTrenoDate.Rome.GetUtcOffset(romeLocal)).ToUnixTimeMilliseconds();

    private static VtDeparture Vt(string number, int hh, int mm, string terminus, int client = 63, int delay = 0) => new()
    {
        NumeroTreno = number,
        Categoria = "R",
        CodOrigine = "ORIG",
        Destinazione = terminus,
        OrarioPartenza = Ms(Day.AddHours(hh).AddMinutes(mm)),
        DataPartenzaTreno = Ms(Day),
        Ritardo = delay,
        CodiceCliente = client,
    };

    private static string Journey(string crowdingSource, int pct, params string[] passes) => $$"""
        [ { "journey_list": [ { "train": { "date": "20261110", "crowding": { "percentage": {{pct}}, "source": "{{crowdingSource}}" } },
                                "pass_list": [ {{string.Join(",", passes)}} ] } ] } ]
        """;

    private static string Pass(string? name, string type, string time, string? actual = null, bool cancelled = false) =>
        (name is null ? "{" : $$"""{ "station": { "station_ori_name": "{{name}}" }, """)
        + $$""" "type": "{{type}}", "arr_time": "{{time}}", "dep_time": "{{time}}", "cancelled": {{(cancelled ? "true" : "false")}} """
        + (actual is null ? "" : $$""", "actual_data": { "arr_actual_time": "{{actual}}", "dep_actual_time": "{{actual}}" } """)
        + "}";

    private static (DepartureService svc, FakeViaggiaTreno vt, FakeTrenord tn) Create()
    {
        var vt = new FakeViaggiaTreno();
        var tn = new FakeTrenord();
        return (new DepartureService(vt, tn, clock: () => Now), vt, tn);
    }

    [Fact]
    public async Task Keeps_only_trains_stopping_at_destination_and_merges_data()
    {
        var (svc, vt, tn) = Create();

        // 1) verso Milano, con affollamento affidabile e 4' di ritardo in partenza
        vt.Departures.Add(Vt("1001", 7, 10, "MILANO CADORNA"));
        tn.Json["1001"] = Journey("APC", 62,
            Pass("CAMNAGO", "O", "07:05:00", "07:09:00"),
            Pass("MARIANO COMENSE", "F", "07:10:00", "07:14:00"),
            Pass("SEVESO", "F", "07:20:00"),
            Pass("MILANO CADORNA", "D", "07:50:00"));

        // 2) direzione opposta: verso Asso → escluso
        vt.Departures.Add(Vt("1002", 7, 12, "ASSO"));
        tn.Json["1002"] = Journey("APC", 20,
            Pass("MILANO CADORNA", "O", "06:30:00", "06:30:00"),
            Pass("MARIANO COMENSE", "F", "07:12:00"),
            Pass("ASSO", "D", "07:40:00"));

        // 3) Trenord non risponde, ma il capolinea è la destinazione → incluso con ritardo VT
        vt.Departures.Add(Vt("1003", 7, 40, "MILANO CADORNA", delay: 5));

        // 4) già partito → escluso
        vt.Departures.Add(Vt("1004", 6, 50, "MILANO CADORNA"));
        tn.Json["1004"] = Journey("APC", 30,
            Pass("MARIANO COMENSE", "O", "06:50:00", "06:51:00"),
            Pass("MILANO CADORNA", "D", "07:30:00"));

        // duplicato (stessa tripla) → una sola riga
        vt.Departures.Add(Vt("1001", 7, 10, "MILANO CADORNA"));

        var board = await svc.GetDeparturesAsync(Leg, new AppSettings());

        Assert.Equal(["1001", "1003"], board.Departures.Select(d => d.TrainNumber));

        var first = board.Departures[0];
        Assert.Equal(4, first.DelayMinutes);
        Assert.Equal(62, first.CrowdingPercent);
        Assert.Equal(Day.AddHours(7).AddMinutes(50), first.ScheduledArrival);
        Assert.False(first.HasDeparted); // partito alle 07:14 ma "ora" sono le 07:00 nel test

        var third = board.Departures[1];
        Assert.Equal(5, third.DelayMinutes);
        Assert.Null(third.CrowdingPercent);
        Assert.Contains(board.Warnings, w => w.Contains("Trenord"));
    }

    [Fact]
    public async Task Untrusted_crowding_source_is_hidden()
    {
        var (svc, vt, tn) = Create();
        vt.Departures.Add(Vt("2001", 7, 30, "MILANO CADORNA"));
        tn.Json["2001"] = Journey("", 80,
            Pass("MARIANO COMENSE", "O", "07:30:00"),
            Pass("MILANO CADORNA", "D", "08:10:00"));

        var board = await svc.GetDeparturesAsync(Leg, new AppSettings());
        Assert.Null(board.Departures.Single().CrowdingPercent);

        var strict = new AppSettings { TrustedCrowdingSources = "APC" };
        tn.Json["2001"] = Journey("STIMA", 80,
            Pass("MARIANO COMENSE", "O", "07:30:00"),
            Pass("MILANO CADORNA", "D", "08:10:00"));
        board = await svc.GetDeparturesAsync(Leg, strict);
        Assert.Null(board.Departures.Single().CrowdingPercent);
    }

    [Fact]
    public async Task Missing_station_falls_back_to_viaggiatreno_names()
    {
        var (svc, vt, tn) = Create();
        vt.Departures.Add(Vt("3001", 7, 20, "SARONNO"));
        tn.Json["3001"] = Journey("APC", 40,
            Pass("MARIANO COMENSE", "O", "07:20:00"),
            Pass(null, "F", "07:35:00"),
            Pass("SARONNO", "D", "07:50:00"));
        vt.Status["3001"] = new VtTrainStatus
        {
            Fermate =
            [
                new VtStop { Id = "MAR", Stazione = "MARIANO COMENSE" },
                new VtStop { Id = "CAD", Stazione = "MILANO CADORNA" }, // posizione della fermata senza station
                new VtStop { Id = "SAR", Stazione = "SARONNO" },
            ],
        };

        var board = await svc.GetDeparturesAsync(Leg, new AppSettings());
        var dep = Assert.Single(board.Departures);
        Assert.True(dep.Stops[1].FromFallback);
        Assert.Equal(Day.AddHours(7).AddMinutes(35), dep.ScheduledArrival);
    }

    [Fact]
    public async Task Cancelled_origin_stop_marks_train_cancelled()
    {
        var (svc, vt, tn) = Create();
        vt.Departures.Add(Vt("4001", 7, 30, "MILANO CADORNA"));
        tn.Json["4001"] = Journey("APC", 10,
            Pass("MARIANO COMENSE", "O", "07:30:00", cancelled: true),
            Pass("MILANO CADORNA", "D", "08:10:00"));

        var board = await svc.GetDeparturesAsync(Leg, new AppSettings());
        Assert.True(board.Departures.Single().IsCancelled);
    }

    [Fact]
    public async Task All_windows_failing_throws()
    {
        var (svc, vt, _) = Create();
        vt.FailDepartures = true;
        await Assert.ThrowsAsync<TrenordTratta.Core.Api.ApiException>(() => svc.GetDeparturesAsync(Leg, new AppSettings()));
    }
}

public class DirectionResolverTests
{
    [Fact]
    public void Manual_toggle_lasts_until_band_changes()
    {
        var r = new DirectionResolver();
        var morning = new DateTime(2026, 11, 10, 8, 0, 0);
        Assert.Equal(Direction.AtoB, r.Resolve(morning, 13));
        Assert.Equal(Direction.BtoA, r.Toggle(morning, 13));
        Assert.Equal(Direction.BtoA, r.Resolve(morning.AddHours(2), 13));
        Assert.Equal(Direction.BtoA, r.Resolve(morning.AddHours(6), 13)); // pomeriggio: auto = BtoA, toggle azzerato
        Assert.False(r.IsManual);
    }
}

public class TripRepositoryTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"tt-{Guid.NewGuid():N}.db3");
    private AppDatabase _db = null!;

    public Task InitializeAsync()
    {
        _db = new AppDatabase(new DatabaseOptions { Path = _path });
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        File.Delete(_path);
    }

    [Fact]
    public async Task Seeds_default_route_and_marks_trip_without_duplicates()
    {
        var routes = new RouteRepository(_db);
        var trips = new TripRepository(_db);

        var route = Assert.Single(await routes.GetAllAsync());
        Assert.Equal("Mariano Comense", route.StationAName);

        var day = new DateTime(2026, 11, 10);
        var dep = new Departure
        {
            TrainNumber = "1001", OriginCode = "X", TrainDate = day, ScheduledDeparture = day.AddHours(7),
            DelayMinutes = 3, CrowdingPercent = 50,
        };
        var leg = route.GetLeg(Direction.AtoB);

        await trips.MarkTakenAsync(dep, route, leg, day.AddHours(7));
        await trips.MarkTakenAsync(dep, route, leg, day.AddHours(7));
        Assert.Single(await trips.GetAllAsync());

        dep.DelayMinutes = 9;
        await trips.UpdateLatestAsync([dep], day.AddHours(8));
        Assert.Equal(9, (await trips.GetAllAsync())[0].DelayMinutes);

        await trips.UnmarkAsync("1001", day);
        Assert.Empty(await trips.GetAllAsync());
    }
}
