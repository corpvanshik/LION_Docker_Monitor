FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build

WORKDIR /app

COPY . .

RUN dotnet restore ./LION_Docker_Monitor.csproj
RUN dotnet publish ./LION_Docker_Monitor.csproj -c Release -o /app/out

FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine

WORKDIR /app

COPY --from=build /app/out .

VOLUME /var/run/docker.sock

CMD ["dotnet", "LION_Docker_Monitor.dll"]
