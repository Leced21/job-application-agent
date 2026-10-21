FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/services/Applications/JobApplicationAgent.Applications.Api/JobApplicationAgent.Applications.Api.csproj", "src/services/Applications/JobApplicationAgent.Applications.Api/"]
COPY ["src/services/Applications/JobApplicationAgent.Applications.Application/JobApplicationAgent.Applications.Application.csproj", "src/services/Applications/JobApplicationAgent.Applications.Application/"]
COPY ["src/services/Applications/JobApplicationAgent.Applications.Domain/JobApplicationAgent.Applications.Domain.csproj", "src/services/Applications/JobApplicationAgent.Applications.Domain/"]
COPY ["src/services/Applications/JobApplicationAgent.Applications.Infrastructure/JobApplicationAgent.Applications.Infrastructure.csproj", "src/services/Applications/JobApplicationAgent.Applications.Infrastructure/"]

RUN dotnet restore "src/services/Applications/JobApplicationAgent.Applications.Api/JobApplicationAgent.Applications.Api.csproj"

COPY . .

RUN dotnet publish \
    "src/services/Applications/JobApplicationAgent.Applications.Api/JobApplicationAgent.Applications.Api.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "JobApplicationAgent.Applications.Api.dll"]
