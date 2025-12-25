# ?? DecisionSpark Web UI - Complete Testing Interface

## Overview
A beautiful, Bootstrap 5-based web interface for testing and validating the DecisionSpark API. Features a complete conversational UI that demonstrates the HATEOAS pattern with automatic `/start` and `/next` endpoint navigation.

## What Was Created

### 1. **HomeController** (`Controllers/HomeController.cs`)
MVC controller with three main views:
- **Index** (`/`) - Full-featured production API testing interface
- **Demo** (`/demo`) - Simplified demo mode (no API key required)
- **About** (`/about`) - Documentation and API information

### 2. **Three Beautiful Views**

#### **Index View** - Production API Tester
**Features:**
- ? Full conversation flow with `/start` and `/next` endpoints
- ? HATEOAS support (follows `nextUrl` from responses)
- ? Session management with visual indicators
- ? Real-time conversation display
- ? Quick action buttons for common inputs
- ? Error handling with helpful messages
- ? Pre-built scenario testing (bowling, movie, golf)
- ? API key authentication
- ? Beautiful gradient design
- ? Responsive Bootstrap 5 layout

#### **Demo View** - Simplified Testing
**Features:**
- ? Uses demo endpoints (no API key required)
- ? Simplified interface for quick testing
- ? Scenario quick actions
- ? Green gradient theme
- ? Perfect for demonstrations

