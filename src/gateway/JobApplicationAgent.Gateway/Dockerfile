FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/gateway/JobApplicationAgent.Gateway/JobApplicationAgent.Gateway.csproj", "src/gateway/JobApplicationAgent.Gateway/"]
RUN dotnet restore "src/gateway/JobApplicationAgent.Gateway/JobApplicationAgent.Gateway.csproj"
COPY . .
RUN dotnet publish "src/gateway/JobApplicationAgent.Gateway/JobApplicationAgent.Gateway.csproj" --configuration Release --output /app/publish --no-restore

FROM base AS final
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "JobApplicationAgent.Gateway.dll"]
