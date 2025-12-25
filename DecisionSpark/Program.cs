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
builder.Services.AddSingleton<IRoutingEvaluator, RoutingEvaluator>();
builder.Services.AddSingleton<IQuestionGenerator, StubQuestionGenerator>();
builder.Services.AddSingleton<IResponseMapper, ResponseMapper>();
builder.Services.AddSingleton<ITraitParser, TraitParser>();

var app = builder.Build();

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
