FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source
COPY global.json Directory.Build.props NuGet.Config ./
COPY src/NetWatch.Data/NetWatch.Data.csproj src/NetWatch.Data/
COPY src/NetWatch.API/NetWatch.API.csproj src/NetWatch.API/
RUN dotnet restore src/NetWatch.API/NetWatch.API.csproj
COPY src/NetWatch.Data src/NetWatch.Data
COPY src/NetWatch.API src/NetWatch.API
RUN dotnet publish src/NetWatch.API/NetWatch.API.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends iputils-ping \
    && rm -rf /var/lib/apt/lists/*
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "NetWatch.API.dll"]
