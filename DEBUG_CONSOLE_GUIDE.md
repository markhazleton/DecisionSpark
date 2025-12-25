# Debug Console Feature - Documentation

## Overview
Added a comprehensive debug console to the Home page (`Index.cshtml`) that displays detailed information about all API requests and responses in real-time.

## Location
The debug console appears in the right sidebar below the API Info section.

## Features

### 1. **Visual Debug Panel**
- Dark theme console (VS Code style)
- Collapsible panel with eye icon toggle
- Auto-expands on errors
- Scrollable content area
- Timestamp for each entry

### 2. **Logged Information**

#### For Each Request:
- **URL**: Full endpoint URL being called
- **Method**: HTTP method (POST, GET)
- **Headers**: All request headers including API key
- **Body**: Complete request payload
- **Note**: Shows property naming (e.g., "user_input (snake_case)")

#### For Each Response:
- **Status**: HTTP status code (200, 400, 404, etc.)
- **Status Text**: HTTP status message
- **Headers**: All response headers
- **Body**: Complete response payload (JSON)

#### For Errors:
- **Error Message**: Descriptive error text
- **Stack Trace**: Full JavaScript stack trace
- **Context**: Where the error occurred

### 3. **Visual Indicators**

Each debug entry has color-coded icons:
- ?? **Blue** - Request sent
- ? **Green** - Successful response (200-299)
- ? **Red** - Error response (400-599)
- ?? **Cyan** - Data being transmitted
- ?? **Purple** - Scenario execution

### 4. **Controls**

Two buttons in the debug panel header:
- **Eye Icon**: Toggle visibility of debug panel
- **Trash Icon**: Clear all debug entries

## Usage

### Starting a Conversation:
1. Click "Start New Conversation"
2. Debug console logs:
   ```
   ?? Starting Conversation
   - URL, method, headers, body
   
   ? Start Response (200)
 - Full response with session ID, question, etc.
   ```

### Submitting an Answer:
1. Type answer and click "Send Answer"
2. Debug console logs:
   ```
   ?? Sending Answer
   - Shows user_input property (snake_case)
   - Full request details
   
   ? Answer Response (200)  OR  ? Answer Response (400)
   - Complete response body
   - Shows error details if status 400
   ```

### Viewing Error Details:
When an error occurs (like the 400 error you're seeing):
1. Panel automatically opens
2. Shows exact request that failed
3. Shows complete error response
4. Includes all HTTP headers
5. Timestamps help track sequence

## What to Look For

### Current Issue (400 Bad Request)
The debug console will show you:

1. **Request Body**:
   ```json
   {
     "user_input": "3"
   }
   ```

2. **Response Body** (if 400):
   ```json
   {
     "error": {
       "code": "INVALID_INPUT",
     "message": "Could not find a number in your response."
     },
     "question": { ... }
   }
   ```

3. **Check These**:
   - Is `user_input` being sent?
   - Is it empty or null?
   - What's the exact error message?
   - Are headers correct?

## Troubleshooting with Debug Console

### Problem: "Could not find a number"
**Look for in debug log**:
- Check request body: Is `user_input` present?
- Check request body: Does it contain the typed value?
- Check response: What's the exact error message?

### Problem: CORS Error
**Look for in debug log**:
- Network error before response
- Error stack trace mentioning CORS
- Missing response object

### Problem: 401 Unauthorized
**Look for in debug log**:
- Request headers section
- Verify `X-API-KEY` is present
- Verify API key value

### Problem: 404 Not Found
**Look for in debug log**:
- Request URL
- Check if path is correct
- Check if sessionId is valid

## Example Debug Output

```
?? Starting Conversation
{
  "url": "https://localhost:44356/start",
  "method": "POST",
  "headers": {
    "Content-Type": "application/json",
    "X-API-KEY": "dev-api-key-change-in-production"
  },
  "body": {}
}

? Start Response (200)
{
  "status": 200,
  "statusText": "OK",
  "body": {
    "texts": ["Thanks! One quick question."],
    "question": {
      "id": "group_size",
      "text": "How many people are going?",
      "allowFreeText": true
    },
    "nextUrl": "/conversation/abc123/next",
    "isComplete": false
  }
}

?? Sending Answer
{
  "url": "https://localhost:44356/conversation/abc123/next",
  "method": "POST",
  "headers": {
    "Content-Type": "application/json",
    "X-API-KEY": "dev-api-key-change-in-production"
  },
  "body": {
    "user_input": "3"
  },
  "note": "Property name: user_input (snake_case)"
}

? Answer Response (400)
{
  "status": 400,
  "statusText": "Bad Request",
  "body": {
    "error": {
      "code": "INVALID_INPUT",
      "message": "Could not find a number in your response."
    }
  }
}
```

## Next Steps

1. **Test After Restart**: Restart the application and try again
2. **Check Debug Log**: Look at the exact request/response
3. **Verify Request Body**: Make sure `user_input` contains "3"
4. **Check Response**: See exact error from server
5. **Compare**: See if server is receiving empty string vs "3"

## Tips

- **Keep Panel Open**: While testing, keep the debug panel visible
- **Clear Between Tests**: Click trash icon to clear old entries
- **Copy Data**: You can select and copy JSON from the debug panel
- **Sequential View**: Entries are timestamped to see the order of events
- **Auto-Scroll**: Panel automatically scrolls to newest entry

## Benefits

? **Real-time visibility** into API calls
? **Complete request/response** data
? **Error diagnosis** made easy
? **No browser dev tools needed**
? **User-friendly** formatted JSON
? **Color-coded** for quick scanning
? **Persistent log** until cleared
