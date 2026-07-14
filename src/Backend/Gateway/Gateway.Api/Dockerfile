FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Backend/Gateway/Gateway.Api/Gateway.Api.csproj", "Backend/Gateway/Gateway.Api/"]
COPY ["Shared/Observability/Shared.Observability.csproj", "Shared/Observability/"]

RUN dotnet restore "Backend/Gateway/Gateway.Api/Gateway.Api.csproj"

COPY . .

RUN dotnet publish "Backend/Gateway/Gateway.Api/Gateway.Api.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Gateway.Api.dll"]
