using Microsoft.Playwright;

namespace PersonUI.E2E.Tests;

file static class AssertionTimeout
{
    // Generous timeout for round trips through Blazor Server's SignalR circuit
    // to a real t3.micro instance, well above Playwright's 5s default.
    public const float Ms = 15000;
}

// Post-deploy browser test: drives the real deployed webui through a full
// create -> edit -> delete lifecycle via an actual browser (Blazor Server
// needs a real SignalR circuit, which bUnit's in-process rendering can't
// provide). Only run against a live instance from apply-webui.yml, not part
// of PersonUI's regular CI - there's no local multi-service stack to target.
[Collection(PlaywrightCollection.Name)]
public class PersonLifecycleTests : IAsyncLifetime
{
    private static readonly string BaseUrl = (Environment.GetEnvironmentVariable("WEBUI_BASE_URL")
        ?? throw new InvalidOperationException("WEBUI_BASE_URL environment variable is required.")).TrimEnd('/');

    private readonly PlaywrightFixture _fixture;
    private IBrowserContext _context = null!;
    private IPage _page = null!;

    public PersonLifecycleTests(PlaywrightFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _context = await _fixture.Browser.NewContextAsync();
        _page = await _context.NewPageAsync();
        _page.Dialog += async (_, dialog) => await dialog.AcceptAsync();
        // Surfaced in CI logs on failure - a client-side JS error is otherwise
        // invisible from the .NET test output.
        _page.PageError += (_, err) => System.Console.WriteLine($"Browser page error: {err}");
    }

    public async Task DisposeAsync()
    {
        await _context.CloseAsync();
    }

    [Fact]
    public async Task Create_Edit_Delete_FullLifecycle()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var lastName = $"E2E-{unique}";
        var updatedLastName = $"E2E-{unique}-updated";
        var email = $"e2e-{unique}@example.com";

        await CreatePersonAsync("Playwright", lastName, email);

        var row = _page.Locator("tr", new PageLocatorOptions { HasText = lastName });
        await Assertions.Expect(row).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = AssertionTimeout.Ms });

        await EditLastNameAsync(row, updatedLastName);

        var updatedRow = _page.Locator("tr", new PageLocatorOptions { HasText = updatedLastName });
        await Assertions.Expect(updatedRow).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = AssertionTimeout.Ms });

        await updatedRow.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete" }).ClickAsync();
        await Assertions.Expect(updatedRow).Not.ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = AssertionTimeout.Ms });
    }

    // Blazor Server prerenders static, non-interactive HTML first and only wires
    // up @bind-Value/event handlers once the SignalR circuit attaches a moment
    // later. Filling inputs before that attach edits the DOM but never reaches
    // the server-side model, so wait for window.Blazor plus a short settle time.
    private async Task WaitForInteractiveAsync()
    {
        await _page.WaitForFunctionAsync("() => window.Blazor !== undefined");
        await _page.WaitForTimeoutAsync(500);
    }

    private async Task CreatePersonAsync(string firstName, string lastName, string email)
    {
        await _page.GotoAsync($"{BaseUrl}/people/create");
        await WaitForInteractiveAsync();

        var inputs = _page.Locator("form input:not([type='hidden'])");
        await inputs.Nth(0).FillAsync(firstName);
        await inputs.Nth(1).FillAsync(lastName);
        await inputs.Nth(2).FillAsync(email);

        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Create" }).ClickAsync();

        // Blazor Server navigates client-side via the SignalR circuit; wait for
        // the new row rather than a URL change, which is the more reliable signal.
        await Assertions.Expect(_page.Locator("tr", new PageLocatorOptions { HasText = lastName }))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = AssertionTimeout.Ms });
    }

    private async Task EditLastNameAsync(ILocator row, string newLastName)
    {
        await row.GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Edit" }).ClickAsync();
        await WaitForInteractiveAsync();

        var inputs = _page.Locator("form input:not([type='hidden'])");
        await inputs.Nth(1).FillAsync(newLastName);
        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save" }).ClickAsync();

        await Assertions.Expect(_page.Locator("tr", new PageLocatorOptions { HasText = newLastName }))
            .ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = AssertionTimeout.Ms });
    }
}
