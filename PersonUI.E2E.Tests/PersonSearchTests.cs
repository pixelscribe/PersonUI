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
            await SearchUntilHiddenAsync(Page.Locator("input[type='search']"), matchLastName, RowByText(otherLastName));

            await ExpectVisibleAsync(RowByText(matchLastName));
        }
        finally
        {
            await TryDeletePersonAsync(matchLastName);
            await TryDeletePersonAsync(otherLastName);
        }
    }
}
