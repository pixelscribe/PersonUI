namespace PersonUI.E2E.Tests;

// Validation is already covered in-process by PersonUI.Tests, but this
// confirms it also renders correctly through a real browser/circuit, not
// just bUnit's fake JS interop. Creates nothing, so no cleanup needed.
public class PersonValidationTests(PlaywrightFixture fixture) : PersonTestBase(fixture)
{
    [Fact]
    public async Task Create_ShowsValidationErrors_WhenFieldsEmpty()
    {
        await Page.GotoAsync($"{BaseUrl}/people/create");
        await WaitForInteractiveAsync();

        await SubmitCreateFormUntilAsync(Page.GetByText("First name is required."));

        await ExpectVisibleAsync(Page.GetByText("Last name is required."));
        await ExpectVisibleAsync(Page.GetByText("Email is required."));
    }
}
