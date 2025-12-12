using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UkrainianBotTaras
{
    public static class HumanExtensions
    {
        private static readonly Random _rng = new Random();

        /// <summary>
        /// Clicks an element at a random position with a random delay, 
        /// avoiding the "dead center" bot signature.
        /// </summary>
        public static async Task HumanClickAsync(this ILocator locator)
        {
            // 1. "Thinking" pause
            await Task.Delay(_rng.Next(300, 1000));

            // 2. Get element size
            var box = await locator.BoundingBoxAsync();
            if (box == null)
            {
                await locator.ClickAsync();
                return;
            }

            // 3. Calculate random coordinates (Result is double)
            var width = box.Width;
            var height = box.Height;

            // We use (float) casting here to fix the error
            var randomX = (float)((width * 0.1) + (_rng.NextDouble() * (width * 0.8)));
            var randomY = (float)((height * 0.1) + (_rng.NextDouble() * (height * 0.8)));

            // 4. Click
            await locator.ClickAsync(new LocatorClickOptions
            {
                Position = new Position { X = randomX, Y = randomY }
            });
        }
        /// <summary>
        /// Types text one character at a time with variable speeds (like a human).
        /// </summary>
        public static async Task HumanFillAsync(this ILocator locator, string text)
        {
            // Focus the element first with a human click
            await locator.HumanClickAsync();

            // Clear existing text manually if needed, or rely on logic
            await locator.ClearAsync();

            foreach (char c in text)
            {
                // Type character
                await locator.TypeAsync(c.ToString(), new LocatorTypeOptions
                {
                    // Normal typing speed: 50ms - 150ms per key
                    Delay = _rng.Next(50, 150)
                });

                // 10% chance to "hesitate" or "stumble" (longer pause)
                if (_rng.NextDouble() < 0.1)
                {
                    await Task.Delay(_rng.Next(200, 500));
                }
            }
        }
    }
}
