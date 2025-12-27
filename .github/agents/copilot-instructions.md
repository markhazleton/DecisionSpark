# DecisionSpark Development Guidelines

Auto-generated from all feature plans. Last updated: 2025-12-25

## Active Technologies
- C# 12 targeting .NET 9/10 multi-targeted ASP.NET Core MVC + ASP.NET Core MVC, EF Core 9 (SQLite + SQL Server providers), FluentValidation, System.Text.Json, existing OpenAI orchestration service (001-decisionspecs-crud)
- New `DecisionSpecsDbContext` over SQLite (local/dev) with provider abstraction for SQL Server/Azure SQL in higher envs; stores canonical JSON plus projection tables for search/filter (001-decisionspecs-crud)
- C# 12 targeting ASP.NET Core MVC on .NET 10 GA + ASP.NET Core MVC, FluentValidation, System.Text.Json, Microsoft.Extensions.FileProviders, existing OpenAI integration, Application Insights SDK (001-decisionspecs-crud)
- JSON spec files under `DecisionSpecs:RootPath` with status-specific folders plus sidecar indexes/audit logs managed through a dedicated file store (001-decisionspecs-crud)

- C# 13 on .NET 10.0 (ASP.NET Core MVC) + ASP.NET Core MVC, Azure.AI.OpenAI 2.1.0, Serilog.AspNetCore 10.0.0, Swashbuckle.AspNetCore 9.0.6 (001-question-types)

## Project Structure

```text
backend/
frontend/
tests/
```

## Commands

# Add commands for C# 13 on .NET 10.0 (ASP.NET Core MVC)

## Code Style

C# 13 on .NET 10.0 (ASP.NET Core MVC): Follow standard conventions

## Recent Changes
- 001-decisionspecs-crud: Added C# 12 targeting ASP.NET Core MVC on .NET 10 GA + ASP.NET Core MVC, FluentValidation, System.Text.Json, Microsoft.Extensions.FileProviders, existing OpenAI integration, Application Insights SDK
- 001-decisionspecs-crud: Added C# 12 targeting ASP.NET Core MVC on .NET 10 GA + ASP.NET Core MVC, FluentValidation, System.Text.Json, Microsoft.Extensions.FileProviders, existing OpenAI integration, Application Insights SDK
- 001-decisionspecs-crud: Added C# 12 targeting .NET 9/10 multi-targeted ASP.NET Core MVC + ASP.NET Core MVC, EF Core 9 (SQLite + SQL Server providers), FluentValidation, System.Text.Json, existing OpenAI orchestration service


<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
