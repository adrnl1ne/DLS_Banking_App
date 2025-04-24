# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["TransactionService.csproj", "."]
RUN dotnet restore "./TransactionService.csproj"
COPY . .
RUN dotnet publish "TransactionService.csproj" -c Release -o /app/publish /p:UseAppHost=false --os linux --arch x64

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 80
EXPOSE 9091
ENTRYPOINT ["dotnet", "TransactionService.dll"]