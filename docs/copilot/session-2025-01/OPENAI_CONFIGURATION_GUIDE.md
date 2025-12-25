# OpenAI Configuration Guide

## Overview
DecisionSpark supports two OpenAI providers:
1. **Direct OpenAI API** - Use OpenAI's public API directly
2. **Azure OpenAI** - Use Azure's OpenAI service

## Configuration Options

### Option 1: Direct OpenAI API (Recommended for Getting Started)

#### Configuration
```json
{
  "OpenAI": {
    "Provider": "OpenAI",
    "ApiKey": "sk-proj-your-actual-openai-key",
    "Model": "gpt-4",
    "MaxTokens": 500,
    "Temperature": 0.7,
    "EnableFallback": true,
    "TimeoutSeconds": 30
  }
}
```

#### Setup Steps
1. **Get API Key from OpenAI**:
   - Go to https://platform.openai.com/api-keys
   - Create a new API key
   - Copy the key (starts with `sk-proj-` or `sk-`)

2. **Configure User Secrets** (Recommended):
```bash
cd DecisionSpark
dotnet user-secrets set "OpenAI:Provider" "OpenAI"
dotnet user-secrets set "OpenAI:ApiKey" "sk-proj-your-actual-key"
dotnet user-secrets set "OpenAI:Model" "gpt-4"
```

3. **Available Models**:
   - `gpt-4` - Most capable (recommended)
   - `gpt-4-turbo-preview` - Faster, cheaper
   - `gpt-3.5-turbo` - Fast and economical

#### Pricing (as of 2024)
- **GPT-4**: ~$0.03 per 1K input tokens, $0.06 per 1K output tokens
- **GPT-3.5-Turbo**: ~$0.0005 per 1K input tokens, $0.0015 per 1K output tokens
- **Estimated cost per conversation**: $0.01-0.03 with GPT-4, $0.001-0.003 with GPT-3.5

### Option 2: Azure OpenAI

#### Configuration
```json
{
  "OpenAI": {
    "Provider": "Azure",
    "Endpoint": "https://your-resource.openai.azure.com/",
    "ApiKey": "your-azure-openai-key",
    "DeploymentName": "gpt-4",
    "MaxTokens": 500,
    "Temperature": 0.7,
    "EnableFallback": true,
    "TimeoutSeconds": 30
  }
}
```

#### Setup Steps
1. **Create Azure OpenAI Resource**:
   - Go to Azure Portal
   - Create new Azure OpenAI resource
   - Deploy a model (e.g., gpt-4)

2. **Get Configuration**:
   - Endpoint: From resource overview
   - API Key: From "Keys and Endpoint" section
   - Deployment Name: Name you gave when deploying model

3. **Configure User Secrets**:
```bash
cd DecisionSpark
dotnet user-secrets set "OpenAI:Provider" "Azure"
dotnet user-secrets set "OpenAI:Endpoint" "https://your-resource.openai.azure.com/"
dotnet user-secrets set "OpenAI:ApiKey" "your-azure-key"
dotnet user-secrets set "OpenAI:DeploymentName" "gpt-4"
```

## Configuration Reference

### Required Fields

#### For Direct OpenAI
| Field | Required | Description | Example |
|-------|----------|-------------|---------|
| `Provider` | No* | Provider type | `"OpenAI"` (default) |
| `ApiKey` | Yes | OpenAI API key | `"sk-proj-..."` |
| `Model` | No | Model to use | `"gpt-4"` (default) |

*Provider defaults to "OpenAI" if not specified

#### For Azure OpenAI
| Field | Required | Description | Example |
|-------|----------|-------------|---------|
| `Provider` | Yes | Must be "Azure" | `"Azure"` |
| `Endpoint` | Yes | Azure endpoint URL | `"https://your.openai.azure.com/"` |
| `ApiKey` | Yes | Azure API key | `"abc123..."` |
| `DeploymentName` | Yes | Deployment name | `"gpt-4"` |

### Optional Fields (Both Providers)

| Field | Default | Description |
|-------|---------|-------------|
| `MaxTokens` | 500 | Maximum tokens per response |
| `Temperature` | 0.7 | Creativity (0.0-2.0) |
| `EnableFallback` | true | Use fallback when OpenAI unavailable |
| `TimeoutSeconds` | 30 | Request timeout |

## Quick Start

### 1. Test Without OpenAI (Fallback Mode)
```bash
cd DecisionSpark
dotnet run
```
System works perfectly without any OpenAI configuration!

### 2. Setup Direct OpenAI API (Easiest)
```bash
# Get your key from https://platform.openai.com/api-keys
dotnet user-secrets set "OpenAI:ApiKey" "sk-proj-YOUR-ACTUAL-KEY"
dotnet user-secrets set "OpenAI:Model" "gpt-4"
dotnet run
```

### 3. Setup Azure OpenAI (Enterprise)
```bash
dotnet user-secrets set "OpenAI:Provider" "Azure"
dotnet user-secrets set "OpenAI:Endpoint" "https://YOUR-RESOURCE.openai.azure.com/"
dotnet user-secrets set "OpenAI:ApiKey" "YOUR-AZURE-KEY"
dotnet user-secrets set "OpenAI:DeploymentName" "gpt-4"
dotnet run
```

## Comparison: OpenAI vs Azure OpenAI

