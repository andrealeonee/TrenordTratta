using TrenordTratta.Core.Models;

namespace TrenordTratta.Core.Data;

public interface IRouteRepository
{
    Task<IReadOnlyList<RouteDefinition>> GetAllAsync();
    Task<RouteDefinition?> GetAsync(int id);
    Task SaveAsync(RouteDefinition route);
    Task DeleteAsync(int id);
}

public sealed class RouteRepository(AppDatabase db) : IRouteRepository
{
    public async Task<IReadOnlyList<RouteDefinition>> GetAllAsync()
    {
        var conn = await db.GetConnectionAsync();
        return await conn.Table<RouteDefinition>().OrderBy(r => r.SortOrder).ThenBy(r => r.Id).ToListAsync();
    }

    public async Task<RouteDefinition?> GetAsync(int id)
    {
        var conn = await db.GetConnectionAsync();
        return await conn.FindAsync<RouteDefinition>(id);
    }

    public async Task SaveAsync(RouteDefinition route)
    {
        var conn = await db.GetConnectionAsync();
        if (route.Id == 0) await conn.InsertAsync(route);
        else await conn.UpdateAsync(route);
    }

    public async Task DeleteAsync(int id)
    {
        var conn = await db.GetConnectionAsync();
        if (await conn.Table<RouteDefinition>().CountAsync() <= 1)
            throw new InvalidOperationException("Deve rimanere almeno una tratta.");
        await conn.DeleteAsync<RouteDefinition>(id);
    }
}

public interface ITripRepository
{
    Task<IReadOnlyList<TripRecord>> GetAllAsync();
    Task<IReadOnlyList<TripRecord>> GetForDayAsync(DateTime day);

    /// <summary>Aggiunge il viaggio; se lo stesso treno dello stesso giorno è già segnato non duplica.</summary>
    Task<TripRecord> MarkTakenAsync(Departure departure, RouteDefinition route, RouteLeg leg, DateTime now);

    Task UnmarkAsync(string trainNumber, DateTime day);
    Task DeleteAsync(int id);

    /// <summary>Aggiorna ritardo/affollamento dei viaggi di oggi con l'ultimo rilevamento.</summary>
    Task UpdateLatestAsync(IEnumerable<Departure> departures, DateTime now);
}

public sealed class TripRepository(AppDatabase db) : ITripRepository
{
    public async Task<IReadOnlyList<TripRecord>> GetAllAsync()
    {
        var conn = await db.GetConnectionAsync();
        return await conn.Table<TripRecord>().OrderByDescending(t => t.ScheduledDeparture).ToListAsync();
    }

    public async Task<IReadOnlyList<TripRecord>> GetForDayAsync(DateTime day)
    {
        var conn = await db.GetConnectionAsync();
        var d = day.Date;
        return await conn.Table<TripRecord>().Where(t => t.TrainDate == d).ToListAsync();
    }

    public async Task<TripRecord> MarkTakenAsync(Departure departure, RouteDefinition route, RouteLeg leg, DateTime now)
    {
        var conn = await db.GetConnectionAsync();
        var day = departure.TrainDate.Date;
        var number = departure.TrainNumber;

        var existing = await conn.Table<TripRecord>()
            .Where(t => t.TrainNumber == number && t.TrainDate == day)
            .FirstOrDefaultAsync();

        var trip = existing ?? new TripRecord { CreatedAt = now };
        trip.RouteId = route.Id;
        trip.FromName = leg.FromName;
        trip.ToName = leg.ToName;
        trip.TrainNumber = number;
        trip.TrainDate = day;
        trip.Category = departure.Category;
        trip.ScheduledDeparture = departure.ScheduledDeparture;
        trip.ScheduledArrival = departure.ScheduledArrival;
        Apply(trip, departure, now);

        if (existing is null) await conn.InsertAsync(trip);
        else await conn.UpdateAsync(trip);
        return trip;
    }

    public async Task UnmarkAsync(string trainNumber, DateTime day)
    {
        var conn = await db.GetConnectionAsync();
        var d = day.Date;
        await conn.Table<TripRecord>().Where(t => t.TrainNumber == trainNumber && t.TrainDate == d).DeleteAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var conn = await db.GetConnectionAsync();
        await conn.DeleteAsync<TripRecord>(id);
    }

    public async Task UpdateLatestAsync(IEnumerable<Departure> departures, DateTime now)
    {
        var trips = await GetForDayAsync(now);
        if (trips.Count == 0) return;

        var byNumber = departures
            .GroupBy(d => (d.TrainNumber, d.TrainDate.Date))
            .ToDictionary(g => g.Key, g => g.First());

        var conn = await db.GetConnectionAsync();
        foreach (var trip in trips)
        {
            if (!byNumber.TryGetValue((trip.TrainNumber, trip.TrainDate.Date), out var dep)) continue;
            Apply(trip, dep, now);
            await conn.UpdateAsync(trip);
        }
    }

    private static void Apply(TripRecord trip, Departure d, DateTime now)
    {
        trip.DelayMinutes = d.DelayMinutes ?? trip.DelayMinutes;
        trip.CrowdingPercent = d.CrowdingPercent ?? trip.CrowdingPercent;
        trip.WasCancelled = d.IsCancelled;
        trip.LastUpdatedAt = now;
    }
}
