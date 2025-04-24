FROM mcr.microsoft.com/dotnet/sdk:9.0

WORKDIR /app

# Copy csproj and restore dependencies (cached layer)
COPY *.csproj ./
RUN dotnet restore

# Copy the rest of the source code
COPY . ./

# Expose ports for the application and Prometheus metrics
EXPOSE 80
EXPOSE 9091

# Entrypoint to run dotnet watch
ENTRYPOINT ["sh", "-c", "dotnet watch run --urls http://*:80"]