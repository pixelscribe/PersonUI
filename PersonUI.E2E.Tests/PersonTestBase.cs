using Microsoft.Playwright;

namespace PersonUI.E2E.Tests;

file static class AssertionTimeout
{
    // Generous timeout for round trips through Blazor Server's SignalR circuit
    // to a real t3.micro instance, well above Playwright's 5s default.
    public const float Ms = 15000;
}

[Collection(PlaywrightCollection.Name)]
public abstract class PersonTestBase : IAsyncLifetime
{
    protected static readonly string BaseUrl = (Environment.GetEnvironmentVariable("WEBUI_BASE_URL")
        ?? throw new InvalidOperationException("WEBUI_BASE_URL environment variable is required.")).TrimEnd('/');

    private readonly PlaywrightFixture _fixture;
    private IBrowserContext _context = null!;
    protected IPage Page { get; private set; } = null!;

    // Defaults to accepting confirm() dialogs (e.g. the delete prompt); flip to
    // false immediately before an action you want to cancel, then flip back.
    protected bool AcceptNextDialog = true;

    protected PersonTestBase(PlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _context = await _fixture.Browser.NewContextAsync();
        Page = await _context.NewPageAsync();
        Page.Dialog += async (_, dialog) =>
        {
            if (AcceptNextDialog)
            {
                await dialog.AcceptAsync();
            }
            else
            {
                await dialog.DismissAsync();
            }
        };
        // Surfaced in CI logs on failure - a client-side JS error is otherwise
        // invisible from the .NET test output.
        Page.PageError += (_, err) => System.Console.WriteLine($"Browser page error: {err}");
    }

    public async Task DisposeAsync()
    {
        await _context.CloseAsync();
    }

    // Blazor Server prerenders static, non-interactive HTML first and only wires
    // up @bind-Value/event handlers once the SignalR circuit attaches a moment
    // later. Interacting with inputs before that attach edits the DOM but never
    // reaches the server-side model, so wait for window.Blazor plus a short
    // settle time before touching anything on a freshly loaded page.
    protected async Task WaitForInteractiveAsync()
    {
        await Page.WaitForFunctionAsync("() => window.Blazor !== undefined");
        await Page.WaitForTimeoutAsync(500);
    }

    protected async Task GoHomeAsync()
    {
        await Page.GotoAsync($"{BaseUrl}/");
        await WaitForInteractiveAsync();
    }

    protected ILocator RowByText(string text) =>
        Page.Locator("tr", new PageLocatorOptions { HasText = text });

    protected async Task ExpectVisibleAsync(ILocator locator) =>
        await Assertions.Expect(locator).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = AssertionTimeout.Ms });

    protected async Task ExpectHiddenAsync(ILocator locator) =>
        await Assertions.Expect(locator).Not.ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = AssertionTimeout.Ms });

    // Even after WaitForInteractiveAsync's check + settle buffer, the circuit
    // has occasionally not been fully attached to a specific element's event
    // handler yet when running on GitHub's runners (not reproducible locally -
    // environment-speed dependent). Retrying the click itself is robust to
    // this regardless of how slow a given run is, unlike guessing a fixed
    // delay. Safe to retry: a click that actually landed but wasn't detected
    // in time just means the next attempt's click() throws (element already
    // gone/changed) or is a harmless no-op, and the expectation check below
    // catches success either way.
    //
    // Catches Exception, not PlaywrightException: a click that can't find/act
    // on its target within its own action timeout throws System.TimeoutException,
    // not PlaywrightException (only Assertions.Expect timeouts do) - missing
    // this meant retries silently never engaged for that failure mode.
    protected async Task ClickUntilAsync(Func<Task> click, Func<Task> checkExpectation, int maxAttempts = 4)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await click();
            }
            catch (Exception) when (attempt < maxAttempts)
            {
            }

            try
            {
                await checkExpectation();
                return;
            }
            catch (Exception) when (attempt < maxAttempts)
            {
            }
        }
    }

    protected Task ClickUntilVisibleAsync(Func<Task> click, ILocator expectation) =>
        ClickUntilAsync(click, () => Assertions.Expect(expectation).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 3000 }));

    protected Task ClickUntilHiddenAsync(Func<Task> click, ILocator expectation) =>
        ClickUntilAsync(click, () => Assertions.Expect(expectation).Not.ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 3000 }));

    // Same rationale as ClickUntilAsync, but for the search box, whose
    // @oninput/@onkeyup handler might not be wired yet - retries rather than
    // assuming one attempt landed. Uses Fill (atomic, one input event) plus a
    // single manually-dispatched keyup rather than typing character-by-
    // character: the search box is one-way bound (value="@searchTerm" with
    // manual handlers, not @bind-Value), so a server re-render after the
    // debounced search completes forcibly resets the DOM value to match
    // server state - if that lands mid-keystroke, it corrupts whatever's
    // been typed so far. Setting the value in one shot avoids that window.
    protected async Task SearchUntilHiddenAsync(ILocator searchBox, string searchText, ILocator expectation, int maxAttempts = 4)
    {
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            await searchBox.FillAsync(searchText);
            await searchBox.DispatchEventAsync("keyup");
            try
            {
                await Assertions.Expect(expectation).Not.ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 3000 });
                return;
            }
            catch (PlaywrightException) when (attempt < maxAttempts)
            {
            }
        }
    }

    // Navigates to the create page and fills the form, but does not submit -
    // callers submit via SubmitCreateFormUntilAsync with whatever expectation
    // fits (a new row, a validation message, a conflict error).
    protected async Task FillCreateFormAsync(string firstName, string lastName, string email)
    {
        await Page.GotoAsync($"{BaseUrl}/people/create");
        await WaitForInteractiveAsync();

        var inputs = Page.Locator("form input:not([type='hidden'])");
        await inputs.Nth(0).FillAsync(firstName);
        await inputs.Nth(1).FillAsync(lastName);
        await inputs.Nth(2).FillAsync(email);
    }

    protected Task SubmitCreateFormUntilAsync(ILocator expectation) =>
        ClickUntilVisibleAsync(
            () => Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create" })
                .ClickAsync(new LocatorClickOptions { Timeout = 8000 }),
            expectation);

    // Full create flow, asserting the person actually appears on the home page.
    protected async Task<ILocator> CreatePersonAsync(string firstName, string lastName, string email)
    {
        await FillCreateFormAsync(firstName, lastName, email);

        var row = RowByText(lastName);
        await SubmitCreateFormUntilAsync(row);
        return row;
    }

    // Deletes via the row's Delete button, accepting the confirm() dialog, and
    // waits for the row to disappear. Always navigates home first (resetting
    // any search filter) so it works as a best-effort cleanup regardless of
    // which page a test's own assertions left the browser on, including after
    // a failed assertion.
    protected async Task DeletePersonAsync(string lastName)
    {
        AcceptNextDialog = true;
        await GoHomeAsync();

        var row = RowByText(lastName);
        if (await row.CountAsync() == 0)
        {
            return;
        }

        await ClickUntilHiddenAsync(
            () => row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete" })
                .ClickAsync(new LocatorClickOptions { Timeout = 8000 }),
            row);
    }

    // Swallows exceptions so that cleaning up one person can't prevent cleaning
    // up another in the same finally block, and so a cleanup failure doesn't
    // mask the original test failure. Always use this (never DeletePersonAsync
    // directly) from a finally block.
    protected async Task TryDeletePersonAsync(string lastName)
    {
        try
        {
            await DeletePersonAsync(lastName);
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Cleanup failed for '{lastName}' (may need manual removal): {ex.Message}");
        }
    }
}
