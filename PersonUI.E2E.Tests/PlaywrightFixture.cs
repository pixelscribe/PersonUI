using Microsoft.Playwright;

namespace PersonUI.E2E.Tests;

public class PlaywrightFixture : IAsyncLifetime
{
    private IPlaywright? _playwright;

    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        await WarmUpAsync();
    }

    // apply-webui.yml's smoke test only hits "/" before handing off to these
    // tests, so on a freshly booted instance "/" is JIT-warm but other routes
    // (like /people/create) are not. Observed a click on the Create button
    // there time out at Playwright's full 30s default on a cold instance -
    // visiting the routes these tests actually use once, up front, means the
    // first real test doesn't pay that cold-start cost inside its own
    // (shorter) per-action timeouts.
    private async Task WarmUpAsync()
    {
        var baseUrl = Environment.GetEnvironmentVariable("WEBUI_BASE_URL")?.TrimEnd('/');
        if (baseUrl is null)
        {
            return;
        }

        IBrowserContext? context = null;
        try
        {
            context = await Browser.NewContextAsync();
            var page = await context.NewPageAsync();

            foreach (var path in new[] { "/", "/people/create" })
            {
                await page.GotoAsync($"{baseUrl}{path}", new PageGotoOptions { Timeout = 60000 });
                await page.WaitForFunctionAsync("() => window.Blazor !== undefined", new PageWaitForFunctionOptions { Timeout = 60000 });
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Warmup navigation failed (non-fatal): {ex.Message}");
        }
        finally
        {
            if (context is not null)
            {
                await context.CloseAsync();
            }
        }
    }

    public async Task DisposeAsync()
    {
        // Final safety net, not a substitute for each test's own cleanup:
        // every person these tests create uses "Playwright" as the first
        // name, so search for that and delete whatever's left. Individual
        // tests can still fail to clean up after themselves in edge cases
        // (a failed assertion mid-flow, a retry that succeeds server-side
        // but isn't detected in time) - this catches those before they
        // accumulate and start affecting FULLTEXT search relevance for
        // later runs, which is what happened without it.
        await SweepLeftoverTestDataAsync();

        await Browser.CloseAsync();
        _playwright?.Dispose();
    }

    private async Task SweepLeftoverTestDataAsync()
    {
        var baseUrl = Environment.GetEnvironmentVariable("WEBUI_BASE_URL")?.TrimEnd('/');
        if (baseUrl is null)
        {
            return;
        }

        IBrowserContext? context = null;
        try
        {
            context = await Browser.NewContextAsync();
            var page = await context.NewPageAsync();
            page.Dialog += async (_, dialog) => await dialog.AcceptAsync();

            await page.GotoAsync($"{baseUrl}/");
            await page.WaitForFunctionAsync("() => window.Blazor !== undefined");
            await page.WaitForTimeoutAsync(500);

            var searchBox = page.Locator("input[type='search']");
            await searchBox.FillAsync("Playwright");
            await searchBox.DispatchEventAsync("keyup");
            await page.WaitForTimeoutAsync(1000);

            var deleteButtons = page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Delete" });
            for (var i = 0; i < 25 && await deleteButtons.CountAsync() > 0; i++)
            {
                await deleteButtons.First.ClickAsync();
                await page.WaitForTimeoutAsync(500);
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Leftover-data sweep failed (non-fatal): {ex.Message}");
        }
        finally
        {
            if (context is not null)
            {
                await context.CloseAsync();
            }
        }
    }
}

[CollectionDefinition(Name)]
public class PlaywrightCollection : ICollectionFixture<PlaywrightFixture>
{
    public const string Name = "Playwright";
}
