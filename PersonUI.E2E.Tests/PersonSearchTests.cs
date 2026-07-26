namespace PersonUI.E2E.Tests;

public class PersonSearchTests(PlaywrightFixture fixture) : PersonTestBase(fixture)
{
    [Fact]
    public async Task Search_FiltersToMatchingPerson()
    {
        // Independent random suffixes, not shared between the two names: MySQL's
        // FULLTEXT tokenizer splits on hyphens, so a shared suffix would make
        // "Findme-<x>" and "Other-<x>" both match a search for either, as false
        // positives via the common "<x>" token rather than a real bug.
        var matchLastName = $"Findme-{Guid.NewGuid():N}"[..16];
        var otherLastName = $"Other-{Guid.NewGuid():N}"[..14];

        try
        {
            await CreatePersonAsync("Playwright", matchLastName, $"findme-{Guid.NewGuid():N}@example.com");
            await CreatePersonAsync("Playwright", otherLastName, $"other-{Guid.NewGuid():N}@example.com");

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
            await TryDeletePersonAsync(matchLastName);
            await TryDeletePersonAsync(otherLastName);
        }
    }
}
