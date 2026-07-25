using System.Net;
using System.Text;
using Bunit;
using PersonUI.Components.Pages;

namespace PersonUI.Tests;

public class HomeTests : PersonUITestContext
{
    private const string OnePersonJson =
        """[{"id":1,"firstName":"Jane","lastName":"Doe","email":"jane.doe@example.com","createdAt":"2026-01-01T00:00:00"}]""";

    [Fact]
    public void ShowsPeople_WhenApiReturnsList()
    {
        RegisterApiClient(FakeHttpMessageHandler.Json(HttpStatusCode.OK, OnePersonJson));

        var cut = Render<Home>();

        Assert.Contains("Jane", cut.Markup);
    }

    [Fact]
    public void ShowsEmptyMessage_WhenNoPeople()
    {
        RegisterApiClient(FakeHttpMessageHandler.Json(HttpStatusCode.OK, "[]"));

        var cut = Render<Home>();

        Assert.Contains("No people found.", cut.Markup);
    }

    [Fact]
    public void ShowsErrorMessage_WhenApiUnreachable()
    {
        RegisterApiClient(new FakeHttpMessageHandler(_ => throw new HttpRequestException("connection refused")));

        var cut = Render<Home>();

        Assert.Contains("Couldn't reach PersonApi", cut.Markup);
    }

    [Fact]
    public void Delete_RemovesPerson_WhenConfirmed()
    {
        var deleteCalled = false;
        RegisterApiClient(new FakeHttpMessageHandler(req =>
        {
            if (req.Method == HttpMethod.Delete)
            {
                deleteCalled = true;
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            var body = deleteCalled ? "[]" : OnePersonJson;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }));
        // Match any invocation of "confirm" regardless of its arguments - the no-args
        // overload only matches zero-argument calls, but the real call passes a message.
        JSInterop.Setup<bool>("confirm", _ => true).SetResult(true);

        var cut = Render<Home>();
        cut.Find("button.btn-danger").Click();

        Assert.True(deleteCalled);
    }

    [Fact]
    public void Delete_DoesNothing_WhenNotConfirmed()
    {
        var deleteCalled = false;
        RegisterApiClient(new FakeHttpMessageHandler(req =>
        {
            if (req.Method == HttpMethod.Delete)
            {
                deleteCalled = true;
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(OnePersonJson, Encoding.UTF8, "application/json"),
            };
        }));
        // Loose JSInterop mode (set in PersonUITestContext) returns default(bool) = false
        // for unconfigured "confirm" calls, so no explicit setup needed for this case.

        var cut = Render<Home>();
        cut.Find("button.btn-danger").Click();

        Assert.False(deleteCalled);
    }
}
