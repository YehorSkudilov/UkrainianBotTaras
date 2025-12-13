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
            try
            {
                await steps.RunAsync(Page);
            }
            finally
            {
                // Ensure the browser closes even if steps fail
                await CloseAsync();
            }
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

            bool isHeadless = Environment.GetEnvironmentVariable("HEADLESS") != "false"; // Default to true in Docker

            // 1. Launch with Anti-Bot Arguments
            browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                Args = new[]
                {
                    "--disable-blink-features=AutomationControlled", // Hides the "Robot" flag
                    "--disable-infobars",
                    "--window-size=1280,800",
                    "--no-sandbox"
                },
                IgnoreDefaultArgs = new[] { "--enable-automation" } // Removes "Chrome is controlled by..." banner
            });

            // 2. Create Context to manage User Agent & Scripts
            context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                // Spoof a real Windows 10 User Agent
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36",
                ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
                Locale = "uk-UA", // Set locale to Ukrainian for authenticity
                TimezoneId = "Europe/Kiev"
            });

            // ---------------------------------------------------------
            // INCREASE TIMEOUTS HERE
            // ---------------------------------------------------------
            // 60000ms = 60 seconds (Double the default)
            context.SetDefaultTimeout(120000);           // For actions (Click, Fill, WaitForSelector)
            context.SetDefaultNavigationTimeout(120000); // For Page.GotoAsync, WaitForLoadState
            // ---------------------------------------------------------

            // 3. Inject the Stealth Script
            // This runs before the page loads to delete the 'webdriver' property
            await context.AddInitScriptAsync(@"
                Object.defineProperty(navigator, 'webdriver', {
                    get: () => undefined
                });
            ");

            // 4. Create Page from the stealth Context
            page = await context.NewPageAsync();
        }

        private IPage Page =>
            page ?? throw new InvalidOperationException("Bot not initialized");

        public async ValueTask DisposeAsync()
        {
            // Close the browser, which closes context and pages automatically
            if (browser != null)
                await browser.DisposeAsync();

            playwright?.Dispose();
        }
    }
}