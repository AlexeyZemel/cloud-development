using Amazon.SQS;
using LocalStack.Client.Extensions;
using ProjectApp.FileService.Messaging;
using ProjectApp.FileService.Storage;
using ProjectApp.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddLocalStack(builder.Configuration);
builder.Services.AddAwsService<IAmazonSQS>();
builder.Services.AddHostedService<SqsConsumerService>();

builder.AddMinioClient("projectapp-minio");
builder.Services.AddScoped<IS3Service, S3MinioService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using var scope = app.Services.CreateScope();

var s3 = scope.ServiceProvider.GetRequiredService<IS3Service>();
await s3.EnsureBucketExists();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapDefaultEndpoints();
app.MapControllers();

app.Run();
