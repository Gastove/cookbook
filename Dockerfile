FROM fsharp:10.2.1-netcore
WORKDIR /app

COPY . /app/

RUN dotnet build -c Release -o out

EXPOSE 8080

ENTRYPOINT ["dotnet", "src/Cookbook/out/Cookbook.dll"]
