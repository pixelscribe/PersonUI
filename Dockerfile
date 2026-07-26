# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY PersonUI.csproj ./
RUN dotnet restore PersonUI.csproj

COPY . .
RUN dotnet publish PersonUI.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

COPY --from=build /app .

USER $APP_UID
ENTRYPOINT ["dotnet", "PersonUI.dll"]
