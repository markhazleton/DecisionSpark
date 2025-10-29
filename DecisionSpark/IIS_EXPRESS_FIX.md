# IIS Express 500 Error - FINAL FIX ?

## The Real Problem
When running under IIS Express, `ContentRootPath` points to the **source directory** (`C:\GitHub\...\DecisionSpark\`), but we also copied files to the **bin output directory** (`bin\Debug\net9.0\`). The code was only checking one location.

## Complete Solution

### 1. Updated `.csproj` to Copy Config Files
```xml
<ItemGroup>
  <None Update="Config\**\*.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

### 2. Updated `FileSystemDecisionSpecLoader` to Check Multiple Paths
The loader now checks multiple candidate paths in order:
1. **ContentRootPath + Config/DecisionSpecs** (source directory - where IIS Express serves from)
2. **AppContext.BaseDirectory + Config/DecisionSpecs** (bin output directory)

It uses the **first path that exists**, making it work in any hosting scenario.

## Verification

### Check Files Exist in Source
```powershell
Test-Path "DecisionSpark\Config\DecisionSpecs\FAMILY_SATURDAY_V1.1.0.0.active.json"
# Should return: True
```

### Check Files Copied to Bin
```powershell
Test-Path "DecisionSpark\bin\Debug\net9.0\Config\DecisionSpecs\FAMILY_SATURDAY_V1.1.0.0.active.json"
# Should return: True
```

## Testing Steps

### 1. Stop and Restart Application
**IMPORTANT:** Hot reload won't apply these changes. You MUST restart:
- Press `Shift+F5` to stop
- Press `F5` to start again

### 2. Check Startup Logs
Look for this log message showing which path was found:
```
[INF] DecisionSpec base path: {Path} (Exists: True)
```

### 3. Test the /start Endpoint

**In Visual Studio:**
1. Click "Send Request" in the `DecisionSpark.http` file (Test 1)
2. Should get **200 OK** with the first question

**Expected Response:**
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
  "nextUrl": "https://api.example.com/v2/pub/conversation/{sessionId}/next"
}
```

## Why This Approach Works

### Different Hosting Scenarios
| Scenario | ContentRootPath | AppContext.BaseDirectory | Files Located |
|----------|----------------|-------------------------|---------------|
| IIS Express | `C:\...\DecisionSpark\` | `C:\...\DecisionSpark\bin\Debug\net9.0\` | Source OR Bin |
| Kestrel (dotnet run) | `C:\...\DecisionSpark\` | `C:\...\DecisionSpark\bin\Debug\net9.0\` | Source OR Bin |
| IIS | `C:\inetpub\wwwroot\DecisionSpark\` | `C:\inetpub\wwwroot\DecisionSpark\` | Bin (only) |
| Docker | `/app` | `/app` | Bin (only) |

The code now handles **all** of these scenarios automatically.

## Files Changed
1. ? **DecisionSpark.csproj** - Added copy directive
2. ? **Services/IDecisionSpecLoader.cs** - Multi-path resolution logic
3. ? **Build verified** - Config files present in both locations

## Debugging Tips

### If Still Getting 500
1. **Check the Debug output** for the actual path being used
2. **Enable break-on-exception** for `InvalidOperationException`
3. **Look at the exception message** - it will show which path failed

### To See What Paths Are Checked
The startup logs now show:
```
[INF] DecisionSpec base path: {ResolvedPath} (Exists: {True/False})
```

If `Exists: False`, it also logs:
```
[WRN] Config directory not found. Searched paths: {Path1}, {Path2}
```

---

**Status: FINAL FIX APPLIED - Restart Required** ??

**Action Required:**
1. Stop debugging (`Shift+F5`)
2. Start debugging (`F5`)
3. Test `/start` endpoint
