# Running DecisionSpark API

## Quick Start

### Option 1: Run from Visual Studio
1. Open `DecisionSpark.sln` in Visual Studio
2. Press **F5** or click the "Play" button
3. Your browser will automatically open to `https://localhost:5001` (Swagger UI)

### Option 2: Run from Command Line

```bash
cd C:\GitHub\markhazleton\DecisionSpark\DecisionSpark
dotnet run
```

The application will start and display:
```
[timestamp INF] DecisionSpark API starting...
[timestamp INF] Swagger UI available at: https://localhost:5001
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5001
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

**Keep the terminal window open** while using the API.

### Option 3: Run with Watch (Auto-Reload)

For development with auto-reload on code changes:

```bash
cd C:\GitHub\markhazleton\DecisionSpark\DecisionSpark
dotnet watch run
```

## Accessing the API

Once running, open your browser to:

- **Primary (HTTPS)**: `https://localhost:5001`
- **Alternate (HTTP)**: `http://localhost:5000`

Both URLs will show the Swagger UI interface.

## Troubleshooting

### "Unable to connect" or "Connection refused"

**Solution 1: Check if the application is running**
- Look for the terminal window with "Now listening on..." messages
- If not running, use one of the run commands above

**Solution 2: Check firewall**
- Windows Firewall may block the first run
- Click "Allow access" when prompted

**Solution 3: Trust the development certificate**

```bash
dotnet dev-certs https --trust
```

Click "Yes" when prompted.

### "This site can't provide a secure connection" (ERR_SSL_PROTOCOL_ERROR)

**Solution: Use HTTP instead**
- Try `http://localhost:5000` instead of HTTPS
- Or run the HTTP profile:

```bash
dotnet run --launch-profile http
```

### Port already in use

If you see "Address already in use" error:

**Find the process:**
```powershell
netstat -ano | findstr :5001
netstat -ano | findstr :5000
```

**Kill the process:**
```powershell
taskkill /PID <process_id> /F
```

Or change the port in `Properties/launchSettings.json`.

### "No active spec found"

Ensure the spec file exists:
```
DecisionSpark/Config/DecisionSpecs/FAMILY_SATURDAY_V1.1.0.0.active.json
```

If missing, copy it from the project template.

## Stopping the Application

- **Command Line**: Press `Ctrl+C` in the terminal
- **Visual Studio**: Click the "Stop" button or press `Shift+F5`

## Environment

The application runs in **Development** mode by default, which:
- ? Enables Swagger UI
- ? Shows detailed error messages
- ? Enables developer exception page
- ? Logs to console and file

## Logs

Check logs at:
```
DecisionSpark/logs/decisionspark-{Date}.txt
```

Example: `decisionspark-20250128.txt`

## Testing the API

### Option 1: Use Swagger UI (Recommended)
1. Open `https://localhost:5001`
2. Click "Authorize"
3. Enter API key: `dev-api-key-change-in-production`
4. Test endpoints interactively

### Option 2: Use curl

```bash
# Start session
curl -X POST https://localhost:5001/start \
  -H "Content-Type: application/json" \
  -H "X-API-KEY: dev-api-key-change-in-production" \
  -d "{}" \
  -k

# Continue (replace {sessionId})
curl -X POST https://localhost:5001/v2/pub/conversation/{sessionId}/next \
  -H "Content-Type: application/json" \
  -H "X-API-KEY: dev-api-key-change-in-production" \
  -d '{"user_input": "6 people"}' \
  -k
```

## Configuration

### API Key
Change the API key in `appsettings.json`:

```json
{
  "DecisionEngine": {
    "ApiKey": "your-secure-api-key"
  }
}
```

### Ports
Change ports in `Properties/launchSettings.json`:

```json
{
  "profiles": {
    "https": {
      "applicationUrl": "https://localhost:5001;http://localhost:5000"
  }
  }
}
```

## URLs Summary

| Purpose | URL |
|---------|-----|
| Swagger UI (HTTPS) | `https://localhost:5001` |
| Swagger UI (HTTP) | `http://localhost:5000` |
| API Endpoints | `/start`, `/v2/pub/conversation/{id}/next` |
| Swagger JSON | `https://localhost:5001/swagger/v1/swagger.json` |

## Next Steps

1. ? Verify the app is running
2. ? Open `https://localhost:5001` in browser
3. ? Authorize with API key
4. ? Test POST /start endpoint
5. ? Follow the SWAGGER_GUIDE.md for complete flow

---

**If you still have connection issues, ensure:**
- The terminal shows "Now listening on: https://localhost:5001"
- No other application is using ports 5000 or 5001
- Your firewall allows the connection
- The development certificate is trusted
