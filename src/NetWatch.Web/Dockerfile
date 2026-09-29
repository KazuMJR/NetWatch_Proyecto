FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /source
COPY global.json Directory.Build.props NuGet.Config ./
COPY src/NetWatch.Web/NetWatch.Web.csproj src/NetWatch.Web/
RUN dotnet restore src/NetWatch.Web/NetWatch.Web.csproj
COPY src/NetWatch.Web src/NetWatch.Web
RUN dotnet publish src/NetWatch.Web/NetWatch.Web.csproj -c Release --no-restore -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "NetWatch.Web.dll"]
