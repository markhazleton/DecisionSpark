using DecisionSpark.Middleware;
using DecisionSpark.Core.Services;
using DecisionSpark.Core.Models.Configuration;
using DecisionSpark.Core.Persistence.FileStorage;
using DecisionSpark.Core.Persistence.Repositories;
using DecisionSpark.Core.Services.Validation;
using DecisionSpark.Swagger;
using FluentValidation;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog - using bootstrap logger pattern for better startup logging
var logDirectory = builder.Configuration["Serilog:LogDirectory"] ?? "logs";
var logPath = Path.Combine(logDirectory, "decisionspark-.txt");

// Ensure log directory exists
Directory.CreateDirectory(logDirectory);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Error)
    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
    .CreateLogger();

// Clear default providers to prevent duplication
builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);

try
{
    Log.Information("Starting web application");

    builder.Host.UseSerilog();

    // Configure DecisionSpecs options with validation
    builder.Services.AddOptions<DecisionSpecsOptions>()
        .BindConfiguration(DecisionSpecsOptions.SectionName)
        .ValidateOnStart();
    builder.Services.AddSingleton<IValidateOptions<DecisionSpecsOptions>, DecisionSpecsOptionsValidator>();

    // Add services to the container - using single AddControllersWithViews call
    builder.Services.AddControllersWithViews()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        });

    // Add health checks for DecisionSpecs directory
    builder.Services.AddHealthChecks()
        .AddCheck("DecisionSpecs", () =>
        {
            var options = builder.Services.BuildServiceProvider().GetRequiredService<IOptions<DecisionSpecsOptions>>().Value;
            var rootPath = options.RootPath;
            
            if (!Directory.Exists(rootPath))
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy($"DecisionSpecs directory does not exist: {rootPath}");
            }

            try
            {
                var testFile = Path.Combine(rootPath, $".health-check-{Guid.NewGuid()}.tmp");
                File.WriteAllText(testFile, "health check");
                File.Delete(testFile);
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("DecisionSpecs directory is writable");
            }
            catch (Exception ex)
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy($"DecisionSpecs directory is not writable: {ex.Message}");
            }
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

        // Use fully qualified type names to avoid schema ID conflicts
        options.CustomSchemaIds(type => type.FullName?.Replace("+", "."));

        // Configure to handle Dictionary<string, object> and polymorphic types
        options.UseAllOfToExtendReferenceSchemas();
        options.UseOneOfForPolymorphism();
        options.UseAllOfForInheritance();
        
        // Handle Dictionary<string, object> as additionalProperties
        options.MapType<Dictionary<string, object>>(() => new OpenApiSchema
        {
            Type = "object",
            AdditionalPropertiesAllowed = true,
            AdditionalProperties = new OpenApiSchema
            {
                Type = "object"
            }
        });
        
        // Add schema filter to handle List<object> and other complex types
        options.SchemaFilter<ObjectTypeSchemaFilter>();

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
    
    // Register new services for question type feature
    builder.Services.AddSingleton<IOptionIdGenerator, OptionIdGenerator>();
    builder.Services.AddSingleton<IQuestionPresentationDecider, QuestionPresentationDecider>();
    builder.Services.AddScoped<IUserSelectionService, UserSelectionService>();
    builder.Services.AddSingleton<IConversationPersistence, FileConversationPersistence>();

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

    // Register DecisionSpec file storage and repository services
    builder.Services.AddSingleton<DecisionSpecFileStore>();
    builder.Services.AddSingleton<FileSearchIndexer>();
    builder.Services.AddSingleton<IDecisionSpecRepository, DecisionSpecRepository>();
    builder.Services.AddScoped<QuestionPatchService>();
    
    // Register FluentValidation validators
    builder.Services.AddValidatorsFromAssemblyContaining<DecisionSpecValidator>();
    builder.Services.AddValidatorsFromAssemblyContaining<DecisionSpark.Areas.Admin.ViewModels.DecisionSpecs.DecisionSpecEditViewModelValidator>();
    
    // Register index refresh background service
    builder.Services.AddHostedService<IndexRefreshHostedService>();

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
    
    // Validation error handling middleware
    app.UseValidationProblemDetails();
    
    app.UseRouting();

    // API Key Authentication - must be after UseRouting and before UseAuthorization
    app.UseApiKeyAuthentication();

    app.UseAuthorization();

    app.MapControllers();
    
    // Map Admin area route
    app.MapControllerRoute(
        name: "admin",
        pattern: "Admin/{controller=DecisionSpecs}/{action=Index}/{id?}",
        defaults: new { area = "Admin" });
    
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}");
    
    // Map health check endpoints
    app.MapHealthChecks("/health");

    Log.Information("DecisionSpark API starting on {Environment}", app.Environment.EnvironmentName);
    Log.Information("Swagger UI available at: /swagger");
    Log.Information("Health check available at: /health");

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
