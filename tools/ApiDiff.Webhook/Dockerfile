# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy the solution and restore dependencies
COPY *.sln .
COPY src/ApiDiff.Core/ApiDiff.Core.csproj src/ApiDiff.Core/
COPY src/ApiDiff.Licensing/ApiDiff.Licensing.csproj src/ApiDiff.Licensing/
COPY tools/ApiDiff.Webhook/ApiDiff.Webhook.csproj tools/ApiDiff.Webhook/

# We only restore the Webhook and its dependencies to optimize caching
RUN dotnet restore tools/ApiDiff.Webhook/ApiDiff.Webhook.csproj

# Copy the rest of the source code
COPY src/ src/
COPY tools/ tools/

# Publish the Webhook project
WORKDIR /app/tools/ApiDiff.Webhook
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Run the published image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Expose port (Render injects PORT environment variable)
EXPOSE 8080

# This matches the DLL name output by the publish step
ENTRYPOINT ["dotnet", "ApiDiff.Webhook.dll"]
