# ============================
# BUILD STAGE (.NET 9 SDK)
# ============================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

COPY UkrainianBotTaras.csproj ./
RUN dotnet restore UkrainianBotTaras.csproj

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
# Install required runtimes:
#  - .NET 8 → PowerShell / Playwright
#  - .NET 9 → Your app
# --------------------------------
RUN apt-get update \
    && apt-get install -y wget ca-certificates \
    && wget https://dot.net/v1/dotnet-install.sh \
    && chmod +x dotnet-install.sh \
    && ./dotnet-install.sh --channel 8.0 --runtime dotnet \
    && ./dotnet-install.sh --channel 9.0 --runtime dotnet \
    && rm dotnet-install.sh \
    && rm -rf /var/lib/apt/lists/*

# --------------------------------
# Copy published app
# --------------------------------
COPY --from=build /app/publish .

# --------------------------------
# Install Playwright browser
# --------------------------------
RUN pwsh playwright.ps1 install chromium

# --------------------------------
# Run the bot
# --------------------------------
ENTRYPOINT ["dotnet", "UkrainianBotTaras.dll"]
