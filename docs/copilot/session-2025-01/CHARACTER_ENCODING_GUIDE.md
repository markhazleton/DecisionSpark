# Character Encoding Best Practices

## Quick Reference

### Recommended Settings

| File Type | Encoding | BOM |
|-----------|----------|-----|
| `.json` | UTF-8 | With BOM preferred |
| `.cs` | UTF-8 | With BOM |
| `.cshtml` | UTF-8 | With BOM |
| `.md` | UTF-8 | No BOM |
| `.ps1` | UTF-8 | With BOM |

## Visual Studio Settings

### Check Current Encoding
1. File ? Advanced Save Options
2. View current: "Encoding: UTF-8 with signature"

### Change Encoding
1. File ? Advanced Save Options
2. Select: **Unicode (UTF-8 with signature) - Codepage 65001**
3. Click OK
4. Save file

## VS Code Settings

### Default Encoding
```json
{
  "files.encoding": "utf8",
  "files.autoGuessEncoding": false
}
```

### Change File Encoding
1. Click encoding in status bar (bottom right)
2. Select "Save with Encoding"
3. Choose "UTF-8"

## Common Character Issues

### Issue 1: Emojis Show as `??`

**Problem**: File saved as ANSI/ASCII instead of UTF-8

**Fix**:
1. Open file in Visual Studio
2. File ? Advanced Save Options
3. Select UTF-8 with signature
4. Save

### Issue 2: Special Characters as `?`

**Problem**: Invalid UTF-8 byte sequence

**Fix**: Replace with correct Unicode character:
- `?` ? Check what character it should be
- Common: `—` (em-dash), `'` (apostrophe), `"` (quote)

### Issue 3: Double-Encoded Characters

**Problem**: UTF-8 text encoded twice (e.g., `â€"` for em-dash)

**Examples**:
- `â€"` ? `—` (em-dash)
- `â€˜` ? `'` (left single quote)
- `â€™` ? `'` (right single quote)
- `â€œ` ? `"` (left double quote)
- `â€` ? `"` (right double quote)

**Fix**: Replace with single character

## Characters to Watch

### Typography

| Character | HTML | Unicode | Description |
|-----------|------|---------|-------------|
| — | `&mdash;` | U+2014 | Em-dash |
| – | `&ndash;` | U+2013 | En-dash |
| ' | `&lsquo;` | U+2018 | Left single quote |
| ' | `&rsquo;` | U+2019 | Right single quote |
| " | `&ldquo;` | U+201C | Left double quote |
| " | `&rdquo;` | U+201D | Right double quote |
| … | `&hellip;` | U+2026 | Ellipsis |

### Emojis (Common in UI)

| Emoji | Unicode | Use Case |
|-------|---------|----------|
| ?? | U+1F3B3 | Bowling |
| ?? | U+1F3AC | Movies |
| ? | U+26F3 | Golf |
| ? | U+2705 | Success |
| ? | U+274C | Error |
| ?? | U+1F680 | Launch |
| ?? | U+1F504 | Restart |
| ?? | U+1F3AF | Target |

## Testing Encoding

### Quick Test String

Add this to your file and verify it displays correctly:

```
Test: ??????? — "quotes" 'apostrophes'
```

If any show as `??` or `?`, encoding is wrong.

### PowerShell Test

```powershell
# Read file as UTF-8
$content = Get-Content "path\to\file.json" -Encoding UTF8
$content | Select-String "??"
```

Should find the emoji if encoding is correct.

## Git Settings

### .gitattributes

Add to repository root:

```
# Auto-detect text files and normalize line endings
* text=auto

# Explicitly declare text files
*.cs text
*.json text
*.md text
*.cshtml text

# Denote binary files
*.png binary
*.jpg binary
*.dll binary
```

### Git Configuration

```bash
# Set core autocrlf
git config --global core.autocrlf true  # Windows
git config --global core.autocrlf input # Mac/Linux
```

## JSON Files Specifically

### Do's ?
```json
{
  "title": "Bowling night ??",
  "message": "It's perfect — easy and fun"
}
```

### Don'ts ?
```json
{
  "title": "Bowling night ??",  // Wrong encoding
  "message": "It's perfect ? easy"  // Invalid character
}
```

### Escape Sequences (Alternative)

If encoding issues persist, use Unicode escapes:

```json
{
  "title": "Bowling night \uD83C\uDFB3",
  "message": "It's perfect \u2014 easy"
}
```

Works but less readable.

## Markdown Files

### Headers with Emojis
```markdown
# ?? Project Goals
## ? Completed
## ?? Next Steps
```

### Lists with Emojis
```markdown
- ? Build successful
- ? Tests failing
- ?? Restart required
```

### Code Blocks
Use triple backticks with language:
````markdown
```json
{
  "emoji": "??"
}
```
````

## C# String Literals

### Regular Strings
```csharp
var title = "Bowling night ??";
```

### Verbatim Strings
```csharp
var message = @"Bowling is perfect — easy and fun";
```

### Unicode Escapes
```csharp
var emoji = "\uD83C\uDFB3";  // ??
var dash = "\u2014";  // —
```

## Troubleshooting

### Problem: File Won't Save with UTF-8

**Solution**: 
1. Copy content to clipboard
2. Close file
3. Create new file
4. Paste content
5. Save as UTF-8

### Problem: Git Shows Changes But File Looks Same

**Cause**: Line ending changes (CRLF vs LF)

**Solution**:
```bash
git config core.autocrlf true
git add --renormalize .
```

### Problem: Emojis in Console Look Wrong

**Cause**: Console font doesn't support emojis

**Solution**:
- Windows: Use Windows Terminal (not cmd.exe)
- Change font to one with emoji support (e.g., "Cascadia Code")

## Verification Steps

### 1. Check File Encoding
```powershell
# PowerShell
[System.Text.Encoding]::Default
Get-Content file.json -Encoding UTF8 | Select-String "??"
```

### 2. Visual Check
Open file and verify:
- Emojis look correct
- No `??` or `?` characters
- Quotes and dashes display properly

### 3. JSON Validation
```bash
# Validate JSON is well-formed
jq . < file.json
```

### 4. Application Test
- Restart app
- Check logs for spec loading
- Verify UI displays correctly

## Summary

? **Always use UTF-8 encoding**
? **Include BOM for .cs, .json, .cshtml files**
? **Test with emoji test string**
? **Configure Visual Studio/VS Code correctly**
? **Set up .gitattributes**
? **Verify after saving**

---

**This ensures emojis, special characters, and international text display correctly across all platforms!** ??
