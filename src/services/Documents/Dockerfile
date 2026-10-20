FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/services/Documents/JobApplicationAgent.Documents.Api/JobApplicationAgent.Documents.Api.csproj", "src/services/Documents/JobApplicationAgent.Documents.Api/"]
COPY ["src/services/Documents/JobApplicationAgent.Documents.Application/JobApplicationAgent.Documents.Application.csproj", "src/services/Documents/JobApplicationAgent.Documents.Application/"]
COPY ["src/services/Documents/JobApplicationAgent.Documents.Domain/JobApplicationAgent.Documents.Domain.csproj", "src/services/Documents/JobApplicationAgent.Documents.Domain/"]
COPY ["src/services/Documents/JobApplicationAgent.Documents.Infrastructure/JobApplicationAgent.Documents.Infrastructure.csproj", "src/services/Documents/JobApplicationAgent.Documents.Infrastructure/"]

RUN dotnet restore "src/services/Documents/JobApplicationAgent.Documents.Api/JobApplicationAgent.Documents.Api.csproj"

COPY . .

RUN dotnet publish \
    "src/services/Documents/JobApplicationAgent.Documents.Api/JobApplicationAgent.Documents.Api.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "JobApplicationAgent.Documents.Api.dll"]
