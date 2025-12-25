# Copilot Session Documentation

This folder contains all AI-generated documentation from GitHub Copilot sessions for the DecisionSpark project.

## Folder Structure

Documentation is organized by month in `session-{YYYY-MM}` folders:

```
docs/copilot/
??? session-2025-01/     # January 2025 session documents
?   ??? openai-integration-guide.md
?   ??? web-ui-documentation.md
?   ??? ...
??? session-2025-02/     # February 2025 session documents (future)
    ??? ...
```

## Documentation Policy

### Mandatory Rules
1. **All Copilot-generated `.md` files** must be placed in the appropriate `session-{YYYY-MM}` folder
2. **No `.md` files** are allowed in the repository root except:
   - `README.md` (GitHub repository overview)
   - `copilot-instructions.md` (AI development guidelines)
3. Create a new session folder for each month: `session-YYYY-MM`
4. Use descriptive filenames: `feature-name-guide.md`, `troubleshooting-issue.md`, etc.

### Document Types in Session Folders
- **Implementation Guides**: Step-by-step guides for implementing features
- **Troubleshooting Docs**: Issue analysis and resolution documentation
- **API Documentation**: Detailed API endpoint documentation
- **Configuration Guides**: Setup and configuration instructions
- **Integration Summaries**: Third-party integration documentation
- **Decision Records**: Architectural and design decisions

## File Naming Conventions

Use lowercase with hyphens for readability:
- ? `openai-integration-guide.md`
- ? `web-ui-implementation-summary.md`
- ? `debugging-fix-logging.md`
- ? `OpenAIIntegrationGuide.md`
- ? `web_ui_implementation_summary.md`

## Session Organization

### Monthly Sessions
Each month's AI-assisted development work is archived in a separate session folder. This provides:
- **Historical Context**: Track how the project evolved over time
- **Decision Trail**: Understand why certain approaches were chosen
- **Knowledge Base**: Reference past solutions for similar problems
- **Onboarding**: Help new team members understand the project history

### What to Include
- Feature implementation documentation
- Troubleshooting guides and issue resolutions
- API endpoint documentation and examples
- Configuration and setup guides
- Integration documentation with external services
- Performance optimization notes
- Security and authentication documentation

### What NOT to Include
- Source code files (these go in `/DecisionSpark/` project folder)
- Configuration files with secrets (excluded via `.gitignore`)
- Build artifacts or logs
- Temporary notes or drafts (clean up before committing)

## Maintenance

### Regular Cleanup
- Review and consolidate duplicate documentation
- Update outdated information when code changes
- Archive obsolete documents to maintain relevance
- Create summary documents for long sessions

### Quality Guidelines
- Use clear, descriptive titles
- Include code examples with syntax highlighting
- Add timestamps for time-sensitive information
- Reference related documents when applicable
- Keep formatting consistent across all documents

## Accessing Documentation

### For Developers
1. Check `copilot-instructions.md` in repository root for AI guidelines
2. Browse session folders chronologically or by topic
3. Use full-text search to find specific topics across sessions
4. Reference recent session folders for current implementation details

### For GitHub Copilot
When generating new documentation:
1. Determine the current month (YYYY-MM format)
2. Place files in `/docs/copilot/session-{YYYY-MM}/`
3. Use descriptive, lowercase-with-hyphens filenames
4. Include context and timestamps in the content
5. Follow markdown formatting conventions

## Questions?

Refer to `copilot-instructions.md` in the repository root for comprehensive AI-assisted development guidelines and project conventions.

---

**Structure Created**: January 2025
**Last Updated**: January 2025
