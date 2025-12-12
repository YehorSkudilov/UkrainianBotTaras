using Microsoft.Playwright;
using System;
using System.Linq;
using System.Security.Policy;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

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

            while (true)
            {
                try
                {
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                    if (await page.GetByText("Вхід на Портал е-Консул").IsVisibleAsync() || await page.GetByRole(AriaRole.Link, new() { Name = "Запис на візит" }).IsVisibleAsync())
                    {
                        if (await page.GetByText("Вхід на Портал е-Консул").IsVisibleAsync())
                        {
                            await Click("Особистий ключ");
                            await UploadFile("Оберіть ключ на своєму носієві", ctx.KeyFilePath);
                            await FillInput("#id-app-login-sign-form-file-key-password", ctx.KeyPassword);
                            await Click("Продовжити");
                        }
                        
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
                        await page.WaitForTimeoutAsync(3000);
                        await FillSmartDropdown("#service", ctx.ServiceName);
                        await ClickCheckboxOnLeft("Для себе");
                    }

                    await Click("Далі");
                    await WaitForElementToAppear("Відбувається пошук активних слотів", 10000);
                    await WaitForElementToDisapear("Відбувається пошук активних слотів", 120000);

                    await page.WaitForTimeoutAsync(3000);

                    if (!await page.GetByText("На жаль, зараз немає вільного часу. Спробуйте пізніше або зробіть запис у чергу очікування.").IsVisibleAsync())
                    {
                        await ScreenShot(@"D:\GitHub\UkrainianBotTaras\UkrainianBotTaras\UkrainianBotTaras\Data\SS");
                        await DownloadPage(@"D:\GitHub\UkrainianBotTaras\UkrainianBotTaras\UkrainianBotTaras\Data\DP");
                    }
                    else
                    {
                        await ScreenShot(@"D:\GitHub\UkrainianBotTaras\UkrainianBotTaras\UkrainianBotTaras\Data\Garbage\SS");
                        await DownloadPage(@"D:\GitHub\UkrainianBotTaras\UkrainianBotTaras\UkrainianBotTaras\Data\Garbage\DP");
                    }
                    await page.ReloadAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\n❌ Error: {ex.Message}");
                    Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                    await OpenURL(ctx.BaseUrl);
                }
            }
        }

        // ----------------------------
        // Reusable Rules
        // ----------------------------

        private async Task OpenURL(string url)
        {
            Console.WriteLine($"Opening url: {url}...");
            await page.GotoAsync(url);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        }

        private async Task WaitForElementToDisapear(string text, int maxWaitInMS)
        {
            Console.WriteLine($"Checking if disapeared up to {maxWaitInMS}MS: {text}...");
            var element = page.GetByText(text);
            await element.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = maxWaitInMS });
        }

        private async Task WaitForElementToAppear(string text, int maxWaitInMS)
        {
            Console.WriteLine($"Checking if appeared up to {maxWaitInMS}MS: {text}...");
            var element = page.GetByText(text);
            await element.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = maxWaitInMS });
        }

        private async Task DownloadPage(string directoryPath)
        {
            Console.WriteLine($"Downloading page HTML to: {directoryPath}...");

            // 1. Get the current DOM state (HTML)
            string content = await page.ContentAsync();

            // 2. Ensure directory exists
            System.IO.Directory.CreateDirectory(directoryPath);

            // 3. Generate Unique Filename
            string baseName = "page_source";
            string extension = ".html";
            string finalPath = System.IO.Path.Combine(directoryPath, $"{baseName}{extension}");
            int counter = 1;

            while (System.IO.File.Exists(finalPath))
            {
                finalPath = System.IO.Path.Combine(directoryPath, $"{baseName}_{counter}{extension}");
                counter++;
            }

            // 4. Write to file
            await System.IO.File.WriteAllTextAsync(finalPath, content);

            Console.WriteLine($"✅ Page source saved to: {finalPath}");
        }

        private async Task ScreenShot(string directoryPath)
        {
            // --- FILE HANDLING ---

            // 1. Ensure the directory exists
            System.IO.Directory.CreateDirectory(directoryPath);

            // 2. Define base filename
            string baseName = "slots_result";
            string extension = ".png";
            string finalPath = System.IO.Path.Combine(directoryPath, $"{baseName}{extension}");
            int counter = 1;

            // 3. Loop until we find a unique name inside that directory
            while (System.IO.File.Exists(finalPath))
            {
                finalPath = System.IO.Path.Combine(directoryPath, $"{baseName}_{counter}{extension}");
                counter++;
            }

            // 4. Take Screenshot
            Console.WriteLine($"Taking Screenshot to: {finalPath}");

            await page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = finalPath,
                FullPage = true
            });

            Console.WriteLine("✅ Screenshot saved.");
        }
        private async Task Click(string searchText)
        {
            Console.WriteLine($"Clicking: {searchText}...");
            var frame = await GetActiveFrame();
            await frame.GetByText(searchText).HumanClickAsync();
        }

        private async Task ClickCheckboxOnLeft(string labelText)
        {
            Console.WriteLine($"Clicking checkbox for: '{labelText}'...");
            var frame = await GetActiveFrame();

            // Strategy: Find the <label> container that has the text, then find the checkbox inside it.
            // This is safer than :left-of because they are grouped together in the HTML.
            var labelContainer = frame.Locator("label").Filter(new() { HasText = labelText }).First;

            // Find the visual checkbox span inside that label
            var checkbox = labelContainer.Locator(".MuiCheckbox-root");

            // Scroll it into view and click
            await checkbox.ScrollIntoViewIfNeededAsync();
            await checkbox.HumanClickAsync(page);
        }

        private async Task UploadFile(string searchText, string filePath)
        {
            Console.WriteLine($"Uploading key file {filePath} to {searchText}...");
            var frame = await GetActiveFrame();

            var fileChooserTask = page.WaitForFileChooserAsync();

            await frame.GetByText(searchText).HumanClickAsync();

            var fileChooser = await fileChooserTask;
            await fileChooser.SetFilesAsync(filePath);
        }

        private async Task FillInput(string searchText, string input)
        {
            Console.WriteLine($"Filling Input {searchText} with {input}");
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
            Console.WriteLine($"Filling Input: {text} with {value}");
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