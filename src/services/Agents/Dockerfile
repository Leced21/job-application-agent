FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/services/Agents/JobApplicationAgent.Agents.Api/JobApplicationAgent.Agents.Api.csproj", "src/services/Agents/JobApplicationAgent.Agents.Api/"]
COPY ["src/services/Agents/JobApplicationAgent.Agents.Application/JobApplicationAgent.Agents.Application.csproj", "src/services/Agents/JobApplicationAgent.Agents.Application/"]
COPY ["src/services/Agents/JobApplicationAgent.Agents.Domain/JobApplicationAgent.Agents.Domain.csproj", "src/services/Agents/JobApplicationAgent.Agents.Domain/"]
COPY ["src/services/Agents/JobApplicationAgent.Agents.Infrastructure/JobApplicationAgent.Agents.Infrastructure.csproj", "src/services/Agents/JobApplicationAgent.Agents.Infrastructure/"]

RUN dotnet restore "src/services/Agents/JobApplicationAgent.Agents.Api/JobApplicationAgent.Agents.Api.csproj"

COPY . .

RUN dotnet publish \
    "src/services/Agents/JobApplicationAgent.Agents.Api/JobApplicationAgent.Agents.Api.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "JobApplicationAgent.Agents.Api.dll"]
