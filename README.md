# DecisionSpark

A Dynamic Decision Routing Engine that uses conversation-style APIs to guide users through minimal questions and recommend optimal outcomes.

## Overview

DecisionSpark is a .NET 9 Web API that implements a flexible, config-driven decision engine. It asks users minimal questions, evaluates responses against configurable rules, and routes to optimal outcomes.

### Example Use Case: Family Saturday Planner
- Ask: "How many people?" ? "What ages?"
- Evaluate: group size, age ranges, derived traits
- Recommend: Go Bowling / Movie Night / Go Golfing

## Architecture

### Core Components

1. **DecisionSpec** (JSON config): Defines traits, rules, outcomes, and tie-breaking strategies
2. **Session Store**: Maintains conversation state (in-memory for POC, Redis-ready)
3. **Routing Evaluator**: Applies immediate rules, outcome rules, and disambiguation logic
4. **Trait Parser**: Extracts structured data from free-text user input
5. **Question Generator**: Phrases questions (stub for now, OpenAI-ready)
6. **Response Mapper**: Converts internal results to API contract shapes

### API Endpoints

#### `POST /start`
Initializes a decision session and returns the first question or final recommendation.

**Request:**
```json
{}
```

**Response (needs info):**
```json
{
  "texts": ["Let's figure out the best plan for Saturday."],
  "question": {
    "id": "group_size",
    "source": "FAMILY_SATURDAY_V1",
    "text": "How many people are going on the outing?",
    "allow_free_text": true,
    "is_free_text": true,
    "type": "text"
  },
  "next_url": "https://api.example.com/v2/pub/conversation/abc123/next"
}
```

**Response (immediate outcome):**
```json
{
  "is_complete": true,
  "texts": ["Here's what I recommend:"],
  "display_cards": [...],
  "care_type_message": "Bowling is perfect for your crew",
  "final_result": {
    "outcome_id": "GO_BOWLING",
    "resolution_button_label": "Reserve a lane",
    "resolution_button_url": "https://example.local/bowling",
    "analytics_resolution_code": "ROUTE_BOWLING"
  },
  "raw_response": "ROUTE_BOWLING"
}
```

#### `POST /v2/pub/conversation/{sessionId}/next`
Continues the conversation with user's answer.

**Request:**
```json
{
  "user_input": "5 people: ages 4, 9, 38, 40, 12"
}
```

**Response (next question or completion):**
Same shape as Start response, plus optional `prev_url`.

### Configuration

**appsettings.json:**
```json
{
  "DecisionEngine": {
    "ConfigPath": "Config/DecisionSpecs",
    "DefaultSpecId": "FAMILY_SATURDAY_V1",
    "ApiKey": "your-api-key"
  }
}
```

### DecisionSpec Schema

Key sections:
- `traits`: Questions to ask (key, question_text, answer_type, bounds, dependencies)
- `derived_traits`: Computed values (min_age, max_age, adult_count)
- `immediate_select_if`: Short-circuit rules (e.g., min_age < 5 ? Movie Night)
- `outcomes`: Possible recommendations with selection_rules
- `tie_strategy`: LLM clarifier for ambiguous cases
- `disambiguation`: Fallback trait order

### Flow

1. **Client calls `/start`**
 - Server creates session
   - Loads active DecisionSpec
   - Evaluates immediate rules
   - Returns first question or outcome

2. **Client POSTs answer to `next_url`**
   - Server parses input ? trait value
 - Validates (retry with rephrased question if invalid)
   - Re-evaluates rules
   - Returns next question or final outcome

3. **Repeats until `is_complete: true`**

### Error Handling

**Invalid input:**
```json
{
  "error": {
    "code": "INVALID_INPUT",
    "message": "Could not find valid ages. Please list as numbers (0-120)."
  },
  "question": { ...rephrased question... },
  "next_url": "..."
}
```

**Missing session:** 404  
**Invalid API key:** 401  
**Input too large (>2KB):** 413

