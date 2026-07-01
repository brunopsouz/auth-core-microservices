FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Backend/AuthCore/AuthCore.Api/AuthCore.Api.csproj", "Backend/AuthCore/AuthCore.Api/"]
COPY ["Backend/AuthCore/AuthCore.Application/AuthCore.Application.csproj", "Backend/AuthCore/AuthCore.Application/"]
COPY ["Backend/AuthCore/AuthCore.Domain/AuthCore.Domain.csproj", "Backend/AuthCore/AuthCore.Domain/"]
COPY ["Backend/AuthCore/AuthCore.Infrastructure/AuthCore.Infrastructure.csproj", "Backend/AuthCore/AuthCore.Infrastructure/"]
COPY ["Shared/Messaging.Contracts/Messaging.Contracts.csproj", "Shared/Messaging.Contracts/"]

RUN dotnet restore "Backend/AuthCore/AuthCore.Api/AuthCore.Api.csproj"

COPY . .

RUN dotnet publish "Backend/AuthCore/AuthCore.Api/AuthCore.Api.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "AuthCore.Api.dll"]
