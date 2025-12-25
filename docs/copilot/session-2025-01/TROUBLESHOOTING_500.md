# Troubleshooting 500 Internal Server Error

## Common Causes and Solutions

### 1. Invalid JSON Configuration

**Symptoms:**
- 500 error on `/start` endpoint
- `System.InvalidOperationException` in debug logs
- Application crashes on startup

**Solution:**
Check `appsettings.json` for syntax errors:

```json
{
  "Serilog": { ... },
  "DecisionEngine": { ... },
  "AllowedHosts": "*"    // ? Only one instance!
}
```

**Common JSON errors:**
- ? Duplicate keys (e.g., two `"AllowedHosts"` entries)
- ? Missing commas between properties
- ? Trailing commas on last property
- ? Unescaped quotes in string values

**How to validate:**
1. Open `appsettings.json` in VS Code or Visual Studio
2. Look for red squiggly underlines
3. Or use an online JSON validator

### 2. Missing Config Directory

**Symptoms:**
- 500 error on `/start`
- Log: "No active spec found for FAMILY_SATURDAY_V1"

**Solution:**
Ensure the spec file exists:
```
DecisionSpark/Config/DecisionSpecs/FAMILY_SATURDAY_V1.1.0.0.active.json
```

Create the directory if missing:
```bash
mkdir -p DecisionSpark/Config/DecisionSpecs
```

### 3. Spec JSON Invalid

**Symptoms:**
- 500 error on `/start`
- Log: "Failed to deserialize spec"

**Solution:**
Validate the DecisionSpec JSON file structure. Required fields:
- `spec_id`
- `version`
- `canonical_base_url`
- `traits` (array, at least one)
- `outcomes` (array, at least one)

### 4. Missing Dependencies

**Symptoms:**
- Application won't start
- DLL load errors in logs

**Solution:**
```bash
cd DecisionSpark
dotnet restore
dotnet build
```

### 5. Port Conflicts

**Symptoms:**
- Application starts but can't bind to port
- "Address already in use" error

**Solution:**
```powershell
# Find process using port 5001
netstat -ano | findstr :5001

# Kill the process (replace PID)
taskkill /PID <process_id> /F
```

Or change ports in `Properties/launchSettings.json`.

### 6. Null Reference in Controllers

**Symptoms:**
- 500 error on specific endpoint
- `NullReferenceException` in logs

**Solution:**
Check the error logs for the specific line. Common causes:
- Configuration value not found
- Service not registered in DI
- Session not found (use proper error handling)

## Debugging Steps

### Step 1: Check Application Logs

**Console output:**
```bash
dotnet run
```

Look for exceptions and stack traces.

**File logs:**
```
DecisionSpark/logs/decisionspark-{Date}.txt
```

### Step 2: Enable Detailed Logging

Add to `appsettings.json`:
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",  // ? Change from Information
      "Override": {
   "Microsoft": "Information",  // ? See more from framework
        "Microsoft.AspNetCore": "Information"
      }
    }
  }
}
```

### Step 3: Use Swagger UI

1. Start the app: `dotnet run`
2. Open `https://localhost:5001`
3. Click "Authorize" and enter API key
4. Try `/start` endpoint
5. Check the response body for error details

### Step 4: Test with curl

```bash
# Detailed error with verbose output
curl -v -X POST https://localhost:5001/start \
  -H "Content-Type: application/json" \
  -H "X-API-KEY: dev-api-key-change-in-production" \
-d "{}" \
  -k
```

### Step 5: Debug in Visual Studio

1. Set breakpoint in `StartController.cs` ? `Start` method
2. Press F5 to start debugging
3. Call the endpoint from Swagger or curl
4. Step through code to find the exception

## Quick Fixes Applied

### ? Fixed Invalid JSON in appsettings.json
**Before (BROKEN):**
```json
{
  "AllowedHosts": "*"    // ? Duplicate!
  "AllowedHosts": "*",
  "Serilog": { ... }
}
```

**After (FIXED):**
```json
{
  "Serilog": { ... },
  "DecisionEngine": { ... },
  "AllowedHosts": "*"
}
```

## Specific 500 Error Messages

### "Internal server error"
Generic catch-all. Check application logs for details.

### "No active spec found"
Spec file missing or wrong path. Verify:
- File exists at `Config/DecisionSpecs/FAMILY_SATURDAY_V1.1.0.0.active.json`
- `DecisionEngine:ConfigPath` in appsettings.json is correct
- `DecisionEngine:DefaultSpecId` matches the file name

### "Failed to deserialize spec"
Spec JSON is invalid. Common issues:
- Missing required fields
- Wrong data types (e.g., string instead of array)
- Invalid JSON syntax

### "Spec must have at least one trait"
DecisionSpec JSON has empty `traits` array.

### "Duplicate trait key"
Two traits have the same `key` value.

## Prevention

### ? Validate JSON Files
- Use editor with JSON schema validation
- Run `dotnet build` after changes
- Check logs before deploying

### ? Use Try-Catch Properly
Controllers already have exception handling:
```csharp
try { ... }
catch (Exception ex)
{
    _logger.LogError(ex, "Error in Start endpoint");
    return StatusCode(500, new { error = "Internal server error" });
}
```

### ? Test Locally First
Always run `dotnet run` and test in Swagger before committing.

### ? Check Logs Regularly
Monitor `logs/decisionspark-{Date}.txt` for warnings.

## Need More Help?

1. ? Check Visual Studio Error List (View ? Error List)
2. ? Check Output window (View ? Output ? Show output from: Build)
3. ? Check Debug console during F5 debugging
4. ? Review `DecisionSpark/logs/` directory
5. ? Enable verbose logging (see Step 2 above)

---

**After fixing appsettings.json, restart the application and try again!**
