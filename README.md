# DecisionSpark

[![.NET Version](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)]()
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](CONTRIBUTING.md)

> A Dynamic Decision Routing Engine that uses conversation-style APIs to guide users through minimal questions and recommend optimal outcomes.

## 🚀 Overview

DecisionSpark is a .NET 10 web application that implements a flexible, config-driven decision engine. It combines a RESTful API with an interactive Razor Pages web interface to guide users through intelligent conversations, asking minimal questions while evaluating responses against configurable rules to recommend optimal outcomes.

### Example Use Case: Family Saturday Planner
- **Ask**: "How many people?" → "What ages?"
- **Evaluate**: group size, age ranges, derived traits
- **Recommend**: Go Bowling / Movie Night / Go Golfing

### Key Features

✅ **Conversation-Driven API** - RESTful endpoints for starting and continuing decision sessions  
✅ **DecisionSpec Management Console** - Admin UI and API for creating, editing, and managing decision specifications  
✅ **LLM-Assisted Spec Creation** - Generate draft specifications from natural language instructions  
✅ **Multiple Question Types** - Text input, single-select, and multi-select with options  
✅ **OpenAI Integration** - Natural language question generation and answer parsing  
✅ **Web UI Interface** - Interactive Razor Pages testing interface  
✅ **Config-Driven Rules** - JSON-based decision specifications with no code changes  
✅ **Intelligent Routing** - Rule-based evaluation with derived traits and tie-breaking  
✅ **Session Management** - File-based conversation persistence  
✅ **API Documentation** - Swagger/OpenAPI with interactive testing  
✅ **Structured Logging** - Serilog with console and rolling file outputs  

### New: DecisionSpec Management Console

The DecisionSpec Management Console provides a comprehensive CRUD interface for creating and managing decision specifications:

- **Admin UI**: Web-based interface for creating, editing, and managing DecisionSpecs
- **REST API**: Full CRUD operations with optimistic concurrency control (ETags)
- **LLM-Assisted Drafting**: Generate spec drafts from natural language instructions using Azure OpenAI
- **Lifecycle Management**: Track specs through Draft → In Review → Published → Retired states
- **Validation**: Real-time validation with field-level error reporting
- **Audit Trail**: Complete history of all changes with user attribution
- **Soft Delete**: 30-day retention for deleted specs with restoration capability
- **Search & Filter**: Find specs by status, owner, or search terms
- **Version Control**: Full version history and comparison

**Quick Start:**
- **Admin UI**: `https://localhost:5001/Admin/DecisionSpecs`
- **API Endpoints**: `https://localhost:5001/api/decisionspecs`
- **Documentation**: See `/docs/copilot/decision-specs-llm.md` for LLM integration details

## 📋 Table of Contents

