using Microsoft.Playwright;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace UkrainianBotTaras.Src
{
    class Bot : IAsyncDisposable
    {
        private IPlaywright? playwright;
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
            try { await steps.RunAsync(Page); }
            finally { await CloseAsync(); }
        }

        public async Task CloseAsync()
        {
            if (context != null)
            {
                await context.CloseAsync();
                context = null;
            }
        }

        private async Task InitializeAsync(StepsContext ctx)
        {
            playwright = await Playwright.CreateAsync();
            string userDataDir = Path.Combine(ctx.DataDirectory, "BrowserProfile");

            context = await playwright.Chromium.LaunchPersistentContextAsync(
                userDataDir,
                new BrowserTypeLaunchPersistentContextOptions
                {
                    Headless = false,
                    Args = new[] {
                "--disable-blink-features=AutomationControlled",
                "--no-sandbox",
                "--disable-infobars",
                // Match your target's dual-monitor offset
                "--window-position=-1920,0"
                    },
                    ViewportSize = new ViewportSize { Width = 1920, Height = 911 },

                    // Canada BC Localization
                    Locale = "en-US",
                    TimezoneId = "America/Vancouver",
                    Geolocation = new Geolocation { Latitude = 49.2827f, Longitude = -123.1207f },

                    // FIX: Empty array ensures Permissions state is 'prompt' (human) instead of 'denied'
                    Permissions = new string[] { },

                    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/143.0.0.0 Safari/537.36"
                }
            );

            await context.AddInitScriptAsync(@"
        // 1. Lightweight WebDriver Removal
        const newProto = navigator.__proto__;
        delete newProto.webdriver;
        navigator.__proto__ = newProto;

        // 2. Fix detailChrome Errors (Mocking Constructors)
        const makeNative = (name) => {
            const fn = function() { [native code] };
            Object.defineProperty(fn, 'name', { value: name });
            return fn;
        };

        window.chrome = {
            runtime: {
                connect: makeNative('connect'),
                sendMessage: makeNative('sendMessage'),
                getPlatformInfo: makeNative('getPlatformInfo'),
                getURL: makeNative('getURL'),
                OnInstalledReason: { INSTALL: 'install', UPDATE: 'update' }
            },
            loadTimes: makeNative('loadTimes'),
            csi: makeNative('csi'),
            app: {
                isInstalled: false,
                InstallState: { INSTALLED: 'installed', NOT_INSTALLED: 'not_installed' },
                RunningState: { READY_TO_RUN: 'ready_to_run', RUNNING: 'running' }
            }
        };

        // 3. Mathematical Screen Measurements (Align Outer/Inner)
        Object.defineProperty(window, 'outerHeight', { get: () => 1045 });
        Object.defineProperty(window, 'outerWidth', { get: () => 1936 });
    ");

            page = context.Pages.Count > 0 ? context.Pages[0] : await context.NewPageAsync();
        }
        private IPage Page => page ?? throw new InvalidOperationException("Bot not initialized");

        public async ValueTask DisposeAsync()
        {
            if (context != null) await context.DisposeAsync();
            playwright?.Dispose();
        }
    }
}