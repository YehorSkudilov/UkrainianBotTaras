# ============================
# BUILD STAGE (.NET 9)
# ============================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

# Copy project file and restore
COPY UkrainianBotTaras.csproj ./
RUN dotnet restore UkrainianBotTaras.csproj

# Copy the rest of the source and publish
COPY . .
RUN dotnet publish UkrainianBotTaras.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ============================
# RUNTIME STAGE (Playwright)
# ============================
FROM mcr.microsoft.com/playwright/dotnet:v1.49.0-noble

WORKDIR /app

# --------------------------------
# Install .NET 8 runtime (required by pwsh)
# --------------------------------
RUN apt-get update \
    && apt-get install -y wget ca-certificates \
    && wget https://dot.net/v1/dotnet-install.sh \
    && chmod +x dotnet-install.sh \
    && ./dotnet-install.sh --channel 8.0 --runtime dotnet \
    && rm dotnet-install.sh \
    && rm -rf /var/lib/apt/lists/*

# --------------------------------
# Copy published app
# --------------------------------
COPY --from=build /app/publish .

# --------------------------------
# Install Playwright Chromium
# --------------------------------
RUN pwsh playwright.ps1 install chromium

# --------------------------------
# Run the bot
# --------------------------------
ENTRYPOINT ["dotnet", "UkrainianBotTaras.dll"]
