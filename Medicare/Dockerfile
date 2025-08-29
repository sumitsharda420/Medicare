# Base image: official .NET runtime
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

# Copy published files
COPY bin/Debug/net8.0/ ./

# Run app
ENTRYPOINT ["dotnet", "Medicare.dll"]