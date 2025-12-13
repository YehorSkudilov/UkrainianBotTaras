using Microsoft.Playwright;
using System;
using System.Globalization;
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
        private int failedInARow = 0;
        public Steps(StepsContext ctx)
        {
            this.ctx = ctx;
        }

        // ENTRY POINT
        public async Task RunAsync(IPage page)
        {
            try
            {
                this.page = page; // Assign to the class field

                await OpenURL(ctx.BaseUrl);

                while (failedInARow < 5)
                {

                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                    if (await page.GetByText("Вхід на Портал е-Консул").IsVisibleAsync() || await page.GetByText("Запис на візит").First.IsVisibleAsync())
                    {
                        if (await page.GetByText("Вхід на Портал е-Консул").IsVisibleAsync())
                        {
                            await Click("Особистий ключ");
                            await UploadFile("Оберіть ключ на своєму носієві", ctx.KeyFilePath);
                            await FillInput("#id-app-login-sign-form-file-key-password", ctx.KeyPassword);
                            await Click("Продовжити");
                        }

                        await ClickExact("Запис на візит");
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

                    if (await page.GetByText("На жаль, зараз немає вільного часу. Спробуйте пізніше або зробіть запис у чергу очікування.").IsVisibleAsync())
                    {
                        await ScreenShot(@"D:\GitHub\UkrainianBotTaras\UkrainianBotTaras\UkrainianBotTaras\Data\SS");
                        await DownloadPage(@"D:\GitHub\UkrainianBotTaras\UkrainianBotTaras\UkrainianBotTaras\Data\DP");
                    }
                    else
                    {
                        await ScreenShot(@"D:\GitHub\UkrainianBotTaras\UkrainianBotTaras\UkrainianBotTaras\Data\Garbage\SS");
                        await DownloadPage(@"D:\GitHub\UkrainianBotTaras\UkrainianBotTaras\UkrainianBotTaras\Data\Garbage\DP");
                        await Click("День");
                        await ChooseBestSlotAndProceed();
                    }
                    await page.ReloadAsync();
                    failedInARow = 0;
                }
            }
            catch (Exception ex)
            {
                failedInARow++;
                Console.WriteLine($"\n❌ Error: {ex.Message}");
                Console.WriteLine($"wStack Trace: {ex.StackTrace}");
                await OpenURL(ctx.BaseUrl);
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

        private async Task<ILocator?> FindFirstAvailableSlot()
        {
            Console.WriteLine("🕵️ Scanning for available time slots...");
            var frame = await GetActiveFrame();

            // 1. Define a Regex to match time strings like "09:00" or "14:30"
            // This ensures we ONLY pick up time slots, not headers or alerts.
            var timePattern = new System.Text.RegularExpressions.Regex(@"\d{2}:\d{2}");

            // 2. Build the Locator chain:
            //    A. Find all clickable buttons (divs with role='button')
            //    B. Exclude ones that are explicitly "booked" (aria-label contains 'заброньовано')
            //    C. Filter specifically for elements containing a Time Pattern (00:00)
            var availableSlots = frame.Locator("div[role='button']:not([aria-label*='заброньовано'])")
                                      .Filter(new() { HasTextRegex = timePattern });

            // 3. Check if we found anything
            if (await availableSlots.CountAsync() > 0)
            {
                var firstSlot = availableSlots.First;

                // Log the success
                var timeText = await firstSlot.InnerTextAsync();
                Console.WriteLine($"✅ Available slot found: {timeText}");

                return firstSlot;
            }

            Console.WriteLine("⚠️ No available slots found.");
            return null;
        }

        private async Task ChooseBestSlotAndProceed()
        {
            ILocator ?bestSlot = null;
            int index = 0;
            ILocator dateEl;
            DateTime lastBookedDT = await GetDateFromFile(@"D:\GitHub\UkrainianBotTaras\UkrainianBotTaras\UkrainianBotTaras\Data");
            DateTime newBookedDT;
            do
            {
                dateEl = await GetDateTabByIndex(index);
                await dateEl.HumanClickAsync(page);


                bestSlot = await FindFirstAvailableSlot();
                index++;
            } while (bestSlot == null);

            newBookedDT =  await ParseAppointmentDate(dateEl, bestSlot);


            if (lastBookedDT < newBookedDT)
            {
                await Click(bestSlot);
                await SaveDateToFile(@"D:\GitHub\UkrainianBotTaras\UkrainianBotTaras\UkrainianBotTaras\Data", newBookedDT);
                await Click("Підтвердити");

                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                if (await page.GetByText("Скасування попередньої дати візиту").IsVisibleAsync())
                {
                    await ClickCheckboxOnLeft("Так");
                    await Click("Підтверджую");
                }
                await WaitForElementToAppear("Відбувається пошук активних слотів", 120000);
            }
            else
            {
                Console.WriteLine("❌ No better slots found than previously booked.");
            }
        }

        private async Task<DateTime> ParseAppointmentDate(ILocator dateEl, ILocator slotEl)
        {
            // 1. Extract the raw text from the elements
            // .Trim() removes accidental whitespace/newlines
            string dateText = (await dateEl.InnerTextAsync()).Trim(); // e.g. "15.12.2025"
            string slotText = (await slotEl.InnerTextAsync()).Trim(); // e.g. "09:00 - 09:10"

            Console.WriteLine($"📝 Parsing: Date='{dateText}', Slot='{slotText}'");

            // 2. Parse the Date Part (Strict format dd.MM.yyyy)
            DateTime datePart;
            try
            {
                datePart = DateTime.ParseExact(dateText, "dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                // Fallback: Sometimes the text might contain the day name (e.g. "Monday 15.12.2025")
                // This regex finds the date pattern inside the string just in case
                var match = System.Text.RegularExpressions.Regex.Match(dateText, @"\d{2}\.\d{2}\.\d{4}");
                if (match.Success)
                {
                    datePart = DateTime.ParseExact(match.Value, "dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture);
                }
                else throw new Exception($"Unable to parse date from text: {dateText}");
            }

            // 3. Parse the Time Part
            // We use Regex to grab the FIRST occurrence of "HH:mm".
            // This works perfectly for "09:00" OR "09:00 - 09:10".
            var timeMatch = System.Text.RegularExpressions.Regex.Match(slotText, @"(\d{2}):(\d{2})");

            if (!timeMatch.Success)
            {
                throw new Exception($"Unable to parse time from slot text: {slotText}");
            }

            int hours = int.Parse(timeMatch.Groups[1].Value);
            int minutes = int.Parse(timeMatch.Groups[2].Value);

            // 4. Combine into one DateTime
            DateTime finalDateTime = new DateTime(datePart.Year, datePart.Month, datePart.Day, hours, minutes, 0);

            Console.WriteLine($"✅ Final Extracted DateTime: {finalDateTime}");
            return finalDateTime;
        }

        private async Task<ILocator> GetDateTabByIndex(int index)
        {
            Console.WriteLine($"👉 Attempting to select date at index [{index}]...");
            var frame = await GetActiveFrame();

            // Regex to strictly match 'DD.MM.YYYY' (e.g. 15.12.2025)
            var datePattern = new System.Text.RegularExpressions.Regex(@"^\d{2}\.\d{2}\.\d{4}$");

            // Locate ALL elements matching the date pattern
            var dateElements = frame.GetByText(datePattern);

            // Get the total count of dates currently visible/loaded
            int count = await dateElements.CountAsync();

            // CHECK: Throw exception if index is invalid (Fails immediately)
            if (index < 0 || index >= count)
            {
                throw new IndexOutOfRangeException($"❌ Date index '{index}' is out of bounds. The calendar only has {count} dates available.");
            }

            // Select the specific date using .Nth()
            var targetDate = dateElements.Nth(index);

            // Ensure the element actually exists/is attached (Double check)
            if (await targetDate.CountAsync() == 0)
            {
                throw new Exception($"❌ Failed to retrieve date element at index {index}. Locator returned 0 elements.");
            }

            var dateText = await targetDate.InnerTextAsync();
            Console.WriteLine($"✅ Current date: {dateText}");

            await page.WaitForTimeoutAsync(1000);

            // Returns a guaranteed non-null ILocator
            return targetDate;
        }

        private async Task Click(ILocator locator)
        {
            // 1. Safety: Use .First to avoid "Strict Mode" errors if the locator matches multiple items
            var target = locator.First;

            // 2. Logging: Try to get the text for better logs, but default to "Element" if empty
            string logText = "Element";
            try
            {
                // Quick check for text content (timeout safety included in Playwright usually, but good to be aware)
                if (await target.IsVisibleAsync())
                {
                    var text = await target.InnerTextAsync();
                    if (!string.IsNullOrWhiteSpace(text)) logText = text.Trim();
                }
            }
            catch { /* Ignore logging errors */ }

            Console.WriteLine($"Clicking locator: '{logText}'...");

            // 3. Execute Click
            await target.HumanClickAsync(page);
        }

        private async Task SaveDateToFile(string directoryPath, DateTime date)
        {
            try
            {
                // 1. Combine directory with the fixed filename
                string finalPath = System.IO.Path.Combine(directoryPath, "lastBooked.txt");

                // 2. Ensure the directory exists
                if (!System.IO.Directory.Exists(directoryPath))
                {
                    System.IO.Directory.CreateDirectory(directoryPath);
                }

                // 3. Save as ISO 8601 string
                string dateString = date.ToString("O");
                await System.IO.File.WriteAllTextAsync(finalPath, dateString);

                Console.WriteLine($"💾 Saved new best slot to: {finalPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Failed to save date to file: {ex.Message}");
            }
        }

        private async Task<DateTime> GetDateFromFile(string directoryPath)
        {
            try
            {
                // 1. Construct the full path
                string finalPath = System.IO.Path.Combine(directoryPath, "lastBooked.txt");

                // 2. Check if file exists
                if (!System.IO.File.Exists(finalPath))
                {
                    Console.WriteLine($"📂 No 'lastBooked.txt' found in {directoryPath}. Starting fresh.");
                    return DateTime.MinValue;
                }

                // 3. Read and Parse
                string fileContent = await System.IO.File.ReadAllTextAsync(finalPath);

                if (string.IsNullOrWhiteSpace(fileContent)) return DateTime.MinValue;

                if (DateTime.TryParse(fileContent, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime savedDate))
                {
                    Console.WriteLine($"📂 Loaded previous booking: {savedDate}");
                    return savedDate;
                }
                else
                {
                    Console.WriteLine("⚠️ File content corrupt. Starting fresh.");
                    return DateTime.MinValue;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error reading save file: {ex.Message}");
                return DateTime.MinValue;
            }
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

            // 1. Standard Text Search (e.g., "Записатись на візит")
            var byText = frame.GetByText(searchText);

            // 2. Button Name Search (Handles Icon Buttons like "День" with aria-label="День")
            var byRole = frame.GetByRole(AriaRole.Button, new() { Name = searchText });

            // 3. Explicit Label Search (Handles inputs or labeled divs)
            var byLabel = frame.GetByLabel(searchText);

            // --- COMBINE THEM ---
            // This creates a "Super Locator" that matches if ANY of the above match
            var combinedLocator = byText.Or(byRole).Or(byLabel);

            // We use .First to ensure that if multiple matches exist, 
            // we simply click the first one instead of crashing with a "Strict Mode" error.
            await combinedLocator.First.HumanClickAsync();
        }

        private async Task ClickExact(string searchText)
        {
            Console.WriteLine($"Clicking: {searchText}...");
            var frame = await GetActiveFrame();

            // ✅ FIX: specific exact match ignores the long headings and PDFs
            var locator = frame.GetByText(searchText, new() { Exact = true });

            // Fallback: If exact match fails, try the first partial match
            if (await locator.CountAsync() == 0)
            {
                locator = frame.GetByText(searchText).First;
            }

            await locator.HumanClickAsync();
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