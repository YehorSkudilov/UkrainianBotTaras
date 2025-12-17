using Microsoft.Playwright;
using System;
using System.Globalization;
using System.Linq;
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace UkrainianBotTaras.Src
{
    internal class Steps : ISteps
    {
        private readonly StepsContext ctx;
        private IPage page; // Class-level variable (no underscore)
        private int failedInARow = 0;
        static string keyPath;
        static string screenshotsDir;
        static string downloadedPagesDir;
        public Steps(StepsContext ctx)
        {
            this.ctx = ctx;
        }

        // ENTRY POINT
        public async Task RunAsync(IPage page)
        {
            (keyPath, screenshotsDir, downloadedPagesDir) =
    EnsureDataDirectories(ctx);

            if (!File.Exists(keyPath))
            {
                throw new FileNotFoundException(
                    $"Key file not found: {keyPath}",
                    keyPath
                );
            }

            this.page = page; // Assign to the class field
            try
            {
                await OpenURL(ctx.BaseUrl);
            }
            catch (Exception ex)
            {
                failedInARow++;
                Console.WriteLine($"\n❌ Error: {ex.Message}");
                Console.WriteLine($"wStack Trace: {ex.StackTrace}");
            }
            while (failedInARow < 5)
            {
                try
                {
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                    if (await page.GetByText("Вхід на Портал е-Консул").IsVisibleAsync() || await page.GetByText("Запис на візит").First.IsVisibleAsync() && !await page.GetByText("Консульська послуга").First.IsVisibleAsync())
                    {
                        if (await page.GetByText("Вхід на Портал е-Консул").IsVisibleAsync())
                        {
                            await Click("Особистий ключ");
                            await UploadFile("Оберіть ключ на своєму носієві", keyPath);
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
                        Task taskFillSmartDropdown = FillSmartDropdown("Країна", ctx.Country);
                        try
                        {
                            await page.WaitForTimeoutAsync(5000);
                            await WaitForElementToAppear("Завантаження...", 20000);
                            await page.WaitForTimeoutAsync(5000);
                            await WaitForElementToDisapear("Завантаження...", 120000);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Завантаження... happened too fast");
                        }
                        await taskFillSmartDropdown;
                        Task taskFillSmartDropdown2 = FillSmartDropdown("Консульська установа", ctx.ConsulateName);
                        try
                        {
                            await page.WaitForTimeoutAsync(5000);
                            await WaitForElementToAppear("Завантаження...", 20000);
                            await page.WaitForTimeoutAsync(5000);
                            await WaitForElementToDisapear("Завантаження...", 120000);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Завантаження... happened too fast");
                        }
                        await taskFillSmartDropdown2;
                        await Click("Далі");
                        await page.WaitForTimeoutAsync(5000);
                        Task taskFillSmartDropdown3 = FillSmartDropdown("#service", ctx.ServiceName);
                        try
                        {
                            await page.WaitForTimeoutAsync(5000);
                            await WaitForElementToAppear("Завантаження...", 20000);
                            await page.WaitForTimeoutAsync(5000);
                            await WaitForElementToDisapear("Завантаження...", 120000);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("Завантаження... happened too fast");
                        }
                        await taskFillSmartDropdown3;
                        await ClickCheckboxOnLeft("Для себе");
                    }

                    await Click("Далі");
                    await WaitForElementToAppear("Відбувається пошук активних слотів", 10000);
                    await WaitForElementToDisapear("Відбувається пошук активних слотів", 120000);

                    await page.WaitForTimeoutAsync(3000);

                    if (await page.GetByText("На жаль, зараз немає вільного часу. Спробуйте пізніше або зробіть запис у чергу очікування.").IsVisibleAsync())
                    {
                        Console.WriteLine("На жаль, зараз немає вільного часу. Спробуйте пізніше або зробіть запис у чергу очікування.");
                    }
                    else
                    {
                        await ScreenShot(screenshotsDir);
                        await DownloadPage(downloadedPagesDir);
                        await Click("Місяць");
                        await ChooseBestSlotAndProceed();
                    }
                    await page.ReloadAsync();
                    failedInARow = 0;
                }
                catch (Exception ex)
                {
                    failedInARow++;
                    Console.WriteLine($"\n❌ Error: {ex.Message}");
                    Console.WriteLine($"wStack Trace: {ex.StackTrace}");
                    await OpenURL(ctx.BaseUrl);
                }
            }
        }

        static (string KeysDir, string ScreenshotsDir, string DownloadedPagesDir)
    EnsureDataDirectories(StepsContext ctx)
        {
            if (string.IsNullOrWhiteSpace(ctx.DataDirectory))
                throw new InvalidOperationException("DataDirectory is not set.");

            Directory.CreateDirectory(ctx.DataDirectory);

            string keysDir = Path.Combine(ctx.DataDirectory, "Keys");
            string keyPath = Path.Combine(ctx.DataDirectory, "Keys", ctx.KeyFileName);

            string screenshotsDir = Path.Combine(ctx.DataDirectory, "Screenshots");
            string downloadedPagesDir = Path.Combine(ctx.DataDirectory, "DownloadedPages");

            Directory.CreateDirectory(keysDir);
            Directory.CreateDirectory(screenshotsDir);
            Directory.CreateDirectory(downloadedPagesDir);

            return (keyPath, screenshotsDir, downloadedPagesDir);
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


        private async Task<ILocator?> FindFirstAvailableSlotInScope(ILocator scopeLocator)
        {
            Console.WriteLine("🕵️ Scanning for available time slots within the provided scope...");

            // Note: We are no longer calling GetActiveFrame() here, 
            // as the search starts from the 'scopeLocator' provided.

            // 1. Define a Regex to match time strings like "09:00" or "14:30"
            var timePattern = new Regex(@"\d{2}:\d{2}");

            // 2. Build the Locator chain, starting the search from the 'scopeLocator':
            //    A. Find all clickable buttons (divs with role='button')
            //    B. Exclude ones that are explicitly "booked" (aria-label contains 'заброньовано')
            //    C. Filter specifically for elements containing a Time Pattern (00:00)

            // The key change is calling Locator() on 'scopeLocator' instead of 'frame'
            var availableSlots = scopeLocator.Locator("div[role='button']:not([aria-label*='заброньовано'])")
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

            Console.WriteLine("⚠️ No available slots found in this scope.");
            return null;
        }

        private async Task ChooseBestSlotAndProceed()
        {
            ILocator? bestSlot = null;
            int index = 0;
            ILocator? dateEl = null;
            DateTime lastBookedDT;
            DateTime newBookedDT;
            do
            {
                ILocator monthEl = await GetMonthTabByIndexAndClick(index);


                bool pageHasAvailableSlots = await FindAndClickFirstAvailableWeeklySlot();

                if (pageHasAvailableSlots)
                {
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    await page.WaitForTimeoutAsync(3000);

                    List<ILocator> daysOfWeek = await GetAllDailyScheduleDivsByText();

                    if (daysOfWeek.Count > 0)
                    {
                        foreach (ILocator dayOfWeek in daysOfWeek)
                        {
                            if (bestSlot == null)
                            {
                                await ClickElementByTextInScope(dayOfWeek, "Показати ще");
                                bestSlot = await FindFirstAvailableSlotInScope(dayOfWeek);
                                if (bestSlot != null)
                                {
                                    dateEl = dayOfWeek;
                                }
                            }


                        }
                    }
                }
                index++;
            } while (bestSlot == null);

            newBookedDT = await ParseAppointmentDate(dateEl, bestSlot);

            lastBookedDT = await GetDateFromFile(ctx.DataDirectory);
            if (lastBookedDT < newBookedDT)
            {
                await Click(bestSlot);
                await SaveDateToFile(ctx.DataDirectory, newBookedDT);
                await Click("Підтвердити");

                await ScreenShot(screenshotsDir);
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                await ScreenShot(screenshotsDir);
                await page.WaitForTimeoutAsync(10000);
                if (await page.GetByText("Скасування попередньої дати візиту").IsVisibleAsync())
                {
                    await ClickCheckboxOnLeft("Так");
                    await Click("Підтверджую");
                }
                await ScreenShot(screenshotsDir);
                await WaitForElementToAppear("Дякуємо, ми обробляємо ваш запит", 120000);
                await ScreenShot(screenshotsDir);
            }
            else
            {
                Console.WriteLine("❌ No better slots found than previously booked.");
            }
        }

        public async Task<bool> ClickElementByTextInScope(ILocator scopeLocator, string searchText)
        {
            Console.WriteLine($"🔍 Searching for '{searchText}' within the provided scope...");

            // Playwright Locators are chainable: calling Locator() on an existing ILocator 
            // scopes the new search exclusively to elements contained within the parent.

            // Build a powerful locator focused only inside the scope:
            var targetElement = scopeLocator
                .Locator($"text={searchText}") // Find by text (e.g., "Далі" or "Підтвердити")
                .Or(scopeLocator.GetByRole(AriaRole.Button, new() { Name = searchText })) // Find by button role name
                .Or(scopeLocator.GetByText(searchText, new() { Exact = true })); // Find by exact text

            // Use .First to ensure only one match is returned, preventing a Strict Mode error.
            var singleTarget = targetElement.First;

            // Check if the element exists and is visible within the scope
            if (await singleTarget.IsVisibleAsync(new() { Timeout = 5000 }))
            {
                Console.WriteLine($"✅ Found '{searchText}' in scope. Clicking...");
                await singleTarget.HumanClickAsync(page);
                return true;
            }

            Console.WriteLine($"❌ Element containing text '{searchText}' not found within the scope.");
            return false;
        }

        public async Task<List<ILocator>> GetAllDailyScheduleDivsByText()
        {
            Console.WriteLine("🎯 Locating all daily schedule container divs using text as anchor...");
            var frame = await GetActiveFrame();

            // 1. Find all elements that contain a day name and date (e.g., "понеділок, 27.04.2026")
            // This is safer than relying on class names that might change.
            // Regex: Finds a day name followed by a comma, date, and year (in the future).
            var dayPattern = new System.Text.RegularExpressions.Regex(@"(понеділок|вівторок|середа|четвер|п’ятниця|субота|неділя),\s\d{2}\.\d{2}\.\d{4}");

            // The inner element is the <button> inside the .jss236 div.
            var dayButtons = frame.Locator("button").Filter(new() { HasTextRegex = dayPattern });

            // 2. Count them and prepare the list
            int count = await dayButtons.CountAsync();
            if (count == 0)
            {
                Console.WriteLine("⚠️ No daily schedule button elements found.");
                return new List<ILocator>();
            }

            Console.WriteLine($"Found {count} distinct daily headers.");

            var dailyContainers = new List<ILocator>();

            // 3. For each button, navigate up to the correct unclassed parent <div>
            for (int i = 0; i < count; i++)
            {
                var buttonLocator = dayButtons.Nth(i);
                string dayText = await buttonLocator.InnerTextAsync();

                // Use Playwright's XPath to go up: 
                // button -> parent (.jss236) -> parent (the full daily container <div>)
                var wholeDayContainer = buttonLocator.Locator("xpath=../..");

                dailyContainers.Add(wholeDayContainer);
                Console.WriteLine($"   -> Added container for: {dayText.Trim()}");
            }

            return dailyContainers;
        }

        public async Task<bool> FindAndClickFirstAvailableWeeklySlot()
        {
            var frame = await GetActiveFrame();
            Console.WriteLine("🔎 [SlotSearch] Scanning for weekly slots...");

            // 1. SPINNER GUARD: Wait for loader to disappear
            try
            {
                var loader = frame.GetByText("Відбувається пошук активних слотів");
                if (await loader.IsVisibleAsync(new() { Timeout = 3000 }))
                {
                    Console.WriteLine("⌛ [SlotSearch] Waiting for loader to finish...");
                    await loader.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 60000 });
                }
            }
            catch { }

            // 2. STABLE LOCATOR: Find elements by their structural relationship.
            // Instead of just Regex, we look for DIVs that have "ВІЛЬНИХ СЛОТІВ" inside them.
            // This is much harder to break than a date regex.
            var slotContainers = frame.Locator("div").Filter(new()
            {
                Has = frame.Locator("div").GetByText("ВІЛЬНИХ СЛОТІВ")
            });

            // 3. POLLING WAIT: Wait for at least one slot container to appear.
            try
            {
                await slotContainers.First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 12000 });
            }
            catch
            {
                Console.WriteLine("⚠️ [SlotSearch] No slot containers appeared after 12s.");
                return false;
            }

            int count = await slotContainers.CountAsync();
            Console.WriteLine($"📊 [SlotSearch] Found {count} weeks to scan.");

            for (int i = 0; i < count; i++)
            {
                var slot = slotContainers.Nth(i);
                string rawText = await slot.InnerTextAsync();

                // Use your Parse logic
                int freeSlots = ParseAvailableSlotCount(rawText);

                // Robustly find the date line for logging even if Regex is finicky
                string dateLine = rawText.Split('\n')[0].Trim();
                Console.WriteLine($"   -> Week {i + 1}: [{dateLine}] | Found: {freeSlots}");

                if (freeSlots > 0)
                {
                    Console.WriteLine($"✅ [SlotSearch] SUCCESS! Clicking range: {dateLine}");

                    // ACTION: Scroll and Click with Force to bypass Mui overlays
                    await slot.ScrollIntoViewIfNeededAsync();
                    await slot.ClickAsync(new() { Force = true });

                    // Final verification: Wait a moment for the Daily view to start loading
                    await page.WaitForTimeoutAsync(2000);
                    return true;
                }
            }

            Console.WriteLine("❌ [SlotSearch] All visible weeks are full (0 slots).");
            return false;
        }
        private int ParseAvailableSlotCount(string statusText)
        {
            // The format is typically "N ВІЛЬНИХ СЛОТІВ"
            // Use Regex to robustly capture the number
            var match = System.Text.RegularExpressions.Regex.Match(statusText, @"(\d+)\s+ВІЛЬНИХ СЛОТІВ");

            if (match.Success && int.TryParse(match.Groups[1].Value, out int count))
            {
                return count;
            }

            return 0; // Default to zero if parsing fails or text doesn't match the expected pattern
        }

        private async Task<DateTime> ParseAppointmentDate(ILocator dayContainer, ILocator slotEl)
        {
            // 1. Extract raw text from the ENTIRE DAY CONTAINER
            string dayContainerText = (await dayContainer.InnerTextAsync()).Trim();
            string slotTimeText = (await slotEl.InnerTextAsync()).Trim();

            Console.WriteLine($"📝 Parsing: Day Container Text='{dayContainerText.Substring(0, Math.Min(dayContainerText.Length, 40))}...', Slot='{slotTimeText}'");

            // 2. Parse the Date Part: Find the DD.MM.YYYY pattern within the container's text.
            var dateMatch = Regex.Match(dayContainerText, @"\d{2}\.\d{2}\.\d{4}");

            if (!dateMatch.Success)
            {
                throw new Exception($"Unable to parse date (DD.MM.YYYY) from day container text: {dayContainerText}");
            }
            DateTime datePart = DateTime.ParseExact(dateMatch.Value, "dd.MM.yyyy", CultureInfo.InvariantCulture);

            // 3. Parse the Time Part (unmodified)
            var timeMatch = Regex.Match(slotTimeText, @"(\d{2}):(\d{2})");
            if (!timeMatch.Success)
            {
                throw new Exception($"Unable to parse time from slot text: {slotTimeText}");
            }

            int hours = int.Parse(timeMatch.Groups[1].Value);
            int minutes = int.Parse(timeMatch.Groups[2].Value);

            // 4. Combine into one DateTime
            DateTime finalDateTime = new DateTime(datePart.Year, datePart.Month, datePart.Day, hours, minutes, 0);

            Console.WriteLine($"✅ Final Extracted DateTime: {finalDateTime}");
            return finalDateTime;
        }

        private async Task<ILocator> GetMonthTabByIndexAndClick(int index)
        {
            var frame = await GetActiveFrame();
            Console.WriteLine($"📅 [MonthSelector] Targeting index {index}...");

            // 1. Target the scroll container using the stable attribute
            var scrollContainer = frame.Locator("div[saveref='calendarScrollBarRef']");

            // 2. REFINE SELECTOR: Target only the immediate children that contain the year.
            // This prevents the '14 tabs' issue caused by counting nested divs.
            var monthTabs = scrollContainer.Locator("> div").Filter(new()
            {
                HasTextString = "202"
            });

            // 3. Wait for the UI to populate
            try
            {
                await monthTabs.First.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10000 });
            }
            catch
            {
                Console.WriteLine("❌ [MonthSelector] Error: No month tabs found in DOM after 10s.");
                throw new Exception("Calendar month tabs missing.");
            }

            int count = await monthTabs.CountAsync();
            Console.WriteLine($"🔎 [MonthSelector] Found {count} unique months available.");

            if (index < 0 || index >= count)
            {
                Console.WriteLine($"⚠️ [MonthSelector] Index {index} is out of bounds (0-{count - 1}).");
                return null;
            }

            var targetTab = monthTabs.Nth(index);
            var labelText = (await targetTab.InnerTextAsync()).Replace("\n", " ").Trim();

            // 4. INTERACTION: Ensure the element is scrolled into view and click programmatically
            // DispatchEvent bypasses CSS masks that often 'hide' these tabs from standard clicks.
            await targetTab.ScrollIntoViewIfNeededAsync();
            await targetTab.DispatchEventAsync("click");

            Console.WriteLine($"✅ [MonthSelector] Clicked month [{index}]: {labelText}");
            return targetTab;
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