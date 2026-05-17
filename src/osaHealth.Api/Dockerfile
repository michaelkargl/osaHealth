# Build stage — restore and publish the Oxpecker F# service.
FROM mcr.microsoft.com/dotnet/sdk:11.0-preview AS build
WORKDIR /src

COPY osaHealth.Api.fsproj .
RUN dotnet restore osaHealth.Api.fsproj

COPY . .
RUN dotnet publish osaHealth.Api.fsproj -c Release -o /app --no-restore

# Runtime stage — minimal ASP.NET Core image.
FROM mcr.microsoft.com/dotnet/aspnet:11.0-preview AS runtime
WORKDIR /app
COPY --from=build /app .

# Kestrel listens on 8080; this must match the DAPR sidecar --app-port.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "osaHealth.Api.dll"]
