FROM registry.gitlab.com/gastove/containers/dotnet/builder:3 AS build

WORKDIR /build
COPY . /build

RUN paket restore

FROM build AS release

RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/core/runtime:3.0

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

WORKDIR /app

COPY --from=release /app ./

ENTRYPOINT ["dotnet", "Cookbook.dll"]
