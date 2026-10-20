FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/services/Matching/JobApplicationAgent.Matching.Api/JobApplicationAgent.Matching.Api.csproj", "src/services/Matching/JobApplicationAgent.Matching.Api/"]
COPY ["src/services/Matching/JobApplicationAgent.Matching.Application/JobApplicationAgent.Matching.Application.csproj", "src/services/Matching/JobApplicationAgent.Matching.Application/"]
COPY ["src/services/Matching/JobApplicationAgent.Matching.Domain/JobApplicationAgent.Matching.Domain.csproj", "src/services/Matching/JobApplicationAgent.Matching.Domain/"]
COPY ["src/services/Matching/JobApplicationAgent.Matching.Infrastructure/JobApplicationAgent.Matching.Infrastructure.csproj", "src/services/Matching/JobApplicationAgent.Matching.Infrastructure/"]

RUN dotnet restore "src/services/Matching/JobApplicationAgent.Matching.Api/JobApplicationAgent.Matching.Api.csproj"

COPY . .

RUN dotnet publish \
    "src/services/Matching/JobApplicationAgent.Matching.Api/JobApplicationAgent.Matching.Api.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "JobApplicationAgent.Matching.Api.dll"]
