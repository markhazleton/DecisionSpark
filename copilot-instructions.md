# Copilot Instructions for DecisionSpark

## Project Overview

**DecisionSpark** is a .NET 9 Web API that implements a flexible, config-driven decision routing engine. It guides users through minimal questions using conversation-style APIs and recommends optimal outcomes based on configurable rules.

### Technology Stack
- **Framework**: .NET 9 (C#)
- **Architecture**: ASP.NET Core Web API with MVC Views
- **Logging**: Serilog
- **API Documentation**: Swagger/OpenAPI
- **External Services**: OpenAI API integration
- **Session Management**: In-memory (Redis-ready)

### Key Components
1. **DecisionSpec** - JSON-based configuration for traits, rules, outcomes, and tie-breaking
2. **Session Store** - Maintains conversation state across API calls
3. **Routing Evaluator** - Applies rules and disambiguation logic
4. **Trait Parser** - Extracts structured data from user input
5. **Question Generator** - Phrases questions (OpenAI-enhanced)
6. **Response Mapper** - Converts internal results to API contracts

## Code Style and Conventions

### General Guidelines
- Follow existing C# coding conventions in the codebase
- Use meaningful variable and method names that reflect domain concepts
- Keep methods focused and single-purpose
- Prefer composition over inheritance
- Use dependency injection for service dependencies

### Naming Conventions
- **Classes/Interfaces**: PascalCase (e.g., `DecisionSession`, `ITraitParser`)
- **Methods**: PascalCase (e.g., `EvaluateRules`, `GenerateQuestion`)
- **Properties**: PascalCase (e.g., `SessionId`, `QuestionText`)
- **Parameters/Variables**: camelCase (e.g., `userId`, `questionId`)
- **Constants**: PascalCase or UPPER_SNAKE_CASE for static readonly
- **Interfaces**: Prefix with 'I' (e.g., `ISessionStore`, `IOpenAIService`)

### Code Organization
- **Controllers**: Handle HTTP requests, minimal business logic
- **Services**: Contain business logic and orchestration
- **Models**: 
  - `Models/Spec` - Configuration and specification models
  - `Models/Api` - Request/Response DTOs
  - `Models/Runtime` - Session and execution state models
- **Views**: Razor views for web UI demonstrations

### Error Handling
- Use structured logging via Serilog for all error scenarios
- Return appropriate HTTP status codes from controllers
- Include meaningful error messages in API responses
- Log exceptions with context (session IDs, user IDs, etc.)

### Async/Await
- Use async/await for I/O-bound operations (OpenAI calls, future DB access)
- Follow async naming convention (suffix methods with `Async`)
- Avoid blocking calls in async methods

### JSON Serialization
- Use case-insensitive property matching (`PropertyNameCaseInsensitive = true`)
- Follow camelCase for API contracts
- Include null handling for optional properties

## Documentation Standards

### Documentation File Organization

**CRITICAL RULE**: All Copilot-generated markdown files MUST be placed in the `/docs/copilot/session-{YYYY-MM}` folder structure.

#### Allowed Locations for .md Files
1. **Repository Root**: `README.md` ONLY (GitHub repository overview)
2. **Copilot Sessions**: `/docs/copilot/session-{YYYY-MM}/` (all AI-generated documentation)
3. **Future Docs**: `/docs/` subfolders for formal documentation (architecture, API specs, etc.)

#### Session Documentation Rules
- Create a new `session-{YYYY-MM}` folder for each month's AI interactions
- Name files descriptively: `feature-name-guide.md`, `implementation-summary.md`, `troubleshooting-{issue}.md`
- Include date/time stamps in session documentation for traceability
- Archive old sessions but keep them for historical reference

#### Example Structure
```
DecisionSpark/
??? README.md (GitHub repository overview - ONLY .md in root)
??? copilot-instructions.md (This file - AI guidance)
??? docs/
?   ??? copilot/
?   ?   ??? session-2025-01/
?   ?   ?   ??? openai-integration-guide.md
?   ?   ?   ??? web-ui-documentation.md
?   ?   ?   ??? debugging-fix-logging.md
?   ?   ??? session-2025-02/
?   ?       ??? (future session docs)
?   ??? architecture/ (future formal docs)
?   ??? api/ (future API specifications)
??? DecisionSpark/ (project code)
```

### Documentation Content Guidelines
- Start each document with a clear title and purpose
- Include code examples where applicable
- Document breaking changes and migration paths
- Keep documentation synchronized with code changes
- Use markdown formatting consistently (headers, code blocks, lists)

## API Design Principles

### RESTful Conventions
- Use appropriate HTTP verbs (GET, POST, PUT, DELETE)
- Return appropriate status codes (200, 201, 400, 404, 500)
- Use plural nouns for resource endpoints
- Version APIs when making breaking changes

### Request/Response Patterns
- Use DTOs (Data Transfer Objects) for API contracts
- Validate input at the controller level
- Return consistent error response formats
- Include correlation IDs for tracking

### Example Endpoints
- `POST /start` - Initialize a decision session
- `POST /conversation/{sessionId}/next` - Submit answer and get next question
- `GET /demo` - Web UI demo endpoints

## Testing Approach

### Test Coverage
- Unit tests for service layer logic (trait parsing, rule evaluation)
- Integration tests for API endpoints
- Test edge cases and error scenarios
- Mock external dependencies (OpenAI API)

### Test Naming
- Follow pattern: `MethodName_Scenario_ExpectedResult`
- Example: `EvaluateRules_WithMatchingTrait_ReturnsExpectedOutcome`

## Security and Configuration

### API Keys and Secrets
- Store sensitive configuration in `appsettings.Development.json` (excluded from git)
- Use environment variables for production secrets
- Support API key authentication for endpoints
- Document required configuration settings

### OpenAI Integration
- Configure API key via `appsettings.json` or environment variables
- Implement retry logic for external API calls
- Log API usage for monitoring and debugging
- Handle rate limiting gracefully

## Git and Version Control

### Commit Messages
- Use conventional commit format: `type(scope): description`
- Types: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`
- Example: `feat(openai): add natural language question generation`

### Branch Strategy
- `main` - stable production-ready code
- Feature branches for new capabilities
- Keep commits focused and atomic

### .gitignore Rules
- Exclude all `.md` files EXCEPT `README.md` and `copilot-instructions.md`
- Exclude `appsettings.Development.json` and `appsettings.*.json` with secrets
- Exclude build artifacts (`bin/`, `obj/`, `logs/`)

## Common Tasks and Patterns

### Adding a New Service
1. Create interface in `Services/I{ServiceName}.cs`
2. Implement service in `Services/{ServiceName}.cs`
3. Register in `Program.cs` dependency injection
4. Inject into controllers or other services as needed

### Adding a New API Endpoint
1. Create request/response models in `Models/Api/`
2. Add controller action in appropriate controller
3. Use dependency injection for required services
4. Add XML documentation comments for Swagger
5. Test endpoint via Swagger UI

### Extending DecisionSpec
1. Update `DecisionSpec.cs` model
2. Update JSON configuration files
3. Modify loader/parser logic if needed
4. Update routing evaluator for new rules
5. Document changes in session documentation

### Integrating External Services
1. Create service interface and implementation
2. Configure API keys in `appsettings.json`
3. Register service in DI container
4. Implement error handling and retry logic
5. Add structured logging for debugging

## AI-Assisted Development Guidelines

### When Using GitHub Copilot
- Review all generated code for correctness and consistency
- Ensure generated code follows project conventions
- Test generated code thoroughly before committing
- Update documentation to reflect AI-assisted changes

### Code Generation Best Practices
- Provide clear, specific prompts with context
- Reference existing patterns in the codebase
- Request explanations for complex generated code
- Iterate on generated solutions if needed

### Documentation Generation
- All AI-generated documentation goes to `/docs/copilot/session-{YYYY-MM}/`
- Include timestamps and context in session docs
- Keep README.md concise and focused on GitHub audience
- Separate implementation notes from user-facing documentation

## OpenAI Service Integration

### Current Capabilities
- Natural language question generation
- Trait extraction from free-text responses
- Enhanced conversation flow with context awareness

### Configuration
```json
{
  "OpenAI": {
    "ApiKey": "your-api-key-here",
    "Model": "gpt-4",
    "MaxTokens": 150
  }
}
```

### Usage Patterns
- Inject `IOpenAIService` into question generator or trait parser
- Provide context from decision session
- Handle API failures gracefully with fallback logic
- Log all API interactions for debugging

## Performance and Scalability

### Session Storage
- Current: In-memory dictionary (POC only)
- Future: Redis or distributed cache for production
- Design services to be cache-agnostic

### Optimization Considerations
- Cache decision specs after loading
- Minimize OpenAI API calls where possible
- Use async/await for non-blocking I/O
- Consider pagination for large result sets

## Troubleshooting and Debugging

### Logging Strategy
- Use Serilog for structured logging
- Log levels: Debug, Information, Warning, Error
- Include context: session IDs, user inputs, API responses
- Review logs in console and file outputs

### Common Issues
- **Character encoding**: Ensure UTF-8 encoding for emoji and special characters
- **Model binding**: Use custom model binders for complex request formats
- **OpenAI API errors**: Check API key, rate limits, and request formats
- **Session state**: Verify session ID consistency across requests

### Debug Tools
- Swagger UI for API testing: `/swagger`
- Web UI demos: `/demo` endpoints
- Serilog file logs: `logs/decisionspark-{date}.txt`
- Browser developer tools for front-end issues

## Future Enhancements

### Roadmap Considerations
- Redis integration for distributed session storage
- Database persistence for decision specs and audit logs
- Advanced OpenAI features (function calling, streaming)
- Multi-tenant support with API key management
- Real-time analytics and monitoring
- WebSocket support for live conversations

## Questions and Support

### Getting Help
- Review existing session documentation in `/docs/copilot/`
- Check README.md for high-level project overview
- Examine code comments and XML documentation
- Test changes using Swagger UI and web demos

### Contributing
- Follow all conventions outlined in this document
- Place all session notes in appropriate `/docs/copilot/session-{YYYY-MM}/` folder
- Keep README.md as the single source of truth for GitHub
- Update this file when adding new patterns or conventions

---

**Last Updated**: January 2025
**Version**: 1.0
**Maintainer**: DecisionSpark Project Team
