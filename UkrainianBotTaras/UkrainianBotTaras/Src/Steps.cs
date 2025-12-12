using Microsoft.Playwright;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace UkrainianBotTaras.Src
{
    internal class Steps : ISteps
    {
        private readonly StepsContext ctx;
        private IPage page; // Class-level variable (no underscore)

        public Steps(StepsContext ctx)
        {
            this.ctx = ctx;
        }

        // ENTRY POINT
        public async Task RunAsync(IPage page)
        {
            this.page = page; // Assign to the class field

            await OpenURL(ctx.BaseUrl);
            await Click("Особистий ключ");
            await UploadFile("Оберіть ключ на своєму носієві", ctx.KeyFilePath);
            await FillInput("#id-app-login-sign-form-file-key-password", ctx.KeyPassword);
            await Click("Продовжити");
            await Click("Запис на візит");
            await Click("Записатись на візит");
            await FillInputComplex("день", ctx.DayOfBirth);
            await SelectAndEnterToDropdown("місяць", ctx.MonthOfBirth);
            await FillInputComplex("рік", ctx.YearOfBirth);
            await Click("Далі");
            await page.WaitForTimeoutAsync(3000);
            await FillSmartDropdown("Країна", ctx.Country);
            await page.WaitForTimeoutAsync(3000);
            await FillSmartDropdown("Консульська установа", ctx.ConsulateName);
            await Click("Далі");
            await FillSmartDropdown("Выберить послугу", ctx.ServiceName);
            await Click("Далі");

        }

        // ----------------------------
        // Reusable Rules
        // ----------------------------

        private async Task OpenURL(string url)
        {
            Console.WriteLine("Opening homepage...");
            await page.GotoAsync(url);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        private async Task Click(string searchText)
        {
            Console.WriteLine($"Clicking {searchText}...");
            var frame = await GetActiveFrame();
            await frame.GetByText(searchText).HumanClickAsync();
        }

        private async Task UploadFile(string searchText, string filePath)
        {
            Console.WriteLine("Uploading key file...");
            var frame = await GetActiveFrame();

            var fileChooserTask = page.WaitForFileChooserAsync();

            await frame.GetByText(searchText).HumanClickAsync();

            var fileChooser = await fileChooserTask;
            await fileChooser.SetFilesAsync(filePath);
        }

        private async Task FillInput(string searchText, string input)
        {
            Console.WriteLine("Filling Input");
            var frame = await GetActiveFrame();

            var Input = frame.Locator(searchText);
            await Input.HumanFillAsync(input);
        }

        private async Task FillSmartDropdown(string identifier, string value)
        {
            Console.WriteLine($"Smart Selecting: '{identifier}' -> '{value}'");
            var frame = await GetActiveFrame();
            ILocator element = null;

            // ---------------------------------------------------------
            // 1. FIND THE ELEMENT (Dynamic Search Strategy)
            // ---------------------------------------------------------
            try
            {
                var byQa = frame.Locator($"[data-qa='{identifier}']");
                var byId = frame.Locator($"#{identifier}");

                // Wait for either to be attached to DOM
                if (await byQa.CountAsync() > 0 || await byId.CountAsync() > 0)
                {
                    element = (await byQa.CountAsync() > 0) ? byQa.First : byId.First;
                }
                else
                {
                    // Fallback: Check visual label or nearby button
                    try { await frame.GetByText(identifier).First.WaitForAsync(new() { Timeout = 3000 }); } catch { }

                    var trigger = frame.Locator($"div[role='button']:near(:text('{identifier}'))").First;
                    if (await trigger.CountAsync() > 0) element = trigger;
                    else element = frame.GetByLabel(identifier).First;
                }
            }
            catch
            {
                Console.WriteLine($"Could not find '{identifier}' normally. Trying generic fallback...");
                element = frame.Locator(identifier).First;
            }

            if (element == null) throw new Exception($"Dropdown '{identifier}' not found.");

            // ---------------------------------------------------------
            // 2. INTERACT BASED ON TYPE
            // ---------------------------------------------------------
            var tagName = await element.EvaluateAsync<string>("el => el.tagName.toLowerCase()");

            if (tagName == "input")
            {
                // CASE A: Autocomplete Input (Country/Consulate) - TYPE TO FILTER
                Console.WriteLine("Detected Autocomplete Input. Typing to filter...");

                // 1. Click and Clear
                await element.ClickAsync();
                await element.ClearAsync();

                // 2. Type the value (This filters the virtual list so the option appears)
                // We type slowly to trigger the JS search events
                await element.TypeAsync(value, new LocatorTypeOptions { Delay = 100 });

                // 3. Wait for the listbox to appear on the MAIN PAGE (outside iframe)
                var listbox = page.Locator("ul[role='listbox']");
                try
                {
                    await listbox.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
                }
                catch
                {
                    Console.WriteLine("⚠️ Listbox didn't open. Clicking chevron as fallback...");
                    // Fallback: Click the arrow button if typing didn't open it
                    var parent = element.Locator("xpath=..");
                    var chevron = parent.Locator(".MuiAutocomplete-popupIndicator");
                    if (await chevron.IsVisibleAsync()) await chevron.ClickAsync();
                }
            }
            else
            {
                // CASE B: Standard Select Div (Month) - CLICK TO OPEN
                Console.WriteLine("Detected Standard Dropdown. Clicking...");
                await element.HumanClickAsync(page);
            }

            // ---------------------------------------------------------
            // 3. SELECT THE OPTION
            // ---------------------------------------------------------
            // Material UI options are usually attached to the main 'page' body (Portals)
            // We assume the list is now open and filtered (if it was an input)
            var optionList = page.Locator("li[role='option']");

            try
            {
                // Wait for at least one option to appear
                await optionList.First.WaitForAsync(new() { Timeout = 10000 });
            }
            catch
            {
                Console.WriteLine("❌ Options list never appeared.");
                throw new Exception($"Options list for '{identifier}' failed to load.");
            }

            // 4. Find the specific text
            // Exact match is preferred to avoid selecting "Great Britain" when searching "Great"
            var specificOption = optionList.GetByText(value, new() { Exact = true });

            if (!await specificOption.IsVisibleAsync())
            {
                Console.WriteLine($"Exact match for '{value}' not visible. Trying partial match...");
                specificOption = optionList.GetByText(value, new() { Exact = false });
            }

            // Wait for the specific option to be stable and click
            await specificOption.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });
            await specificOption.HumanClickAsync(page);
        }
        private async Task SelectAndEnterToDropdown(string labelOrQaId, string optionText)
        {
            Console.WriteLine($"Selecting '{optionText}' in dropdown '{labelOrQaId}'...");
            var frame = await GetActiveFrame();

            ILocator dropdownTrigger;

            dropdownTrigger = frame.Locator($"div[role='button']:near(:text('{labelOrQaId}'))").First;

            await dropdownTrigger.HumanClickAsync(page);

            await page.WaitForSelectorAsync("li[role='option']");
            var option = frame.Locator("li[role='option']").GetByText(optionText);

            if (!await option.IsVisibleAsync())
            {
                
                option = page.Locator("li[role='option']").GetByText(optionText);
            }

            await option.HumanClickAsync(page);
        }

        private async Task FillInputComplex(string text, string value)
        {
            Console.WriteLine($"Filling Input: {text}");
            IFrame frame = await GetActiveFrame();

            ILocator input;

            // 1. Try to find by the 'data-qa' attribute (Your HTML uses this!)
            // This turns "день" into "[data-qa='день']"
            var byDataQa = frame.Locator($"[data-qa='{text}']");

            if (await byDataQa.CountAsync() > 0)
            {
                input = byDataQa.First;
            }
            else
            {
                // 2. Fallback: Try to find by visual Label
                input = frame.GetByLabel(text).First;
            }

            // Now 'input' is valid, so BoundingBoxAsync won't timeout
            await input.HumanFillAsync(value, page);
        }

        private async Task<IFrame> GetActiveFrame()
        {
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var frame = page.MainFrame;
            if (frame == null) throw new Exception("Auth iframe not found");
            return frame;
        }
    }
}