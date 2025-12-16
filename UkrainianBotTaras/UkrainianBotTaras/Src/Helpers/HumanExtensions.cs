using Microsoft.Playwright;
using System;
using System.Threading.Tasks;

namespace UkrainianBotTaras
{
    public static class HumanExtensions
    {
        private static readonly Random _rng = new Random();

        public static async Task HumanClickAsync(this ILocator locator, IPage page)
        {
            await Task.Delay(_rng.Next(300, 900));

            var box = await locator.BoundingBoxAsync();
            if (box == null)
            {
                await locator.ClickAsync();
                return;
            }

            float targetX = (float)(
               box.X + (box.Width * (0.2 + _rng.NextDouble() * 0.6))
           );

            float targetY = (float)(
                box.Y + (box.Height * (0.2 + _rng.NextDouble() * 0.6))
            );

            await page.Mouse.MoveAsync(
                targetX,
                targetY,
                new MouseMoveOptions { Steps = _rng.Next(10, 25) }
            );

            await Task.Delay(_rng.Next(80, 200));

            await page.Mouse.DownAsync();
            await Task.Delay(_rng.Next(50, 120));
            await page.Mouse.UpAsync();
        }


        public static async Task HumanFillAsync(this ILocator locator, string text, IPage page)
        {
            await locator.HumanClickAsync(page); // Pass page to click first
            await locator.ClickAsync();
            await page.Keyboard.PressAsync("Control+A");
            await Task.Delay(_rng.Next(50, 120));
            await page.Keyboard.PressAsync("Backspace");

            foreach (char c in text)
            {
                await locator.TypeAsync(c.ToString(), new LocatorTypeOptions
                {
                    Delay = _rng.Next(50, 150)
                });

                if (_rng.NextDouble() < 0.1)
                {
                    await Task.Delay(_rng.Next(200, 500));
                }
            }
        }
    }
}