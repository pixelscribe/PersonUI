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

    // Navigates to the create page, fills the form and submits, but does not
    // wait for or assert the outcome - callers check whatever they care about
    // (a new row, a validation message, a conflict error).
    protected async Task GoToCreateAndSubmitAsync(string firstName, string lastName, string email)
    {
        await Page.GotoAsync($"{BaseUrl}/people/create");
        await WaitForInteractiveAsync();

        var inputs = Page.Locator("form input:not([type='hidden'])");
        await inputs.Nth(0).FillAsync(firstName);
        await inputs.Nth(1).FillAsync(lastName);
        await inputs.Nth(2).FillAsync(email);

        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create" }).ClickAsync();
    }

    // Full create flow, asserting the person actually appears on the home page.
    protected async Task<ILocator> CreatePersonAsync(string firstName, string lastName, string email)
    {
        await GoToCreateAndSubmitAsync(firstName, lastName, email);

        var row = RowByText(lastName);
        await ExpectVisibleAsync(row);
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

        await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete" }).ClickAsync();
        await ExpectHiddenAsync(row);
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
