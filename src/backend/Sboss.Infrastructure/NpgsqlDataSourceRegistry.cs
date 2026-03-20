using System.Collections.Concurrent;
using Npgsql;

namespace Sboss.Infrastructure;

public static class NpgsqlDataSourceRegistry
{
    private static readonly ConcurrentDictionary<Guid, WeakReference<NpgsqlDataSource>> DataSources = new();

    public static NpgsqlDataSource Create(string connectionString)
    {
        var dataSource = NpgsqlDataSource.Create(connectionString);
        DataSources[Guid.NewGuid()] = new WeakReference<NpgsqlDataSource>(dataSource);
        return dataSource;
    }

    public static async Task DisposeTrackedDataSourcesAsync()
    {
        foreach (var entry in DataSources)
        {
            if (entry.Value.TryGetTarget(out var dataSource))
            {
                DataSources.TryRemove(entry.Key, out _);
                await dataSource.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                DataSources.TryRemove(entry.Key, out _);
            }
        }
    }
}
