namespace PersonUI.E2E.Tests;

public class PersonSearchTests(PlaywrightFixture fixture) : PersonTestBase(fixture)
{
    [Fact]
    public async Task Search_FiltersToMatchingPerson()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var matchLastName = $"Findme-{unique}";
        var otherLastName = $"Other-{unique}";

        try
        {
            await CreatePersonAsync("Playwright", matchLastName, $"findme-{unique}@example.com");
            await CreatePersonAsync("Playwright", otherLastName, $"other-{unique}@example.com");

            await GoHomeAsync();
            // PressSequentially fires real key events, needed for the search
            // box's @onkeyup-driven (debounced) filtering - Fill only fires
            // input/change, which the search box also handles but this is
            // closer to how a real user triggers it.
            await Page.Locator("input[type='search']").PressSequentiallyAsync(matchLastName);

            await ExpectVisibleAsync(RowByText(matchLastName));
            await ExpectHiddenAsync(RowByText(otherLastName));
        }
        finally
        {
            await DeletePersonAsync(matchLastName);
            await DeletePersonAsync(otherLastName);
        }
    }
}
