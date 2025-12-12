using Microsoft.Playwright;
using System;
using System.Threading.Tasks;

namespace UkrainianBotTaras
{
    public static class HumanExtensions
    {
        private static readonly Random _rng = new Random();

        public static async Task HumanClickAsync(this ILocator locator, IPage? page = null)
        {
            // 1. "Thinking" pause
            await Task.Delay(_rng.Next(300, 1000));

            // 2. Get element size
            var box = await locator.BoundingBoxAsync();
            if (box == null)
            {
                await locator.ClickAsync();
            }
            else
            {
                var width = box.Width;
                var height = box.Height;

                // float casting for Playwright Position
                var randomX = (float)((width * 0.1) + (_rng.NextDouble() * (width * 0.8)));
                var randomY = (float)((height * 0.1) + (_rng.NextDouble() * (height * 0.8)));

                await locator.ClickAsync(new LocatorClickOptions
                {
                    Position = new Position { X = randomX, Y = randomY }
                });
            }
        }

        public static async Task HumanFillAsync(this ILocator locator, string text, IPage? page = null)
        {
            await locator.HumanClickAsync(page); // Pass page to click first
            await locator.ClearAsync();

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