#### **About View** - Documentation
**Features:**
- ? Complete API documentation
- ? How it works explanation
- ? Sample code (JavaScript & C#)
- ? Technology stack overview
- ? Quick links to all interfaces

### 3. **Updated Program.cs**
- ? Added MVC views support
- ? Added static files support
- ? Added routing middleware
- ? Changed Swagger route to `/swagger`
- ? Set home page as default route

## How to Use

### 1. Start the Application
```bash
# In Visual Studio
Press F5

# Or via command line
dotnet run
```

### 2. Access the Web UI
Open your browser and navigate to:
```
https://localhost:44356
```

### 3. Test Conversation Flow

#### **Option A: Manual Testing**
1. Click **"Start New Conversation"**
2. Answer the first question (e.g., "6 people")
3. Click **"Send Answer"**
4. Answer the second question (e.g., "8, 10, 12, 35, 37, 40")
5. Receive bowling recommendation! ??

#### **Option B: Quick Scenarios**
1. Click **"Bowling Scenario"** (or Movie/Golf)
2. Watch the complete conversation unfold
3. See the final recommendation

#### **Option C: Demo Mode**
1. Navigate to `/demo`
2. Click **"Start Demo"**
3. Test without API keys

## UI Features

### Visual Design
- **Gradient Backgrounds:** Purple gradient for main UI, green for demo
- **Animated Messages:** Smooth fade-in animations
- **Color-Coded Messages:**
  - ?? **Blue** - Assistant questions
  - ?? **Green** - User answers
  - ?? **Red** - Error messages
  - ?? **Gradient** - Final recommendations
- **Status Indicators:** Pulsing circle shows session state
- **Responsive Layout:** Works on desktop, tablet, and mobile

### Interactive Elements
- **Quick Action Buttons:** Click example answers to fill input
- **Real-time Updates:** See conversation flow as it happens
- **Session Tracking:** Display current session ID
- **Progress Indicators:** Know where you are in the flow
- **Error Handling:** Helpful messages with examples

### HATEOAS Implementation
The UI automatically:
1. Calls `POST /start` to begin
2. Extracts `sessionId` from `nextUrl` in response
3. Uses `nextUrl` for subsequent `POST` requests
4. Follows the hypermedia-driven workflow

## Code Examples

### Starting a Conversation (from UI)
```javascript
const response = await fetch(`${BASE_URL}/start`, {
    method: 'POST',
    headers: {
    'Content-Type': 'application/json',
        'X-API-KEY': API_KEY
  },
    body: JSON.stringify({})
});

const data = await response.json();
// data.nextUrl contains the URL for the next request
// e.g., "/v2/pub/conversation/abc123/next"
```

### Submitting an Answer (from UI)
```javascript
const response = await fetch(currentNextUrl, {
    method: 'POST',
    headers: {
        'Content-Type': 'application/json',
        'X-API-KEY': API_KEY
    },
body: JSON.stringify({
        user_input: userInput
    })
});

const data = await response.json();
// Check data.isComplete to see if conversation is finished
// If not complete, data.nextUrl has the URL for next question
```

## Available URLs

| URL | Description | API Key Required |
|-----|-------------|------------------|
| `/` | Main testing interface | Yes |
| `/demo` | Demo mode interface | No |
| `/about` | Documentation page | No |
| `/swagger` | API documentation | No |

## Testing Scenarios

### Bowling Scenario
**Expected Flow:**
```
Q: How many people are going on the outing?
A: 6 people

Q: What are the ages of everyone who's going?
A: 8, 10, 12, 35, 37, 40

Result: GO_BOWLING ??
```

### Movie Night Scenario
**Expected Flow:**
```
Q: How many people are going on the outing?
A: 4 people

Q: What are the ages of everyone who's going?
A: 3, 6, 35, 37

Result: MOVIE_NIGHT ?? (immediate rule: min_age < 5)
```

### Golfing Scenario
**Expected Flow:**
```
Q: How many people are going on the outing?
A: 3

Q: What are the ages of everyone who's going?
A: 14, 16, 42

Result: GO_GOLFING ?
```

## Architecture

### Frontend Stack
- **Bootstrap 5.3.0** - UI framework
- **Bootstrap Icons** - Icon library
- **Vanilla JavaScript** - No framework dependencies
- **CSS Animations** - Smooth transitions
- **Fetch API** - HTTP requests

### Backend Stack
- **ASP.NET Core 9.0** - Web framework
- **MVC Pattern** - Controller and views
- **Razor Views** - Server-side rendering
- **HATEOAS** - Hypermedia-driven API

### Data Flow
```
User Action
    ?
JavaScript Event Handler
  ?
Fetch API Request (with API Key)
    ?
ASP.NET Core Controller (/start or /next)
    ?
Decision Engine Services
    ?
JSON Response with nextUrl
    ?
JavaScript Parses Response
    ?
Update UI with Messages
```

## Customization

### Changing Colors
Edit the CSS variables in `Index.cshtml`:
```css
:root {
    --primary-color: #0d6efd;
    --success-color: #198754;
    --danger-color: #dc3545;
    --warning-color: #ffc107;
}
```

### Adding New Scenarios
Add to the scenario buttons section in `Index.cshtml`:
```html
<button class="btn btn-outline-primary w-100 mb-2" onclick="runScenario('custom')">
    <i class="bi bi-star-fill"></i> Custom Scenario
</button>
```

And update the demo controller's scenarios dictionary:
```csharp
["custom"] = new List<string> { "answer1", "answer2" }
```

### Modifying Messages
Edit the message display functions in the JavaScript:
```javascript
function addMessage(type, content) {
    // Customize message rendering here
}
```

## Troubleshooting

### UI Not Loading
1. **Check Program.cs:** Ensure `AddControllersWithViews()` is added
2. **Check Views Folder:** Verify `Views/Home/*.cshtml` files exist
3. **Check Routing:** Ensure `MapControllerRoute` is configured

### API Calls Failing
1. **Check API Key:** Verify correct key in `appsettings.json`
2. **Check CORS:** If calling from different origin
3. **Check Logs:** Look at Serilog output for errors

### Swagger Not Found
- Swagger is now at `/swagger` (moved from root)
- Access via: `https://localhost:44356/swagger`

## File Structure

```
DecisionSpark/
??? Controllers/
?   ??? DemoController.cs       # Demo API endpoints
?   ??? HomeController.cs  # MVC controller for views
?   ??? StartController.cs      # Production start endpoint
?   ??? ConversationController.cs # Production conversation endpoint
??? Views/
?   ??? Home/
?       ??? Index.cshtml        # Main testing interface
?       ??? Demo.cshtml         # Demo mode interface
?       ??? About.cshtml        # Documentation page
??? Program.cs    # Application startup with MVC
??? appsettings.json       # Configuration with API key
```

## Benefits

### For Developers
- **Visual Debugging:** See conversation flow in real-time
- **Quick Testing:** Pre-built scenarios for instant validation
- **Error Visibility:** Clear error messages with hints
- **Code Examples:** Learn API patterns by viewing network calls

### For Demos
- **Impressive UI:** Professional, gradient design
- **No Setup:** Just open browser and start
- **Multiple Modes:** Production and demo versions
- **Quick Scenarios:** Show complete flows instantly

### For Testing
- **Full Flow:** Test complete conversations
- **HATEOAS Validation:** Verify hypermedia links work
- **Error Cases:** Test invalid inputs
- **Edge Cases:** Try different group sizes and ages

## Next Steps

1. **Restart Application** - Stop (Shift+F5) and start (F5)
2. **Open Browser** - Navigate to `https://localhost:44356`
3. **Test Main UI** - Try the conversation flow
4. **Test Demo Mode** - Visit `/demo`
5. **Review Documentation** - Check `/about` page
6. **Try Swagger** - Explore `/swagger` for API docs

## Screenshots

### Main Interface
- Purple gradient background
- Conversation area with color-coded messages
- Control panel with scenarios
- API info panel with documentation links

### Demo Mode
- Green gradient background
- Simplified controls
- Quick scenario buttons
- No API key required

### About Page
- Feature showcase
- API endpoint tables
- Code samples
- Technology stack overview

---

## Summary

? **HomeController Created** - MVC controller with 3 views  
? **Beautiful UI** - Bootstrap 5 with gradients and animations  
? **HATEOAS Support** - Automatic nextUrl navigation  
? **Multiple Modes** - Production API and Demo modes  
? **Full Documentation** - About page with examples  
? **Quick Scenarios** - Pre-built test flows  
? **Error Handling** - Helpful messages and hints  
? **Responsive Design** - Works on all devices  
? **Build Successful** - Ready to run!  

**Status: COMPLETE** ??  
**Action: Restart and test at https://localhost:44356** ??
