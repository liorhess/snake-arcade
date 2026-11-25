# Multi-stage Linux build for Railway / Render (no Windows/IIS dependency)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY SnakeWebForms/SnakeWebForms.csproj SnakeWebForms/
RUN dotnet restore SnakeWebForms/SnakeWebForms.csproj

COPY SnakeWebForms/ SnakeWebForms/
RUN dotnet publish SnakeWebForms/SnakeWebForms.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish ./
RUN mkdir -p /app/App_Data && chown -R app:app /app/App_Data

USER app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SnakeWebForms.dll"]
