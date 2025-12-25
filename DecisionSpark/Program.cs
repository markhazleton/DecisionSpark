using DecisionSpark.Services;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/decisionspark-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        Console.WriteLine("[Startup] JSON options configured: PropertyNameCaseInsensitive = true");
    });
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        Console.WriteLine("[Startup] MVC JSON options configured: PropertyNameCaseInsensitive = true");
    });

// Add Swagger/OpenAPI with custom configuration
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
  options.SwaggerDoc("v1", new OpenApiInfo
    {
      Title = "DecisionSpark API",
        Version = "v1",
        Description = "Dynamic Decision Routing Engine - Guides users through minimal questions to recommend optimal outcomes",
Contact = new OpenApiContact
 {
       Name = "DecisionSpark",
            Url = new Uri("https://github.com/markhazleton/DecisionSpark")
        }
    });

    // Add API Key authentication to Swagger UI
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
 {
        Type = SecuritySchemeType.ApiKey,
        Name = "X-API-KEY",
        In = ParameterLocation.Header,
        Description = "API Key required for authentication. Use: dev-api-key-change-in-production"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
    new OpenApiSecurityScheme
          {
    Reference = new OpenApiReference
  {
         Type = ReferenceType.SecurityScheme,
             Id = "ApiKey"
          }
            },
            Array.Empty<string>()
        }
    });

  // Include XML comments
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// Register Decision Engine services
builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();
builder.Services.AddSingleton<IDecisionSpecLoader, FileSystemDecisionSpecLoader>();

// Register OpenAI services
builder.Services.AddSingleton<IOpenAIService, OpenAIService>();

// Register routing evaluator with OpenAI support
builder.Services.AddSingleton<IRoutingEvaluator, RoutingEvaluator>();

// Register question generator - use OpenAI version if available, fallback to stub
var useOpenAI = builder.Configuration.GetValue<bool>("OpenAI:EnableFallback", true);
if (useOpenAI)
{
    builder.Services.AddSingleton<IQuestionGenerator, OpenAIQuestionGenerator>();
    Console.WriteLine("[Startup] Using OpenAI-powered question generator");
}
else
{
    builder.Services.AddSingleton<IQuestionGenerator, StubQuestionGenerator>();
    Console.WriteLine("[Startup] Using stub question generator");
}

builder.Services.AddSingleton<IResponseMapper, ResponseMapper>();

// Register trait parser with OpenAI support
builder.Services.AddSingleton<ITraitParser, TraitParser>();

var app = builder.Build();

// Log OpenAI configuration status
var openAIService = app.Services.GetRequiredService<IOpenAIService>();
if (openAIService.IsAvailable())
{
    Log.Information("OpenAI service is configured and available");
}
else
{
    Log.Warning("OpenAI service is not configured - using fallback mode");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "DecisionSpark API v1");
        options.RoutePrefix = "swagger"; // Move Swagger to /swagger
        options.DocumentTitle = "DecisionSpark API";
        options.DisplayRequestDuration();
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Enable static files
app.UseRouting(); // Enable routing
app.UseAuthorization();

app.MapControllers();
app.MapControllerRoute(
    name: "default",
  pattern: "{controller=Home}/{action=Index}/{id?}"); // Add MVC routing

Log.Information("DecisionSpark API starting...");
Log.Information("Web UI available at: https://localhost:44356");
Log.Information("Swagger UI available at: https://localhost:44356/swagger");

try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
