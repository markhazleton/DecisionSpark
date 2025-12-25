# ? SOLUTION: 500 Internal Server Error on /start

## Root Cause Identified
The JSON spec file uses **snake_case** property naming (e.g., `spec_id`, `canonical_base_url`), but the C# models use **PascalCase** (e.g., `SpecId`, `CanonicalBaseUrl`).

**Why `PropertyNameCaseInsensitive` wasn't enough:**
- This option only handles case differences (e.g., `specid` vs `SpecId`)
- It does NOT handle snake_case vs PascalCase naming convention differences
- The deserializer couldn't map `spec_id` ? `SpecId` without the proper naming policy

## Complete Fix Applied

### 1. Project Configuration (.csproj) ?
Added Config file copying:
```xml
<ItemGroup>
  <None Update="Config\**\*.json">
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

### 2. Path Resolution (IDecisionSpecLoader.cs) ?
Added multi-path checking for IIS Express compatibility:
```csharp
var candidatePaths = new List<string>();
candidatePaths.Add(Path.Combine(environment.ContentRootPath, configPath));
candidatePaths.Add(Path.Combine(AppContext.BaseDirectory, configPath));
_configBasePath = candidatePaths.FirstOrDefault(Directory.Exists) ?? candidatePaths[0];
```

### 3. JSON Deserialization (IDecisionSpecLoader.cs) ?  **? THE KEY FIX**
Added snake_case naming policy:
```csharp
var options = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower  // ? THIS WAS MISSING!
};

var spec = JsonSerializer.Deserialize<DecisionSpec>(json, options);
```

## Testing Steps

### 1. Stop and Restart
**CRITICAL:** You MUST fully restart the application:
```
Press Shift+F5 (Stop)
Press F5 (Start)
```

### 2. Test the /start Endpoint
Run Test 1 from `DecisionSpark.http`:
```http
POST https://localhost:44356/start
Content-Type: application/json
X-API-KEY: dev-api-key-change-in-production

{}
```

### 3. Expected Success Response
```json
{
  "isComplete": false,
  "texts": ["Thanks! One quick question."],
  "question": {
    "id": "group_size",
    "source": "FAMILY_SATURDAY_V1",
    "text": "How many people are going on the outing?",
    "allowFreeText": true,
    "isFreeText": true,
    "type": "text"
  },
  "nextUrl": "https://api.example.com/v2/pub/conversation/{sessionId}/next",
  "sessionId": "{12-char-id}"
}
```

## What Was Wrong

| Component | Issue | Fix |
|-----------|-------|-----|
| **Config Files** | Not copied to bin output | Added `<None Update>` to .csproj |
| **Path Resolution** | Only checked ContentRootPath | Check both ContentRootPath and BaseDirectory |
| **JSON Naming** | snake_case ? PascalCase mismatch | Added `SnakeCaseLower` naming policy |

## Why It Failed Before

1. **Silent deserialization failure:** JSON deserializer returned an object, but all properties were default values
2. **Validation threw exception:** `SpecId` was empty string, causing `InvalidOperationException`
3. **Generic error handling:** Controller caught exception and returned generic 500 error

## Verification Checklist

- [x] Config files copied to bin directory
- [x] Multi-path resolution implemented
- [x] Snake_case JSON naming policy added
- [x] Build successful
- [ ] **Application restarted** ? YOU NEED TO DO THIS
- [ ] **/start endpoint tested** ? THEN TEST THIS

## Files Modified
1. `DecisionSpark/DecisionSpark.csproj` - Config file copying
2. `DecisionSpark/Services/IDecisionSpecLoader.cs` - Path resolution + JSON naming policy

---

## Quick Test Commands

### Verify Config Files Exist
```powershell
# Source
Test-Path "DecisionSpark\Config\DecisionSpecs\FAMILY_SATURDAY_V1.1.0.0.active.json"

# Build output
Test-Path "DecisionSpark\bin\Debug\net9.0\Config\DecisionSpecs\FAMILY_SATURDAY_V1.1.0.0.active.json"
```

### Check JSON Property Names
```powershell
Get-Content "DecisionSpark\Config\DecisionSpecs\FAMILY_SATURDAY_V1.1.0.0.active.json" | Select-String '"spec_id"|"canonical_base_url"'
```

---

**STATUS: COMPLETE FIX APPLIED** ?  
**ACTION REQUIRED: RESTART APPLICATION AND TEST** ??
