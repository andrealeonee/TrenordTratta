using SQLite;
using TrenordTratta.Core.Models;

namespace TrenordTratta.Core.Data;

public sealed class DatabaseOptions
{
    public required string Path { get; init; }
}

/// <summary>SQLite locale: tratte e storico dei treni presi.</summary>
public sealed class AppDatabase(DatabaseOptions options) : IAsyncDisposable
{
    private const SQLiteOpenFlags Flags =
        SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache | SQLiteOpenFlags.FullMutex;

    private readonly SemaphoreSlim _initLock = new(1, 1);
    private SQLiteAsyncConnection? _connection;

    public async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_connection is not null) return _connection;

        await _initLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_connection is not null) return _connection;

            var conn = new SQLiteAsyncConnection(options.Path, Flags);
            await conn.CreateTableAsync<RouteDefinition>().ConfigureAwait(false);
            await conn.CreateTableAsync<TripRecord>().ConfigureAwait(false);
            await SeedAsync(conn).ConfigureAwait(false);

            _connection = conn;
            return conn;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>Tratta di default. I codici restano vuoti: vengono risolti al primo utilizzo.</summary>
    private static async Task SeedAsync(SQLiteAsyncConnection conn)
    {
        if (await conn.Table<RouteDefinition>().CountAsync().ConfigureAwait(false) > 0) return;

        await conn.InsertAsync(new RouteDefinition
        {
            Name = "Casa ↔ Milano",
            StationAName = "Mariano Comense",
            StationBName = "Milano Cadorna",
            SortOrder = 0,
        }).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null) await _connection.CloseAsync().ConfigureAwait(false);
        _initLock.Dispose();
    }
}
