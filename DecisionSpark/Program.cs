using DecisionSpark.Middleware;
using DecisionSpark.Services;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog - using bootstrap logger pattern for better startup logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/decisionspark-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting web application");

    builder.Host.UseSerilog();

    // Add services to the container - using single AddControllersWithViews call
    builder.Services.AddControllersWithViews()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
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

    // Register Decision Engine services - using keyed services for better organization
    builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();
    builder.Services.AddSingleton<IDecisionSpecLoader, FileSystemDecisionSpecLoader>();
    builder.Services.AddSingleton<IOpenAIService, OpenAIService>();
    builder.Services.AddSingleton<IRoutingEvaluator, RoutingEvaluator>();
    builder.Services.AddSingleton<IResponseMapper, ResponseMapper>();
    builder.Services.AddSingleton<ITraitParser, TraitParser>();

    // Register question generator - use OpenAI version if available, fallback to stub
    var useOpenAI = builder.Configuration.GetValue<bool>("OpenAI:EnableFallback", true);
    if (useOpenAI)
    {
        builder.Services.AddSingleton<IQuestionGenerator, OpenAIQuestionGenerator>();
        Log.Information("Configured OpenAI-powered question generator");
    }
    else
    {
        builder.Services.AddSingleton<IQuestionGenerator, StubQuestionGenerator>();
        Log.Information("Configured stub question generator");
    }

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

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "DecisionSpark API v1");
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "DecisionSpark API";
            options.DisplayRequestDuration();
        });
    }
    else
    {
        app.UseExceptionHandler("/Home/Error");
        app.UseHsts();
    }

    app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();

    // API Key Authentication - must be after UseRouting and before UseAuthorization
    app.UseApiKeyAuthentication();

    app.UseAuthorization();

    app.MapControllers();
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");

    Log.Information("DecisionSpark API starting on {Environment}", app.Environment.EnvironmentName);
    Log.Information("Swagger UI available at: /swagger");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
