using System.Net;
using System.Text;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PersonUI.Components.Pages;

namespace PersonUI.Tests;

public class PersonEditTests : PersonUITestContext
{
    private const string PersonJson =
        """{"id":1,"firstName":"Jane","lastName":"Doe","email":"jane.doe@example.com","createdAt":"2026-01-01T00:00:00"}""";

    [Fact]
    public void LoadsAndPopulatesForm_WhenPersonExists()
    {
        RegisterApiClient(FakeHttpMessageHandler.Json(HttpStatusCode.OK, PersonJson));

        var cut = Render<PersonEdit>(parameters => parameters.Add(p => p.Id, 1));

        var inputs = cut.FindAll("input");
        Assert.Equal("Jane", inputs[0].GetAttribute("value"));
        Assert.Equal("Doe", inputs[1].GetAttribute("value"));
    }

    [Fact]
    public void ShowsNotFound_WhenPersonDoesNotExist()
    {
        RegisterApiClient(FakeHttpMessageHandler.Empty(HttpStatusCode.NotFound));

        var cut = Render<PersonEdit>(parameters => parameters.Add(p => p.Id, 999));

        Assert.Contains("No person with id 999 was found", cut.Markup);
    }

    [Fact]
    public void Submit_CallsUpdateAndNavigatesHome_WhenValid()
    {
        RegisterApiClient(new FakeHttpMessageHandler(req => req.Method == HttpMethod.Get
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonBody(PersonJson) }
            : new HttpResponseMessage(HttpStatusCode.NoContent)));

        var cut = Render<PersonEdit>(parameters => parameters.Add(p => p.Id, 1));
        cut.Find("form").Submit();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Equal("http://localhost/", nav.Uri);
    }

    [Fact]
    public void Submit_ShowsServerError_WhenApiReturnsConflict()
    {
        RegisterApiClient(new FakeHttpMessageHandler(req => req.Method == HttpMethod.Get
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonBody(PersonJson) }
            : new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent("\"Email already in use.\"", Encoding.UTF8, "text/plain"),
            }));

        var cut = Render<PersonEdit>(parameters => parameters.Add(p => p.Id, 1));
        cut.Find("form").Submit();

        Assert.Contains("Email already in use.", cut.Markup);
    }

    private static StringContent JsonBody(string json) => new(json, Encoding.UTF8, "application/json");
}