- [Architecture](#architecture)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [API Reference](#api-reference)
- [Configuration](#configuration)
- [Web UI](#web-ui)
- [Decision Specs](#decision-specs)
- [Development](#development)
- [Testing](#testing)
- [Deployment](#deployment)
- [Contributing](#contributing)
- [License](#license)

## 🏗️ Architecture

### Core Components

```
┌─────────────────────────────────────────────────────────────┐
│                    DecisionSpark Engine                      │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │   Web UI     │  │  API Layer   │  │    Swagger   │      │
│  │ (Razor Pages)│  │ (REST API)   │  │   (OpenAPI)  │      │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘      │
│         │                  │                  │               │
│         └──────────────────┴──────────────────┘              │
│                            │                                  │
│         ┌──────────────────┴───────────────────┐            │
│         │                                        │            │
│  ┌──────▼──────┐  ┌──────────────┐  ┌─────────▼────┐      │
│  │  Session    │  │   Question   │  │   Response   │      │
│  │   Store     │  │  Generator   │  │    Mapper    │      │
│  └─────────────┘  └──────┬───────┘  └──────────────┘      │
│                           │                                  │
│  ┌─────────────┐  ┌──────▼───────┐  ┌──────────────┐      │
│  │   Routing   │  │    Trait     │  │   OpenAI     │      │
│  │  Evaluator  │  │    Parser    │  │   Service    │      │
│  └─────────────┘  └──────────────┘  └──────────────┘      │
│                                                               │
│  ┌────────────────────────────────────────────────────┐    │
│  │          DecisionSpec Loader (JSON)                 │    │
│  └────────────────────────────────────────────────────┘    │
│                                                               │
└─────────────────────────────────────────────────────────────┘
```

### Key Services

| Service | Purpose | Implementation |
|---------|---------|----------------|
| **DecisionSpecLoader** | Loads and validates JSON decision configurations | `FileSystemDecisionSpecLoader` |
| **SessionStore** | Maintains conversation state | `InMemorySessionStore` |
| **RoutingEvaluator** | Applies rules and determines outcomes | `RoutingEvaluator` |
| **TraitParser** | Extracts structured data from user input | `TraitParser` |
| **QuestionGenerator** | Phrases natural language questions | `OpenAIQuestionGenerator` |
| **ResponseMapper** | Converts internal results to API contracts | `ResponseMapper` |
| **OpenAIService** | Integration with OpenAI/Azure OpenAI | `OpenAIService` |
| **ConversationPersistence** | Saves conversation history | `FileConversationPersistence` |

## 📦 Prerequisites

- **.NET 10 SDK** or later ([Download](https://dotnet.microsoft.com/download))
- **Visual Studio 2022** (v17.12+) or **VS Code** with C# extension
- **OpenAI API Key** or **Azure OpenAI** credentials (optional, has fallback mode)
- **Git** for version control

### Development Tools (Recommended)

- **Postman** or **curl** for API testing
- **Azure CLI** for Azure deployments
- **Docker** (optional, for containerized deployment)

## 🚀 Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/markhazleton/DecisionSpark.git
cd DecisionSpark
```

### 2. Configure Application Settings

Edit `appsettings.json` or use User Secrets for sensitive data:

```bash
cd DecisionSpark
dotnet user-secrets set "OpenAI:ApiKey" "your-openai-api-key"
dotnet user-secrets set "DecisionEngine:ApiKey" "your-custom-api-key"
```

**Minimal Configuration** (`appsettings.json`):

```json
{
  "DecisionEngine": {
    "ConfigPath": "Config/DecisionSpecs",
    "DefaultSpecId": "TECH_STACK_ADVISOR_V1",
    "ApiKey": "dev-api-key-change-in-production"
  },
  "OpenAI": {
    "Provider": "OpenAI",
    "ApiKey": "your-openai-api-key-here",
    "Model": "gpt-4",
    "EnableFallback": true
  }
}
```

### 3. Build and Run

```bash
dotnet restore
dotnet build
dotnet run --project DecisionSpark
```

The application will start at:
- **HTTPS**: `https://localhost:5001`
- **HTTP**: `http://localhost:5000`

### 4. Access the Application

- **Web UI**: `https://localhost:5001/`
- **API Documentation**: `https://localhost:5001/swagger`
- **About Page**: `https://localhost:5001/about`

## 📚 API Reference

### Base URL

```
https://localhost:5001
```

All API endpoints require the `X-API-KEY` header for authentication.

### Authentication

```http
X-API-KEY: dev-api-key-change-in-production
```

### Endpoints

#### Get Available Decision Specs

```http
GET /conversation/specs
```

**Response:**
```json
{
  "specs": [
    {
      "specId": "FAMILY_SATURDAY_V1",
      "fileName": "FAMILY_SATURDAY_V1.1.0.0.active.json",
      "displayName": "FAMILY SATURDAY V1",
      "isDefault": false
    },
    {
      "specId": "TECH_STACK_ADVISOR_V1",
      "fileName": "TECH_STACK_ADVISOR_V1.0.0.0.active.json",
      "displayName": "TECH STACK ADVISOR V1",
      "isDefault": true
    }
  ]
}
```

#### Start a Decision Conversation

```http
POST /conversation/start
Content-Type: application/json
X-API-KEY: your-api-key

{
  "spec_id": "TECH_STACK_ADVISOR_V1"
}
```

**Response (Question):**
```json
{
  "texts": ["Let's find the best technology stack for your project."],
  "question": {
    "id": "project_type",
    "source": "TECH_STACK_ADVISOR_V1",
    "text": "What type of project are you building?",
    "type": "single-select",
    "allowFreeText": true,
    "isFreeText": false,
    "options": [
      {
        "id": "web-app",
        "label": "Web Application",
        "value": "web-app",
        "isNegative": false
      },
      {
        "id": "mobile-app",
        "label": "Mobile Application",
        "value": "mobile-app",
        "isNegative": false
      }
    ]
  },
  "next_url": "https://localhost:5001/conversation/abc123/next"
}
```

**Response (Final Recommendation):**
```json
{
  "is_complete": true,
  "texts": ["Based on your requirements, here's my recommendation:"],
  "display_cards": [...],
  "care_type_message": "React with .NET Core API",
  "final_result": {
    "outcome_id": "REACT_DOTNET",
    "resolution_button_label": "Get Started",
    "resolution_button_url": "https://example.com/react-dotnet",
    "analytics_resolution_code": "ROUTE_REACT_DOTNET"
  }
}
```

#### Continue Conversation

```http
POST /conversation/{sessionId}/next
Content-Type: application/json
X-API-KEY: your-api-key

{
  "user_input": "web application"
}
```

Or with structured selection:

```json
{
  "selected_option_ids": ["web-app"],
  "user_input": "I want to build a web app"
}
```

**Response:** Same structure as Start endpoint

### Error Responses

**Invalid Input (400):**
```json
{
  "error": {
    "code": "INVALID_INPUT",
    "message": "Could not parse response. Please provide a valid selection."
  },
  "question": { ...rephrased question... },
  "next_url": "..."
}
```

**Session Not Found (404):**
```json
{
  "error": "Session not found"
}
```

**Unauthorized (401):**
```json
{
  "error": "Invalid or missing API key"
}
```

## ⚙️ Configuration

### Application Settings

| Setting | Description | Default |
|---------|-------------|---------|
| `DecisionEngine:ConfigPath` | Path to decision spec JSON files | `Config/DecisionSpecs` |
| `DecisionEngine:DefaultSpecId` | Default spec to use | `TECH_STACK_ADVISOR_V1` |
| `DecisionEngine:ApiKey` | API authentication key | `dev-api-key-change-in-production` |
| `OpenAI:Provider` | Provider type (`OpenAI` or `Azure`) | `OpenAI` |
| `OpenAI:ApiKey` | OpenAI API key | - |
| `OpenAI:Model` | Model to use | `gpt-4` |
| `OpenAI:EnableFallback` | Use fallback if OpenAI unavailable | `true` |
| `ConversationStorage:Path` | Path for conversation persistence | `conversations` |
| `Serilog:LogDirectory` | Log file directory | `logs` |

### OpenAI Configuration

**OpenAI (Direct):**
```json
{
  "OpenAI": {
    "Provider": "OpenAI",
    "ApiKey": "sk-...",
    "Model": "gpt-4",
    "MaxTokens": 500,
    "Temperature": 0.7,
    "EnableFallback": true
  }
}
```

**Azure OpenAI:**
```json
{
  "OpenAI": {
    "Provider": "Azure",
    "Endpoint": "https://your-resource.openai.azure.com",
    "ApiKey": "your-azure-key",
    "DeploymentName": "gpt-4",
    "EnableFallback": true
  }
}
```

## 🖥️ Web UI

DecisionSpark includes a full-featured Razor Pages web interface for testing and demonstrating the decision engine.

### Features

- 📝 **Interactive Conversation Interface** - Real-time question/answer flow
- 🎯 **Multiple Question Types** - Text inputs, single-select, and multi-select
- 📊 **Spec Selection** - Choose from available decision specifications
- 🔍 **Debug Console** - View session state and evaluation details
- 📜 **Conversation History** - Track all questions and answers
- 💾 **Session Persistence** - Save and resume conversations

### Access

Navigate to `https://localhost:5001/` to access the interactive testing interface.

## 📝 Decision Specs

Decision specifications are JSON configuration files that define the conversation flow and decision logic.

### File Naming Convention

```
{SPEC_ID}.{MAJOR}.{MINOR}.{PATCH}.{BUILD}.active.json
```

Example: `TECH_STACK_ADVISOR_V1.0.0.0.active.json`

### Spec Structure

```json
{
  "spec_id": "TECH_STACK_ADVISOR_V1",
  "version": "1.0.0.0",
  "metadata": {
    "display_name": "Technology Stack Advisor",
    "description": "Recommends optimal technology stacks"
  },
  "traits": [
    {
      "key": "project_type",
      "question_text": "What type of project?",
      "answer_type": "list",
      "validation": {
        "allowed_values": ["web", "mobile", "desktop"]
      }
    }
  ],
  "derived_traits": [
    {
      "key": "complexity_score",
      "expression": "team_size * 2 + feature_count"
    }
  ],
  "immediate_select_if": [
    {
      "condition": "project_type == legacy",
      "outcome_id": "MAINTAIN_CURRENT"
    }
  ],
  "outcomes": [
    {
      "outcome_id": "REACT_DOTNET",
      "display_name": "React with .NET Core",
      "selection_rules": [
        "project_type == web",
        "team_size >= 3"
      ]
    }
  ]
}
```

### Available Specs

1. **FAMILY_SATURDAY_V1** - Family activity planner
2. **TECH_STACK_ADVISOR_V1** - Technology stack recommendations

### Creating Custom Specs

1. Create a new JSON file in `Config/DecisionSpecs/`
2. Follow the naming convention
3. Define traits, rules, and outcomes
4. Update `DefaultSpecId` in `appsettings.json`

## 🛠️ Development

### Project Structure

```
DecisionSpark/
├── Config/
│   └── DecisionSpecs/              # JSON decision specifications
├── Controllers/
│   ├── ConversationController.cs   # API endpoints
│   └── HomeController.cs           # Web UI controllers
├── Models/
│   ├── Api/                        # Request/Response DTOs
│   │   ├── RequestModels.cs
│   │   └── ResponseModels.cs
│   ├── Runtime/                    # Session and evaluation models
│   │   ├── DecisionSession.cs
│   │   └── EvaluationResult.cs
│   └── Spec/                       # DecisionSpec models
│       └── DecisionSpec.cs
├── Services/                       # Core business logic
│   ├── ISessionStore.cs
│   ├── IDecisionSpecLoader.cs
│   ├── IRoutingEvaluator.cs
│   ├── ITraitParser.cs
│   ├── IQuestionGenerator.cs
│   ├── IResponseMapper.cs
│   ├── IOpenAIService.cs
│   ├── OpenAIService.cs
│   ├── OpenAIQuestionGenerator.cs
│   └── IConversationPersistence.cs
├── Middleware/
│   └── ApiKeyAuthenticationMiddleware.cs
├── Views/                          # Razor Pages views
│   ├── Home/
│   │   ├── Index.cshtml
│   │   └── About.cshtml
│   └── Shared/
│       └── Components/
│           └── Questions/
├── Common/
│   └── Constants.cs
├── Program.cs                      # Application startup
└── appsettings.json                # Configuration

tests/
└── DecisionSpark.Tests/            # Unit and integration tests
    ├── Services/
    ├── Controllers/
    └── DecisionSpark.Tests.csproj
```

### Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Azure.AI.OpenAI | 2.1.0 | OpenAI and Azure OpenAI integration |
| Serilog.AspNetCore | 10.0.0 | Structured logging |
| Swashbuckle.AspNetCore | 9.0.6 | Swagger/OpenAPI documentation |

### Building

```bash
# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Build specific project
dotnet build DecisionSpark/DecisionSpark.csproj
```

### Running Locally

```bash
# Run with default settings
dotnet run --project DecisionSpark

# Run with specific environment
dotnet run --project DecisionSpark --environment Development

# Watch mode (auto-reload on changes)
dotnet watch run --project DecisionSpark
```

## 🧪 Testing

### Test Framework

- **xUnit** - Test runner
- **Moq** - Mocking framework
- **FluentAssertions** - Assertion library

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true

# Run specific test project
dotnet test tests/DecisionSpark.Tests/DecisionSpark.Tests.csproj

# Run tests with detailed output
dotnet test --logger "console;verbosity=detailed"
```

### Test API with curl

```bash
# Get available specs
curl -X GET https://localhost:5001/conversation/specs \
  -H "X-API-KEY: dev-api-key-change-in-production" \
  -k

# Start conversation
curl -X POST https://localhost:5001/conversation/start \
  -H "Content-Type: application/json" \
  -H "X-API-KEY: dev-api-key-change-in-production" \
  -d '{"spec_id": "TECH_STACK_ADVISOR_V1"}' \
  -k

# Continue conversation
curl -X POST https://localhost:5001/conversation/{sessionId}/next \
  -H "Content-Type: application/json" \
  -H "X-API-KEY: dev-api-key-change-in-production" \
  -d '{"user_input": "web application"}' \
  -k
```

## 🚀 Deployment

### Local Deployment

```bash
dotnet publish -c Release -o ./publish
cd publish
dotnet DecisionSpark.dll
```

### Azure App Service

```bash
# Login to Azure
az login

# Create App Service
az webapp create --name decisionspark \
  --resource-group myResourceGroup \
  --plan myAppServicePlan \
  --runtime "DOTNET|10.0"

# Deploy
az webapp deployment source config-zip \
  --resource-group myResourceGroup \
  --name decisionspark \
  --src ./publish.zip
```

### Docker

```dockerfile
# Example Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["DecisionSpark/DecisionSpark.csproj", "DecisionSpark/"]
RUN dotnet restore "DecisionSpark/DecisionSpark.csproj"
COPY . .
WORKDIR "/src/DecisionSpark"
RUN dotnet build "DecisionSpark.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "DecisionSpark.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "DecisionSpark.dll"]
```

### Production Configuration

**Use Azure Key Vault or User Secrets for sensitive data:**

```bash
# Set up User Secrets
dotnet user-secrets init --project DecisionSpark
dotnet user-secrets set "OpenAI:ApiKey" "your-prod-key" --project DecisionSpark
dotnet user-secrets set "DecisionEngine:ApiKey" "your-prod-api-key" --project DecisionSpark
```

**Environment Variables:**

```bash
export DecisionEngine__ApiKey="your-secure-api-key"
export OpenAI__ApiKey="your-openai-key"
export ASPNETCORE_ENVIRONMENT="Production"
```

## 🤝 Contributing

Contributions are welcome! Please follow these guidelines:

1. **Fork the repository**
2. **Create a feature branch** (`git checkout -b feature/AmazingFeature`)
3. **Commit your changes** (`git commit -m 'Add some AmazingFeature'`)
4. **Push to the branch** (`git push origin feature/AmazingFeature`)
5. **Open a Pull Request**

### Development Focus Areas

- 🔧 Enhanced rule expression engine
- 🌐 Additional trait parsers
- 🧪 Expanded test coverage
- 📊 Analytics and telemetry
- 🔄 Redis session persistence
- 🎯 LLM-powered tie resolution
- ⏮️ `/prev` endpoint for backwards navigation
- 👥 Admin API for spec management

### Code Style

- Follow standard C# naming conventions
- Use XML documentation comments for public APIs
- Maintain existing code structure and patterns
- Write unit tests for new features
- Update documentation for API changes

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Built with **.NET 10** and **ASP.NET Core**
- Powered by **Azure AI OpenAI** SDK
- Structured logging with **Serilog**
- API documentation via **Swagger/OpenAPI**
- Testing with **xUnit**, **Moq**, and **FluentAssertions**

## 📧 Contact

**Mark Hazleton** - [GitHub](https://github.com/markhazleton)

**Project Link**: [https://github.com/markhazleton/DecisionSpark](https://github.com/markhazleton/DecisionSpark)

---

**DecisionSpark** - Smart decisions through minimal questions. 🎯
