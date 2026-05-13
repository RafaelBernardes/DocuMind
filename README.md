# DocuMind
Project to use as a study for AI engineering

## Configuration

See `src/DocuMind.Api/appsettings.example.json` for the required sections and example local paths.

OpenAI credentials must not be committed in `appsettings*.json`. Provide secrets via environment variables instead.

Example:

```powershell
$env:OpenAI__ApiKey="your-openai-api-key"
```

Non-secret OpenAI settings such as `Endpoint`, `ChatModel`, and `EmbeddingModel` can stay in configuration files.

## Project Docs

- `docs/status.md`: current implementation status
- `docs/backlog.md`: prioritized remaining work
