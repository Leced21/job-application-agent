FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/services/Profile/JobApplicationAgent.Profile.Api/JobApplicationAgent.Profile.Api.csproj", "src/services/Profile/JobApplicationAgent.Profile.Api/"]
COPY ["src/services/Profile/JobApplicationAgent.Profile.Application/JobApplicationAgent.Profile.Application.csproj", "src/services/Profile/JobApplicationAgent.Profile.Application/"]
COPY ["src/services/Profile/JobApplicationAgent.Profile.Domain/JobApplicationAgent.Profile.Domain.csproj", "src/services/Profile/JobApplicationAgent.Profile.Domain/"]
COPY ["src/services/Profile/JobApplicationAgent.Profile.Infrastructure/JobApplicationAgent.Profile.Infrastructure.csproj", "src/services/Profile/JobApplicationAgent.Profile.Infrastructure/"]

RUN dotnet restore \
    "src/services/Profile/JobApplicationAgent.Profile.Api/JobApplicationAgent.Profile.Api.csproj"

COPY . .

RUN dotnet publish \
    "src/services/Profile/JobApplicationAgent.Profile.Api/JobApplicationAgent.Profile.Api.csproj" \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM base AS final
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "JobApplicationAgent.Profile.Api.dll"]