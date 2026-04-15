using Ocelot.Configuration;
using Ocelot.LoadBalancer.Interfaces;
using Ocelot.Responses;
using Ocelot.Values;

namespace ProjectApp.Gateway.LoadBalancing;

/// <summary>
/// Балансировщик нагрузки с алгоритмом взвешенного случайного выбора (Weighted Random)
/// </summary>
public sealed class WeightedRandomLoadBalancer(
    IReadOnlyList<DownstreamHostAndPort> downstreamAddresses,
    IReadOnlyList<double> configuredWeights)
    : ILoadBalancer
{
    private readonly Random _random = Random.Shared;

    private readonly IReadOnlyList<ServiceHostAndPort> _replicas =
        downstreamAddresses
            .Select(x => new ServiceHostAndPort(x.Host, x.Port))
            .ToArray();

    private readonly double[] _weights =
        NormalizeWeights(configuredWeights, downstreamAddresses.Count);

    public string Type => "WeightedRandom";

    /// <summary>
    /// Выбирает реплику для обработки запроса
    /// </summary>
    public Task<Response<ServiceHostAndPort>> LeaseAsync(HttpContext httpContext)
    {
        if (_replicas.Count == 0)
        {
            return Task.FromResult<Response<ServiceHostAndPort>>(
                new OkResponse<ServiceHostAndPort>(
                    new ServiceHostAndPort("localhost", 1)));
        }

        var selected = _replicas[SelectIndexByWeight(_weights)];

        return Task.FromResult<Response<ServiceHostAndPort>>(
            new OkResponse<ServiceHostAndPort>(selected));
    }

    public void Release(ServiceHostAndPort hostAndPort)
    {
    }

    /// <summary>
    /// Выбор индекса методом рулетки
    /// </summary>
    private int SelectIndexByWeight(IReadOnlyList<double> weights)
    {
        var roll = _random.NextDouble();
        var cumulative = 0d;

        for (var i = 0; i < weights.Count; i++)
        {
            cumulative += weights[i];
            if (roll <= cumulative)
                return i;
        }

        return weights.Count - 1;
    }

    /// <summary>
    /// Нормализация весов
    /// </summary>
    private static double[] NormalizeWeights(
        IReadOnlyList<double> configuredWeights,
        int replicasCount)
    {
        if (replicasCount == 0)
            return [];

        if (configuredWeights.Count != replicasCount ||
            configuredWeights.Any(x => x <= 0d))
        {
            return [.. Enumerable.Repeat(1d / replicasCount, replicasCount)];
        }

        var sum = configuredWeights.Sum();

        if (sum <= 0d)
        {
            return [.. Enumerable.Repeat(1d / replicasCount, replicasCount)];
        }

        return configuredWeights
            .Select(x => x / sum)
            .ToArray();
    }
}