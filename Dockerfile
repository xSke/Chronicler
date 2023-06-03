FROM mcr.microsoft.com/dotnet/sdk:6.0

WORKDIR /app

ADD SIBR.Storage.API/SIBR.Storage.API.csproj SIBR.Storage.API/
ADD SIBR.Storage.CLI/SIBR.Storage.CLI.csproj SIBR.Storage.CLI/
ADD SIBR.Storage.Data/SIBR.Storage.Data.csproj SIBR.Storage.Data/
ADD SIBR.Storage.Ingest/SIBR.Storage.Ingest.csproj SIBR.Storage.Ingest/
ADD SIBR.Storage.API.Tests/SIBR.Storage.API.Tests.csproj SIBR.Storage.API.Tests/
ADD SIBR.sln .
RUN dotnet restore

ADD . /app
RUN dotnet publish -c Release -o out/

WORKDIR /app/out
ENTRYPOINT ["dotnet"]
