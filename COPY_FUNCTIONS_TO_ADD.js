/* ============================================
   COPY THIS JAVASCRIPT CODE
   Add this at the end of your <script> section in Index.cshtml
   (Before the closing </script> tag)
   ============================================ */

// Copy individual debug entry to clipboard
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
        if (!btn) return;
    
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

// Copy all debug entries to clipboard
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
