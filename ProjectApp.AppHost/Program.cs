using Amazon;
using Aspire.Hosting.LocalStack.Container;

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("cache")
    .WithRedisCommander();

var awsConfig = builder.AddAWSSDKConfig()
    .WithProfile("default")
    .WithRegion(RegionEndpoint.EUCentral1);

var localstack = builder
    .AddLocalStack("projectapp-localstack", awsConfig: awsConfig, configureContainer: container =>
    {
        container.Lifetime = ContainerLifetime.Session;
        container.DebugLevel = 1;
        container.LogLevel = LocalStackLogLevel.Debug;
        container.Port = 4566;
        container.AdditionalEnvironmentVariables.Add("DEBUG", "1");
    });

var awsResources = builder
    .AddAWSCloudFormationTemplate("resources", "CloudFormation/projectapp-template.yaml", "projectapp")
    .WithReference(awsConfig);

var minio = builder.AddMinioContainer("projectapp-minio");

const int replicaCount = 5;
var apiReplicas = new List<IResourceBuilder<ProjectResource>>(replicaCount);

var gateway = builder.AddProject<Projects.ProjectApp_Gateway>("projectapp-gateway")
    .WithExternalHttpEndpoints();

for (var i = 0; i < replicaCount; i++)
{
    var replica = builder.AddProject<Projects.ProjectApp_Api>($"projectapp-api-{i + 1}")
        .WithReference(redis)
        .WithReference(awsResources)
        .WaitFor(redis)
        .WaitFor(awsResources);

    apiReplicas.Add(replica);

    gateway = gateway
        .WithReference(replica)
        .WithEnvironment($"ApiReplicas__{i}", replica.GetEndpoint("https"))
        .WaitFor(replica);
}

builder.AddProject<Projects.Client_Wasm>("client")
    .WithReference(gateway)
    .WithEnvironment("BaseAddress", gateway.GetEndpoint("https"))
    .WaitFor(gateway);

builder.AddProject<Projects.ProjectApp_FileService>("projectapp-fileservice")
    .WithReference(awsResources)
    .WithReference(minio)
    .WithEnvironment("AWS__Resources__MinioBucketName", "projectapp-bucket")
    .WaitFor(awsResources)
    .WaitFor(minio);

builder.UseLocalStack(localstack);

builder.Build().Run();
