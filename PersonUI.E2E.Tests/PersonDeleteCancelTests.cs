using Microsoft.Playwright;

namespace PersonUI.E2E.Tests;

public class PersonDeleteCancelTests(PlaywrightFixture fixture) : PersonTestBase(fixture)
{
    [Fact]
    public async Task Delete_LeavesPersonIntact_WhenDialogDismissed()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var lastName = $"Keepme-{unique}";

        try
        {
            var row = await CreatePersonAsync("Playwright", lastName, $"keepme-{unique}@example.com");

            AcceptNextDialog = false;
            await row.GetByRole(AriaRole.Button, new LocatorGetByRoleOptions { Name = "Delete" }).ClickAsync();

            // No visible-state change to await on a dismissed dialog; give the
            // round trip a moment, then confirm the row is still there.
            await Page.WaitForTimeoutAsync(500);
            await ExpectVisibleAsync(row);
        }
        finally
        {
            await DeletePersonAsync(lastName);
        }
    }
}
