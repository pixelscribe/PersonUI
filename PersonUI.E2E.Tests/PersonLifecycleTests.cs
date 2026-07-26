using Microsoft.Playwright;

namespace PersonUI.E2E.Tests;

// Post-deploy browser test: drives the real deployed webui through a full
// create -> edit -> delete lifecycle via an actual browser (Blazor Server
// needs a real SignalR circuit, which bUnit's in-process rendering can't
// provide). Only run against a live instance from apply-webui.yml, not part
// of PersonUI's regular CI - there's no local multi-service stack to target.
public class PersonLifecycleTests(PlaywrightFixture fixture) : PersonTestBase(fixture)
{
    [Fact]
    public async Task Create_Edit_Delete_FullLifecycle()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var lastName = $"E2E-{unique}";
        var updatedLastName = $"E2E-{unique}-updated";
        var email = $"e2e-{unique}@example.com";

        try
        {
            var row = await CreatePersonAsync("Playwright", lastName, email);

            await EditLastNameAsync(row, updatedLastName);

            var updatedRow = RowByText(updatedLastName);
            await ExpectVisibleAsync(updatedRow);

            await updatedRow.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete" }).ClickAsync();
            await ExpectHiddenAsync(updatedRow);
        }
        finally
        {
            // Whichever name the person currently has if something failed partway.
            await TryDeletePersonAsync(updatedLastName);
            await TryDeletePersonAsync(lastName);
        }
    }

    private async Task EditLastNameAsync(ILocator row, string newLastName)
    {
        await row.GetByRole(AriaRole.Link, new LocatorGetByRoleOptions { Name = "Edit" }).ClickAsync();
        await WaitForInteractiveAsync();

        var inputs = Page.Locator("form input:not([type='hidden'])");
        await inputs.Nth(1).FillAsync(newLastName);
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save" }).ClickAsync();

        await ExpectVisibleAsync(RowByText(newLastName));
    }
}
