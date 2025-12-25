# DecisionSpark 500 Error - FIXED ?

## Problem
The `/start` endpoint was returning 500 Internal Server Error with `InvalidOperationException` being thrown repeatedly.

## Root Cause
**The Config/DecisionSpecs JSON files were NOT being copied to the build output directory.** 

When the application tried to load `FAMILY_SATURDAY_V1.1.0.0.active.json`, it couldn't find it because:
- Source location: `DecisionSpark/Config/DecisionSpecs/FAMILY_SATURDAY_V1.1.0.0.active.json` ?
- Expected at runtime: `bin/Debug/net9.0/Config/DecisionSpecs/FAMILY_SATURDAY_V1.1.0.0.active.json` ?

## Solution
Updated `DecisionSpark.csproj` to copy Config files to output directory:

```xml
<ItemGroup>
  <None Update="Config\**\*.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

## Changes Made
1. ? Modified `DecisionSpark/DecisionSpark.csproj`
2. ? Rebuilt the project successfully
3. ? Verified files are now copied to `bin/Debug/net9.0/Config/DecisionSpecs/`

## Next Steps - RESTART REQUIRED

**You MUST restart the application for this fix to take effect:**

### Option 1: Visual Studio
1. Press `Shift+F5` to stop debugging
2. Press `F5` to start debugging again

### Option 2: Terminal
1. Press `Ctrl+C` to stop the application
2. Run `dotnet run` to start again

## Testing
After restarting, test the `/start` endpoint:

```http
POST https://localhost:44356/start
Content-Type: application/json
X-API-KEY: dev-api-key-change-in-production

{}
```

**Expected Result:** 200 OK with first question about group size

## Why This Happened
The .NET SDK automatically includes content files, but doesn't copy them to the output directory by default. We needed to explicitly tell the build system to copy the Config files using the `<None Update>` directive with `<CopyToOutputDirectory>`.

---

**Status: FIXED - Restart required to apply changes** ??
