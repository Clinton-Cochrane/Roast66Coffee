# Use the official .NET SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies
COPY ./CoffeeShopApi/CoffeeShopApi.csproj ./CoffeeShopApi/
RUN dotnet restore "CoffeeShopApi/CoffeeShopApi.csproj"

# Copy the entire source code and build the application
COPY ./CoffeeShopApi ./CoffeeShopApi/
RUN dotnet publish "CoffeeShopApi/CoffeeShopApi.csproj" -c Release -o /app

# Use the official ASP.NET Core runtime image
# Use the official ASP.NET Core runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Install debugging tools
RUN apt-get update && apt-get install -y \
    netcat-openbsd \
    iputils-ping \
    curl \
    bash

COPY --from=build /app .

# Expose the port and run the application
EXPOSE 80
ENTRYPOINT ["dotnet", "CoffeeShopApi.dll"]



