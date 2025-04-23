# Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/TransactionService.API/TransactionService.API.csproj", "TransactionService.API/"]
RUN dotnet restore "TransactionService.API/TransactionService.API.csproj"
COPY ["src/TransactionService.API/", "TransactionService.API/"]
WORKDIR "/src/TransactionService.API"
RUN dotnet build "TransactionService.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TransactionService.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TransactionService.API.dll"]