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
    }

    public async Task DisposeAsync()
    {
        await Browser.CloseAsync();
        _playwright?.Dispose();
    }
}

[CollectionDefinition(Name)]
public class PlaywrightCollection : ICollectionFixture<PlaywrightFixture>
{
    public const string Name = "Playwright";
}
