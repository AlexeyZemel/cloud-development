using Ocelot.DependencyInjection;
using Ocelot.LoadBalancer.Interfaces;
using Ocelot.Middleware;
using ProjectApp.Gateway.LoadBalancing;
using ProjectApp.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Configuration
    .AddJsonFile("ocelot.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

var replicas = GetReplicas(builder.Configuration);
if (replicas.Count > 0)
{
    var overrides = new Dictionary<string, string?>
    {
        ["Routes:0:DownstreamScheme"] = replicas[0].Scheme
    };

    foreach (var (replica, index) in replicas.Select((value, i) => (value, i)))
    {
        overrides[$"Routes:0:DownstreamHostAndPorts:{index}:Host"] = replica.Host;
        overrides[$"Routes:0:DownstreamHostAndPorts:{index}:Port"] = replica.Port.ToString();
    }

    builder.Configuration.AddInMemoryCollection(overrides);
}

var allowedOrigins = builder.Configuration
    .GetSection("AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .WithMethods("GET")
              .WithHeaders("Content-Type");
    });
});

builder.Services.AddSingleton<ILoadBalancerCreator, WeightedRandomLoadBalancerCreator>();
builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();

app.UseCors();
await app.UseOcelot();

app.Run();

static List<(string Scheme, string Host, int Port)> GetReplicas(IConfiguration configuration)
{
    var addresses = configuration.GetSection("ApiReplicas").Get<string[]>()
                    ?? configuration.GetSection("Services:projectapp-api:https").Get<string[]>()
                    ?? configuration.GetSection("Services:projectapp-api:http").Get<string[]>()
                    ?? [];

    return addresses
        .Select(x => Uri.TryCreate(x, UriKind.Absolute, out var uri) ? uri : null)
        .Where(x => x is not null)
        .Select(x => (x!.Scheme, x.Host, x.Port))
        .Distinct()
        .ToList();
}