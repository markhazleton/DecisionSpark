using DecisionSpark.Services;
using Serilog;

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
builder.Services.AddControllers();
builder.Services.AddOpenApi();

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
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

Log.Information("DecisionSpark API starting...");

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
