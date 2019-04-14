FROM mcr.microsoft.com/dotnet/core/sdk:2.2.105 AS build
# FROM microsoft/dotnet:2.2-sdk AS build

RUN apt-get update && apt-get install -y locales && rm -rf /var/lib/apt/lists/* \
    && localedef -i en_US -c -f UTF-8 -A /usr/share/locale/locale.alias en_US.UTF-8
ENV LANG en_US.utf8

WORKDIR /build
COPY . /build

RUN dotnet tool install -g paket
ENV PATH="$PATH:/root/.dotnet/tools"

RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/core/runtime:2.2.4-stretch-slim

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

WORKDIR /app

COPY --from=build /app ./

ENTRYPOINT ["dotnet", "Cookbook.dll"]
