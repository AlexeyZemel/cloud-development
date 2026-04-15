using Ocelot.Configuration;
using Ocelot.LoadBalancer.Interfaces;
using Ocelot.Responses;
using Ocelot.ServiceDiscovery.Providers;

namespace ProjectApp.Gateway.LoadBalancing;

/// <summary>
/// Балансировщик нагрузки с алгоритмом взвешенного случайного выбора (Weighted Random)
/// </summary>
public sealed class WeightedRandomLoadBalancerCreator(IConfiguration configuration) : ILoadBalancerCreator
{
    public string Type => "WeightedRandom";

    public Response<ILoadBalancer> Create(
        DownstreamRoute downstreamRoute,
        IServiceDiscoveryProvider serviceDiscoveryProvider)
    {
        var weights = configuration
            .GetSection("Gateway:WeightedRandom:Weights")
            .Get<double[]>() ?? [];

        ILoadBalancer loadBalancer =
            new WeightedRandomLoadBalancer(downstreamRoute.DownstreamAddresses, weights);

        return new OkResponse<ILoadBalancer>(loadBalancer);
    }
}