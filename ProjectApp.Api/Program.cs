using Amazon.SQS;
using LocalStack.Client.Extensions;
using ProjectApp.Api.Messaging;
using ProjectApp.Api.Options;
using ProjectApp.Api.Services;
using ProjectApp.ServiceDefaults;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddRedisDistributedCache("cache");

builder.Services.Configure<CacheSettings>(builder.Configuration.GetSection("CacheSettings"));
builder.Services.AddSingleton(new JsonSerializerOptions(JsonSerializerDefaults.Web));

builder.Services.AddLocalStack(builder.Configuration);
builder.Services.AddAwsService<IAmazonSQS>();
builder.Services.AddScoped<IProducerService, SqsProducerService>();

builder.Services.AddScoped<ProgramProjectGeneratorService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "Project Generator API"
    });

    var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    var domainXmlPath = Path.Combine(AppContext.BaseDirectory, "ProjectApp.Domain.xml");
    if (File.Exists(domainXmlPath))
    {
        options.IncludeXmlComments(domainXmlPath);
    }
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
