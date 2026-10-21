FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/services/Collection/JobApplicationAgent.Collection.Api/JobApplicationAgent.Collection.Api.csproj", "src/services/Collection/JobApplicationAgent.Collection.Api/"]
COPY ["src/services/Collection/JobApplicationAgent.Collection.Application/JobApplicationAgent.Collection.Application.csproj", "src/services/Collection/JobApplicationAgent.Collection.Application/"]
COPY ["src/services/Collection/JobApplicationAgent.Collection.Domain/JobApplicationAgent.Collection.Domain.csproj", "src/services/Collection/JobApplicationAgent.Collection.Domain/"]
COPY ["src/services/Collection/JobApplicationAgent.Collection.Infrastructure/JobApplicationAgent.Collection.Infrastructure.csproj", "src/services/Collection/JobApplicationAgent.Collection.Infrastructure/"]

RUN dotnet restore "src/services/Collection/JobApplicationAgent.Collection.Api/JobApplicationAgent.Collection.Api.csproj"

COPY . .

RUN dotnet publish \
    "src/services/Collection/JobApplicationAgent.Collection.Api/JobApplicationAgent.Collection.Api.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "JobApplicationAgent.Collection.Api.dll"]
