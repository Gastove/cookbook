FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build

WORKDIR /build
COPY . /build

RUN dotnet tool restore
RUN dotnet paket restore
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:5.0

EXPOSE 5000

WORKDIR /app

COPY --from=build /app ./

ENTRYPOINT ["dotnet", "Cookbook.dll"]