### Derived Traits

Automatically computed from collected traits:
- `min_age`: `min(all_ages)`
- `max_age`: `max(all_ages)`
- `adult_count`: `count(all_ages >= 18)`

### Rules Engine

Simple expression evaluator supporting:
- Operators: `<`, `>`, `<=`, `>=`, `==`
- Examples:
  - `min_age < 5`
  - `group_size >= 4`
  - `adult_count == 2`

### Tie Resolution

When multiple outcomes satisfy rules:
1. Trigger LLM clarifier (asks preference question)
2. Map answer to pseudo-trait (e.g., `preference_activity_style`)
3. Re-evaluate with pseudo-trait
4. If still ambiguous after max attempts (2): fallback to first outcome

### Logging

Uses Serilog with:
- Console sink (development)
- Rolling file sink: `logs/decisionspark-{Date}.txt`
- Structured logging: session_id, trait_key, outcome_id, etc.

### Security

- API Key required in `X-API-KEY` header
- Configure in appsettings.json (use secrets in production)
- Input size limits (2KB)

## Getting Started

### Prerequisites
- .NET 9 SDK
- Visual Studio 2022 or VS Code

### Run Locally

```bash
cd DecisionSpark
dotnet run
```

API will be available at `https://localhost:5001` (or configured port).

### Test with curl

```bash
# Start session
curl -X POST https://localhost:5001/start \
  -H "Content-Type: application/json" \
  -H "X-API-KEY: dev-api-key-change-in-production" \
  -d "{}"

# Continue (use next_url from response)
curl -X POST https://localhost:5001/v2/pub/conversation/{sessionId}/next \
  -H "Content-Type: application/json" \
  -H "X-API-KEY: dev-api-key-change-in-production" \
  -d '{"user_input": "5 people"}'
```

## Extensibility

### Add New Spec

1. Create JSON file: `Config/DecisionSpecs/YOUR_SPEC_V1.1.0.0.active.json`
2. Define traits, rules, outcomes
3. Update `DefaultSpecId` in appsettings.json

### Swap OpenAI Integration

1. Implement `IQuestionGenerator` with Azure.AI.OpenAI
2. Register in Program.cs DI
3. Configure API key in appsettings or Azure Key Vault

### Use Redis Session Store

1. Implement `ISessionStore` with StackExchange.Redis
2. Register in Program.cs DI
3. Add connection string to configuration

## Future Enhancements

- [ ] OpenAI question phrasing (currently stub)
- [ ] LLM answer validation for complex traits
- [ ] LLM clarifier for tie resolution
- [ ] Multi-select option support
- [ ] Session expiration and cleanup
- [ ] Redis session persistence
- [ ] Analytics event publishing
- [ ] `/prev` endpoint for navigation back
- [ ] Admin API for spec management

## Project Structure

```
DecisionSpark/
??? Config/
?   ??? DecisionSpecs/          # JSON spec files
??? Controllers/
?   ??? StartController.cs      # POST /start
?   ??? ConversationController.cs # POST /conversation/{id}/next
??? Models/
?   ??? Api/    # Request/Response DTOs
?   ??? Runtime/        # Session, EvaluationResult
?   ??? Spec/     # DecisionSpec models
??? Services/
?   ??? ISessionStore.cs        # Session persistence
?   ??? IDecisionSpecLoader.cs  # Load & validate specs
?   ??? IRoutingEvaluator.cs    # Rule evaluation
?   ??? ITraitParser.cs         # Input parsing
?   ??? IQuestionGenerator.cs   # Question phrasing (stub)
?   ??? IResponseMapper.cs      # API response mapping
??? Program.cs      # DI, Serilog, startup
??? appsettings.json           # Configuration

```

## License

MIT

## Contributing

PRs welcome! Focus areas:
- OpenAI integration
- Enhanced rule expressions
- Additional trait parsers
- Test coverage

---

**DecisionSpark** - Smart decisions through minimal questions.
