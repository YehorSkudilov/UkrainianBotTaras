using Microsoft.Playwright;
using System;
using System.Threading.Tasks;

namespace UkrainianBotTaras.Src
{
    class Bot : IAsyncDisposable
    {
        private IPlaywright? playwright;
        private IBrowser? browser;
        private IBrowserContext? context; 
        private IPage? page;

        public static async Task<Bot> CreateAsync()
        {
            var bot = new Bot();
            await bot.InitializeAsync();
            return bot;
        }

        public async Task RunAsync(ISteps steps)
        {
            await steps.RunAsync(Page);
        }

        public async Task CloseAsync()
        {
            if (browser != null)
            {
                Console.WriteLine("🛑 Shutting down browser...");
                await browser.CloseAsync();
                browser = null;
            }
        }

        private async Task InitializeAsync()
        {
            playwright = await Playwright.CreateAsync();

            var profileDir = Path.Combine(
                AppContext.BaseDirectory,
                "BrowserProfile"
            );

            Directory.CreateDirectory(profileDir);

            context = await playwright.Chromium.LaunchPersistentContextAsync(
                profileDir,
                new BrowserTypeLaunchPersistentContextOptions
                {
                    Headless = false,
                    Args = new[]
                    {
                "--disable-blink-features=AutomationControlled",
                "--disable-infobars",
                "--window-size=1280,800"
                    },
                    IgnoreDefaultArgs = new[] { "--enable-automation" },
                    Locale = "uk-UA",
                    TimezoneId = "Europe/Kiev",
                    ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
                    UserAgent =
                        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                        "AppleWebKit/537.36 (KHTML, like Gecko) " +
                        "Chrome/126.0.0.0 Safari/537.36"
                }
            );

            context.SetDefaultTimeout(120_000);
            context.SetDefaultNavigationTimeout(120_000);

            await context.AddInitScriptAsync("""
        Object.defineProperty(navigator, 'webdriver', {
            get: () => undefined
        });
    """);

            page = context.Pages.Count > 0
                ? context.Pages[0]
                : await context.NewPageAsync();
        }


        private IPage Page =>
            page ?? throw new InvalidOperationException("Bot not initialized");

        public async ValueTask DisposeAsync()
        {
            if (context != null)
                await context.CloseAsync();

            playwright?.Dispose();
        }
    }
}