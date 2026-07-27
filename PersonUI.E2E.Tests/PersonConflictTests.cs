namespace PersonUI.E2E.Tests;

// Confirms the 409 duplicate-email response from PersonApi renders correctly
// as a server error message in a real browser, not just bUnit's fake handler.
public class PersonConflictTests(PlaywrightFixture fixture) : PersonTestBase(fixture)
{
    [Fact]
    public async Task Create_ShowsConflictError_WhenEmailAlreadyExists()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var lastName = $"Dupe-{unique}";
        var email = $"dupe-{unique}@example.com";

        try
        {
            await CreatePersonAsync("Playwright", lastName, email);

            await FillCreateFormAsync("Playwright", $"Dupe2-{unique}", email);
            await SubmitCreateFormUntilAsync(Page.GetByText($"A person with email '{email}' already exists."));
        }
        finally
        {
            await TryDeletePersonAsync(lastName);
        }
    }
}
