FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Backend/NotificationCore/NotificationCore.Api/NotificationCore.Api.csproj", "Backend/NotificationCore/NotificationCore.Api/"]
COPY ["Backend/NotificationCore/NotificationCore.Application/NotificationCore.Application.csproj", "Backend/NotificationCore/NotificationCore.Application/"]
COPY ["Backend/NotificationCore/NotificationCore.Domain/NotificationCore.Domain.csproj", "Backend/NotificationCore/NotificationCore.Domain/"]
COPY ["Backend/NotificationCore/NotificationCore.Infrastructure/NotificationCore.Infrastructure.csproj", "Backend/NotificationCore/NotificationCore.Infrastructure/"]
COPY ["Shared/Messaging.Contracts/Messaging.Contracts.csproj", "Shared/Messaging.Contracts/"]
COPY ["Shared/Observability/Shared.Observability.csproj", "Shared/Observability/"]

RUN dotnet restore "Backend/NotificationCore/NotificationCore.Api/NotificationCore.Api.csproj"

COPY . .

RUN dotnet publish "Backend/NotificationCore/NotificationCore.Api/NotificationCore.Api.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "NotificationCore.Api.dll"]
