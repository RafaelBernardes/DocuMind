FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["DocuMind.sln", "."]
COPY ["src/DocuMind.Api/DocuMind.Api.csproj", "src/DocuMind.Api/"]
COPY ["src/DocuMind.Core/DocuMind.Core.csproj", "src/DocuMind.Core/"]
COPY ["src/DocuMind.Infrastructure/DocuMind.Infrastructure.csproj", "src/DocuMind.Infrastructure/"]

RUN dotnet restore "src/DocuMind.Api/DocuMind.Api.csproj"

COPY . .
RUN dotnet publish "src/DocuMind.Api/DocuMind.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "DocuMind.Api.dll"]
