var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("cache")
    .WithRedisCommander();

const int replicaCount = 5;
var apiReplicas = new List<IResourceBuilder<ProjectResource>>(replicaCount);

for (var i = 0; i < replicaCount; i++)
{
    var replica = builder.AddProject<Projects.ProjectApp_Api>($"projectapp-api-{i + 1}")
        .WithReference(redis)
        .WaitFor(redis);

    apiReplicas.Add(replica);
}

var gateway = builder.AddProject<Projects.ProjectApp_Gateway>("projectapp-gateway")
    .WithExternalHttpEndpoints();

for (var i = 0; i < apiReplicas.Count; i++)
{
    var replica = apiReplicas[i];
    gateway = gateway
        .WithReference(replica)
        .WithEnvironment($"ApiReplicas__{i}", replica.GetEndpoint("https"))
        .WaitFor(replica);
}

builder.AddProject<Projects.Client_Wasm>("client")
    .WithReference(gateway)
    .WithEnvironment("BaseAddress", gateway.GetEndpoint("https"))
    .WaitFor(gateway);

builder.Build().Run();
