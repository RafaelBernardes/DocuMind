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

## Docker

The local Docker environment currently includes:

- `postgres` with `pgvector`
- `rabbitmq` with management UI
- `api`

Start the stack:

```powershell
docker compose up --build
```

The services will be available at:

- API: `http://localhost:8080`
- RabbitMQ UI: `http://localhost:15672`
- PostgreSQL: `localhost:5432`

OpenAI secrets must still come from environment variables and are passed through to the API container:

```powershell
$env:DOCUMIND_OPENAI_API_KEY="your-openai-api-key"
docker compose up --build
```

Important:

- The Docker stack does not apply EF Core migrations automatically yet.
- Before the first run, create/update the schema manually against the containerized PostgreSQL:

```powershell
$env:Postgres__ConnectionString="Host=localhost;Port=5432;Database=documind;Username=postgres;Password=postgres"
dotnet ef database update --project src/DocuMind.Infrastructure --startup-project src/DocuMind.Api
```

## Project Docs

- `docs/status.md`: current implementation status
- `docs/backlog.md`: prioritized remaining work
