FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/services/Job/JobApplicationAgent.Job.Api/JobApplicationAgent.Job.Api.csproj", "src/services/Job/JobApplicationAgent.Job.Api/"]
COPY ["src/services/Job/JobApplicationAgent.Job.Application/JobApplicationAgent.Job.Application.csproj", "src/services/Job/JobApplicationAgent.Job.Application/"]
COPY ["src/services/Job/JobApplicationAgent.Job.Domain/JobApplicationAgent.Job.Domain.csproj", "src/services/Job/JobApplicationAgent.Job.Domain/"]
COPY ["src/services/Job/JobApplicationAgent.Job.Infrastructure/JobApplicationAgent.Job.Infrastructure.csproj", "src/services/Job/JobApplicationAgent.Job.Infrastructure/"]

RUN dotnet restore "src/services/Job/JobApplicationAgent.Job.Api/JobApplicationAgent.Job.Api.csproj"

COPY . .

RUN dotnet publish \
    "src/services/Job/JobApplicationAgent.Job.Api/JobApplicationAgent.Job.Api.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "JobApplicationAgent.Job.Api.dll"]