| Feature | Direct OpenAI | Azure OpenAI |
|---------|---------------|--------------|
| **Setup Complexity** | ? Easy | ??? Complex |
| **Cost** | Pay-as-you-go | Varies by region |
| **Rate Limits** | Tier-based | Configurable |
| **Data Residency** | US-based | Choose Azure region |
| **Enterprise Features** | Limited | Full Azure integration |
| **Best For** | Development, startups | Enterprise, compliance needs |

## Validation & Troubleshooting

### Check Configuration Status
When you run the app, check console output:

**? Direct OpenAI Configured**:
```
[INF] OpenAI API client initialized successfully for model: gpt-4
[INF] OpenAI service is configured and available
```

**? Azure OpenAI Configured**:
```
[INF] Azure OpenAI client initialized successfully
[INF] OpenAI service is configured and available
```

**?? Fallback Mode** (No OpenAI):
```
[WRN] OpenAI configuration is incomplete. Service will use fallback mode.
[WRN] OpenAI service is not configured - using fallback mode
```

### Common Issues

#### Issue: "OpenAI not configured"
**Cause**: Missing or invalid API key

**Solution for Direct OpenAI**:
```bash
dotnet user-secrets set "OpenAI:ApiKey" "sk-proj-your-key"
```

**Solution for Azure**:
```bash
dotnet user-secrets set "OpenAI:Provider" "Azure"
dotnet user-secrets set "OpenAI:Endpoint" "https://your-resource.openai.azure.com/"
dotnet user-secrets set "OpenAI:ApiKey" "your-azure-key"
dotnet user-secrets set "OpenAI:DeploymentName" "gpt-4"
```

#### Issue: "Invalid API key"
- **Direct OpenAI**: Check key starts with `sk-` or `sk-proj-`
- **Azure**: Verify key from Azure Portal "Keys and Endpoint"

#### Issue: "Model not found" (Direct OpenAI)
**Solution**: Use valid model name
```bash
dotnet user-secrets set "OpenAI:Model" "gpt-3.5-turbo"
```

#### Issue: "Deployment not found" (Azure)
**Solution**: Verify deployment name matches Azure Portal
```bash
dotnet user-secrets set "OpenAI:DeploymentName" "your-deployment-name"
```

## Security Best Practices

### ? DO
- Store API keys in User Secrets for development
- Use Azure Key Vault for production
- Rotate keys regularly
- Monitor usage and costs
- Set spending limits (OpenAI platform)

### ? DON'T
- Commit API keys to source control
- Share keys in documentation
- Use production keys in development
- Leave keys in appsettings.json

## Cost Management

### Direct OpenAI
1. **Set Usage Limits**:
   - Go to https://platform.openai.com/account/billing/limits
   - Set monthly spending cap

2. **Monitor Usage**:
   - Dashboard: https://platform.openai.com/usage
   - Track tokens per request in logs

3. **Optimize Costs**:
   - Use GPT-3.5-Turbo for simple tasks
   - Reduce MaxTokens (default: 500)
   - Lower Temperature for parsing (0.3 vs 0.7)

### Azure OpenAI
1. **Monitor in Azure Portal**:
   - Cost Management + Billing
   - Set budget alerts

2. **Optimize**:
   - Choose appropriate region (pricing varies)
   - Configure quotas
   - Use Azure Advisor recommendations

## Testing Your Configuration

### Quick Test
```bash
# Start the app
cd DecisionSpark
dotnet run

# In another terminal, test the API
curl -X POST https://localhost:5001/start \
  -H "X-API-KEY: dev-api-key-change-in-production" \
  -H "Content-Type: application/json" \
  -d '{}'
```

### Verify OpenAI Integration
Check logs for:
```
[INF] Requesting OpenAI completion. OpenAI model: gpt-4, MaxTokens: 150
[INF] OpenAI completion received. Length: 87
[INF] Generated question for group_size: [Natural question here]
```

## Migration Between Providers

### From Azure to Direct OpenAI
```bash
# Remove Azure settings
dotnet user-secrets remove "OpenAI:Provider"
dotnet user-secrets remove "OpenAI:Endpoint"
dotnet user-secrets remove "OpenAI:DeploymentName"

# Add OpenAI settings
dotnet user-secrets set "OpenAI:ApiKey" "sk-proj-your-key"
dotnet user-secrets set "OpenAI:Model" "gpt-4"
```

### From Direct OpenAI to Azure
```bash
# Add Azure settings
dotnet user-secrets set "OpenAI:Provider" "Azure"
dotnet user-secrets set "OpenAI:Endpoint" "https://your-resource.openai.azure.com/"
dotnet user-secrets set "OpenAI:DeploymentName" "gpt-4"
# Keep the same ApiKey field but use Azure key
dotnet user-secrets set "OpenAI:ApiKey" "your-azure-key"

# Remove OpenAI-specific setting
dotnet user-secrets remove "OpenAI:Model"
```

## Summary

- ? **For Development**: Use Direct OpenAI API (easier setup)
- ? **For Production/Enterprise**: Consider Azure OpenAI (better compliance)
- ? **Not Sure?**: Start without OpenAI (fallback mode works perfectly)
- ? **Switch Anytime**: Configuration is flexible

The system gracefully handles all scenarios! ??
