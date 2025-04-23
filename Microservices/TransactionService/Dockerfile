# Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/TransactionService.API/TransactionService.API.csproj", "TransactionService.API/"]
RUN dotnet restore "TransactionService.API/TransactionService.API.csproj"
COPY src/TransactionService.API/ TransactionService.API/

# Install required tools
RUN apt-get update && apt-get install -y sed

# Fix missing using statements
RUN sed -i '1s/^/using System.Security.Cryptography;\nusing System.Text;\n\n/' TransactionService.API/Infrastructure/Data/Repositories/TransactionRepository.cs
RUN sed -i '1s/^/using System.Security.Cryptography;\n\n/' TransactionService.API/Controllers/TransactionController.cs

# Fix the duplicate sanitizedTransferId issues - find specific lines containing both the variable and SanitizeInput
RUN grep -n "sanitizedTransferId = SanitizeInput" TransactionService.API/Infrastructure/Data/Repositories/TransactionRepository.cs || echo "Not found"
RUN grep -n "sanitizedTransferId = SanitizeInput" TransactionService.API/Controllers/TransactionController.cs || echo "Not found"

# Remove the lines with the duplicate variable declarations
RUN sed -i '/sanitizedTransferId = SanitizeInput/d' TransactionService.API/Infrastructure/Data/Repositories/TransactionRepository.cs
RUN sed -i '/sanitizedTransferId = SanitizeInput/d' TransactionService.API/Controllers/TransactionController.cs

# Show the modified files
RUN grep -n "transferId" TransactionService.API/Infrastructure/Data/Repositories/TransactionRepository.cs
RUN grep -n "transferId" TransactionService.API/Controllers/TransactionController.cs

WORKDIR "/src"
RUN dotnet build "TransactionService.API/TransactionService.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TransactionService.API/TransactionService.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TransactionService.API.dll"]