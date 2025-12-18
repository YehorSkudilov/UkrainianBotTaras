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

        public static async Task<Bot> CreateAsync(StepsContext ctx)
        {
            var bot = new Bot();
            await bot.InitializeAsync(ctx);
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

        private async Task InitializeAsync(StepsContext ctx)
        {
            playwright = await Playwright.CreateAsync();

            string userDataDir = Path.Combine(ctx.DataDirectory, "BrowserProfile");

            // Launch persistent context (auto-reuses storage)
            context = await playwright.Chromium.LaunchPersistentContextAsync(
                userDataDir,
                new BrowserTypeLaunchPersistentContextOptions
                {
                    Headless = false,
                    Args = new[] { "--disable-blink-features=AutomationControlled" },
                    ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
                    Locale = "uk-UA",
                    TimezoneId = "Europe/Kiev"
                }
            );

            // Increase timeouts
            context.SetDefaultTimeout(120000);
            context.SetDefaultNavigationTimeout(120000);

            // Inject stealth script
            await context.AddInitScriptAsync(@"
        Object.defineProperty(navigator, 'webdriver', {
            get: () => undefined
        });
    ");

            // Use existing page if available
            page = context.Pages.Count > 0 ? context.Pages[0] : await context.NewPageAsync();
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