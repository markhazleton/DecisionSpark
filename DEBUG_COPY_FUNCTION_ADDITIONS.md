# Copy Debug Entry Functions - Add to Index.cshtml

Add these JavaScript functions after the `updateDebugDisplay()` function in your Index.cshtml file:

```javascript
// Copy debug entry to clipboard
function copyDebugEntry(index) {
    const entry = debugLog[index];
    const textToCopy = `=== ${entry.title} ===
Timestamp: ${entry.timestamp}
Type: ${entry.type}

${JSON.stringify(entry.data, null, 2)}
`;
    
    navigator.clipboard.writeText(textToCopy).then(() => {
    // Show visual feedback
        const btn = event.target.closest('button');
        const originalHtml = btn.innerHTML;
        btn.innerHTML = '<i class="bi bi-check"></i>';
  btn.classList.add('btn-success');
        btn.classList.remove('btn-outline-light');
        
        setTimeout(() => {
      btn.innerHTML = originalHtml;
            btn.classList.remove('btn-success');
            btn.classList.add('btn-outline-light');
        }, 1500);
    }).catch(err => {
    console.error('Failed to copy:', err);
        alert('Failed to copy to clipboard');
    });
}

// Copy all debug entries
function copyAllDebug() {
    if (debugLog.length === 0) {
        alert('No debug entries to copy');
    return;
 }

    let allText = '=== DecisionSpark Debug Log ===\n';
    allText += `Generated: ${new Date().toLocaleString()}\n`;
    allText += `Total Entries: ${debugLog.length}\n\n`;
    
    debugLog.forEach((entry, index) => {
        allText += `\n${'='.repeat(60)}\n`;
  allText += `Entry ${index + 1}: ${entry.title}\n`;
        allText += `Timestamp: ${entry.timestamp}\n`;
   allText += `Type: ${entry.type}\n`;
    allText += `${'='.repeat(60)}\n`;
        allText += JSON.stringify(entry.data, null, 2);
        allText += '\n';
    });

    navigator.clipboard.writeText(allText).then(() => {
        alert(`Copied all ${debugLog.length} debug entries to clipboard!`);
    }).catch(err => {
   console.error('Failed to copy:', err);
        alert('Failed to copy to clipboard');
});
}
```

## Update the `updateDebugDisplay()` function:

Replace the forEach loop in `updateDebugDisplay()` with:

```javascript
let html = '';
debugLog.forEach((entry, index) => {
    const colorClass = entry.type === 'error' ? 'debug-error' : 
        entry.type === 'warning' ? 'debug-warning' : 'debug-success';
  
    html += `
  <div class="debug-header ${colorClass}">
            <div class="d-flex justify-content-between align-items-center">
                <span>
  <i class="bi bi-${entry.type === 'error' ? 'x-circle' : entry.type === 'warning' ? 'exclamation-triangle' : 'check-circle'}"></i>
      ${entry.title}
          </span>
      <div>
 <span class="debug-timestamp me-2">${entry.timestamp}</span>
          <button class="btn btn-sm btn-outline-light copy-btn" onclick="copyDebugEntry(${index})" title="Copy to clipboard">
  <i class="bi bi-clipboard"></i>
                 </button>
            </div>
            </div>
        </div>
  <pre>${escapeHtml(JSON.stringify(entry.data, null, 2))}</pre>
        ${index < debugLog.length - 1 ? '<hr style="border-color: #444; margin: 15px 0;">' : ''}
    `;
});
```

## Add "Copy All" button to the debug header:

Update the debug panel header section:

```html
<!-- Debug Panel -->
<div class="card mt-3">
    <div class="card-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0"><i class="bi bi-bug-fill"></i> Debug Console</h5>
 <div>
            <button class="btn btn-sm btn-outline-light" onclick="copyAllDebug()" title="Copy all debug entries">
           <i class="bi bi-files"></i>
            </button>
            <button class="btn btn-sm btn-outline-light" onclick="toggleDebug()">
            <i class="bi bi-eye" id="debugToggleIcon"></i>
     </button>
  <button class="btn btn-sm btn-outline-light" onclick="clearDebug()">
          <i class="bi bi-trash"></i>
            </button>
        </div>
    </div>
    <!-- rest of debug panel -->
</div>
```

## Result:

Each debug entry will now have a clipboard icon button that:
- ? Copies that specific request/response pair
- ? Includes timestamp and type
- ? Shows checkmark feedback when copied
- ? Properly formatted for sharing

Plus a "Copy All" button that copies the entire debug log in a formatted way.

## Usage Example:

When you copy a debug entry for "?? Sending Answer", you'll get:

```
=== ?? Sending Answer ===
Timestamp: 8:42:31 AM
Type: success

{
  "url": "https://localhost:44356/conversation/8de3fca40ab3/next",
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
```

This makes it easy to copy and share debug information for troubleshooting!
