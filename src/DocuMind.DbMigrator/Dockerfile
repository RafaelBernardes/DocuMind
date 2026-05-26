FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["DocuMind.sln", "."]
COPY ["src/DocuMind.DbMigrator/DocuMind.DbMigrator.csproj", "src/DocuMind.DbMigrator/"]
COPY ["src/DocuMind.Infrastructure/DocuMind.Infrastructure.csproj", "src/DocuMind.Infrastructure/"]
COPY ["src/DocuMind.Core/DocuMind.Core.csproj", "src/DocuMind.Core/"]

RUN dotnet restore "src/DocuMind.DbMigrator/DocuMind.DbMigrator.csproj"

COPY . .
RUN dotnet publish "src/DocuMind.DbMigrator/DocuMind.DbMigrator.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
COPY src/DocuMind.Api/appsettings.json ./

ENTRYPOINT ["dotnet", "DocuMind.DbMigrator.dll"]
