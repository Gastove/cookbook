FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /app
COPY . /app

RUN dotnet tool restore
RUN dotnet paket restore
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0

ENV DOTNET_ENVIRONMENT="Production"

WORKDIR /app

COPY --from=build /app ./

ENTRYPOINT ["dotnet", "Cookbook.dll"]
