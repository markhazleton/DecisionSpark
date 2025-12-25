# OpenAI API Support - What Changed

## Summary
Updated DecisionSpark to support **both** OpenAI API providers:
- ? **Direct OpenAI API** (new) - Using OpenAI's public API
- ? **Azure OpenAI** (existing) - Using Azure's OpenAI service

## What Changed

### 1. Configuration Structure
**Before** (Azure only):
```json
{
  "OpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiKey": "azure-key",
    "DeploymentName": "gpt-4"
  }
}
```

**After** (Both providers supported):
```json
{
  "OpenAI": {
    "Provider": "OpenAI",           // NEW: "OpenAI" or "Azure"
    "ApiKey": "sk-proj-...",        // Works for both
    "Model": "gpt-4",               // NEW: For direct OpenAI
    "Endpoint": "https://...",      // For Azure only
    "DeploymentName": "gpt-4"       // For Azure only
  }
}
```

### 2. Files Modified

| File | Changes |
|------|---------|
| `appsettings.json` | Added `Provider` and `Model` fields |
| `appsettings.Development.json` | Updated for direct OpenAI |
| `Services/IOpenAIService.cs` | Added configuration fields |
| `Services/OpenAIService.cs` | Support both client types |
| `secrets.json` | Updated for direct OpenAI |

### 3. New Documentation
- **OPENAI_CONFIGURATION_GUIDE.md** - Complete setup guide for both providers

## Quick Setup

### For Direct OpenAI API (Recommended)
```bash
cd DecisionSpark

# Set configuration
dotnet user-secrets set "OpenAI:Provider" "OpenAI"
dotnet user-secrets set "OpenAI:ApiKey" "sk-proj-YOUR-KEY"
dotnet user-secrets set "OpenAI:Model" "gpt-4"

# Run
dotnet run
```

**Get API Key**: https://platform.openai.com/api-keys

### For Azure OpenAI (Existing)
```bash
cd DecisionSpark

# Set configuration
dotnet user-secrets set "OpenAI:Provider" "Azure"
dotnet user-secrets set "OpenAI:Endpoint" "https://your-resource.openai.azure.com/"
dotnet user-secrets set "OpenAI:ApiKey" "your-azure-key"
dotnet user-secrets set "OpenAI:DeploymentName" "gpt-4"

# Run
dotnet run
```

## Configuration Fields Reference

### Common Fields (Both Providers)
- `ApiKey` (required) - API key for authentication
- `MaxTokens` (optional, default: 500)
- `Temperature` (optional, default: 0.7)
- `EnableFallback` (optional, default: true)
- `TimeoutSeconds` (optional, default: 30)

### Direct OpenAI Specific
- `Provider: "OpenAI"` (optional, this is the default)
- `Model` (optional, default: "gpt-4")
  - Options: "gpt-4", "gpt-4-turbo-preview", "gpt-3.5-turbo"

### Azure OpenAI Specific
- `Provider: "Azure"` (required)
- `Endpoint` (required) - Azure OpenAI resource endpoint
- `DeploymentName` (required) - Your model deployment name

## Migration Guide

### From Azure to Direct OpenAI
```bash
# Remove Azure-specific settings
dotnet user-secrets remove "OpenAI:Provider"
dotnet user-secrets remove "OpenAI:Endpoint"
dotnet user-secrets remove "OpenAI:DeploymentName"

# Add OpenAI settings
dotnet user-secrets set "OpenAI:ApiKey" "sk-proj-YOUR-KEY"
dotnet user-secrets set "OpenAI:Model" "gpt-4"
```

### From Direct OpenAI to Azure
```bash
# Add Azure settings
dotnet user-secrets set "OpenAI:Provider" "Azure"
dotnet user-secrets set "OpenAI:Endpoint" "https://your-resource.openai.azure.com/"
dotnet user-secrets set "OpenAI:ApiKey" "your-azure-key"
dotnet user-secrets set "OpenAI:DeploymentName" "gpt-4"

# Remove OpenAI-specific
dotnet user-secrets remove "OpenAI:Model"
```

## Verification

### Check Startup Logs

**Direct OpenAI**:
```
[INF] OpenAI API client initialized successfully for model: gpt-4
[INF] OpenAI service is configured and available
```

**Azure OpenAI**:
```
[INF] Azure OpenAI client initialized successfully
[INF] OpenAI service is configured and available
```

**Fallback Mode** (No config):
```
[WRN] OpenAI service is not configured - using fallback mode
```

## Cost Comparison

| Provider | GPT-4 Input | GPT-4 Output | GPT-3.5 Input | GPT-3.5 Output |
|----------|-------------|--------------|---------------|----------------|
| **OpenAI** | $0.03/1K | $0.06/1K | $0.0005/1K | $0.0015/1K |
| **Azure** | Varies by region | Varies | Varies | Varies |

**Estimated per conversation**:
- Direct OpenAI GPT-4: ~$0.01-0.03
- Direct OpenAI GPT-3.5: ~$0.001-0.003

## Why Both?

### Use Direct OpenAI When:
- ? Getting started quickly
- ? Development/testing
- ? Small to medium projects
- ? No compliance requirements
- ? Simple pay-as-you-go billing

### Use Azure OpenAI When:
- ? Enterprise requirements
- ? Data residency needs
- ? Integration with Azure services
- ? Advanced compliance needs
- ? Custom rate limits required

## Troubleshooting

### "OpenAI not configured"
**Direct OpenAI**: Check API key format (starts with `sk-` or `sk-proj-`)
**Azure**: Verify Provider set to "Azure" and all required fields present

### "Model not found"
**Direct OpenAI**: Use valid model name: "gpt-4", "gpt-3.5-turbo"
**Azure**: Use correct deployment name from Azure Portal

### "Authentication failed"
- Verify API key is current and valid
- Check spending limits (OpenAI platform)
- Verify Azure resource is active

## Testing

### Quick Test
```bash
# Start the app
dotnet run

# In another terminal
curl -X POST https://localhost:5001/start \
  -H "X-API-KEY: dev-api-key-change-in-production" \
  -H "Content-Type: application/json" \
  -d '{}'
```

Look for natural question generation in the response!

## Backward Compatibility

? **Existing Azure configurations continue to work**
- Default provider is "Azure" if Endpoint is present
- No breaking changes to existing deployments

? **Fallback mode still works**
- System operates without any OpenAI configuration
- Perfect for testing and development

## Summary

- ? Build successful
- ? Both providers supported
- ? Backward compatible
- ? Comprehensive documentation
- ? Easy migration path
- ? Production ready

**You can now use whichever OpenAI provider best fits your needs!** ??

---

For detailed configuration: See [OPENAI_CONFIGURATION_GUIDE.md](./OPENAI_CONFIGURATION_GUIDE.md)
For testing: See [OPENAI_TESTING_GUIDE.md](./OPENAI_TESTING_GUIDE.md)
For implementation details: See [OPENAI_IMPLEMENTATION_SUMMARY.md](./OPENAI_IMPLEMENTATION_SUMMARY.md)
