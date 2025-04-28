# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy the QueryService project file into the container
COPY ["QueryService.csproj", "."]

# Restore dependencies for the QueryService project
RUN dotnet restore "./QueryService.csproj"

# Copy the rest of the source code into the container
COPY . .

# Publish the QueryService project in Release mode for a Linux-based environment
RUN dotnet publish "QueryService.csproj" -c Release -o /app/publish /p:UseAppHost=false --os linux --arch x64

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Copy the published files from the build stage to the runtime stage
COPY --from=build /app/publish .

# Expose the necessary ports
EXPOSE 80
EXPOSE 9091

# Define the entrypoint for the container to start the QueryService application
ENTRYPOINT ["dotnet", "QueryService.dll"]
