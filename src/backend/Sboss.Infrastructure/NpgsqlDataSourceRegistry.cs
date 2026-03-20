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

    public static void ClearTrackedPools()
    {
        foreach (var entry in DataSources)
        {
            if (entry.Value.TryGetTarget(out var dataSource))
            {
                dataSource.Clear();
                continue;
            }

            DataSources.TryRemove(entry.Key, out _);
        }
    }
}
