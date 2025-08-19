# Base image for the ASP.NET Core runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5292  
# Use SDK image for building the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies
COPY ["MataaProxy/MataaProxy/MataaProxy.csproj", "./"]
RUN dotnet restore "./MataaProxy.csproj"

# Copy the entire source code
COPY . .

# Build the project in Release mode
RUN dotnet build "MataaProxy/MataaProxy/MataaProxy.csproj" -c Release -o /app/build

# Publish the app
FROM build AS publish
RUN dotnet publish "MataaProxy/MataaProxy/MataaProxy.csproj" -c Release -o /app/publish

# Final stage: Run the published app
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Set environment variable to ensure Ocelot uses the correct port
ENV ASPNETCORE_URLS=http://+:5292

# Set the entry point for the container
ENTRYPOINT ["dotnet", "MataaProxy.dll"]






