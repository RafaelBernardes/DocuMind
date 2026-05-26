FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["DocuMind.sln", "."]
COPY ["src/DocuMind.Worker/DocuMind.Worker.csproj", "src/DocuMind.Worker/"]
COPY ["src/DocuMind.Core/DocuMind.Core.csproj", "src/DocuMind.Core/"]
COPY ["src/DocuMind.Infrastructure/DocuMind.Infrastructure.csproj", "src/DocuMind.Infrastructure/"]

RUN dotnet restore "src/DocuMind.Worker/DocuMind.Worker.csproj"

COPY . .
RUN dotnet publish "src/DocuMind.Worker/DocuMind.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "DocuMind.Worker.dll"]
