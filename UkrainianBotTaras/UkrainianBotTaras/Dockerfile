# STAGE 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy the project file first to cache dependencies
COPY ["UkrainianBotTaras.csproj", "./"]
RUN dotnet restore "UkrainianBotTaras.csproj"

# Copy ALL source code (including Src folder and everything else)
COPY . .

# Build the app
RUN dotnet publish "UkrainianBotTaras.csproj" -c Release -o /app/publish

# STAGE 2: Runtime (Playwright + .NET 9)
FROM mcr.microsoft.com/playwright/dotnet:v1.49.0-noble AS final
WORKDIR /app

# --- Install .NET 9 Runtime Manually ---
# The Playwright base image defaults to .NET 8, so we add 9.0 support.
USER root
RUN apt-get update && apt-get install -y wget && \
    wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh && \
    chmod +x dotnet-install.sh && \
    ./dotnet-install.sh --channel 9.0 --runtime dotnet && \
    ./dotnet-install.sh --channel 9.0 --runtime aspnetcore

# Add .NET 9 to the system PATH
ENV DOTNET_ROOT=/root/.dotnet
ENV PATH=$PATH:$DOTNET_ROOT:$DOTNET_ROOT/tools

# Copy the compiled app from Stage 1
COPY --from=build /app/publish .

# Install Chromium browser
RUN pwsh playwright.ps1 install chromium

# Start the bot
ENTRYPOINT ["dotnet", "UkrainianBotTaras.dll"]