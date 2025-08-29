# Use .NET SDK image instead of runtime
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /app

COPY . ./

# Restore dependencies
RUN dotnet restore

# Build the app
RUN dotnet build -c Debug

# Set entry point
CMD ["dotnet", "watch", "run", "--project", "Medicare.csproj"]