using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using PersonUI.Components.Pages;

namespace PersonUI.Tests;

public class PersonCreateTests : PersonUITestContext
{
    [Fact]
    public void Submit_CallsCreateAndNavigatesHome_WhenValid()
    {
        RegisterApiClient(FakeHttpMessageHandler.Json(HttpStatusCode.Created,
            """{"id":1,"firstName":"Jane","lastName":"Doe","email":"jane.doe@example.com","createdAt":"2026-01-01T00:00:00"}"""));
        var cut = Render<PersonCreate>();

        FillForm(cut, "Jane", "Doe", "jane.doe@example.com");
        cut.Find("form").Submit();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Equal("http://localhost/", nav.Uri);
    }

    [Fact]
    public void Submit_ShowsServerError_WhenApiReturnsConflict()
    {
        RegisterApiClient(FakeHttpMessageHandler.PlainText(HttpStatusCode.Conflict,
            "\"A person with email 'jane.doe@example.com' already exists.\""));
        var cut = Render<PersonCreate>();

        FillForm(cut, "Jane", "Doe", "jane.doe@example.com");
        cut.Find("form").Submit();

        Assert.Contains("already exists", cut.Markup);
    }

    [Fact]
    public void Submit_DoesNotCallApi_WhenFieldsAreEmpty()
    {
        var handlerCalled = false;
        RegisterApiClient(new FakeHttpMessageHandler(_ =>
        {
            handlerCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        var cut = Render<PersonCreate>();

        cut.Find("form").Submit();

        Assert.False(handlerCalled);
        Assert.Contains("required", cut.Markup, StringComparison.OrdinalIgnoreCase);
    }

    private static void FillForm(IRenderedComponent<PersonCreate> cut, string firstName, string lastName, string email)
    {
        // Each Change() re-renders the component, which invalidates event handler IDs on
        // elements found before it - re-query fresh before each interaction rather than
        // reusing one FindAll("input") result across multiple Change() calls.
        cut.FindAll("input")[0].Change(firstName);
        cut.FindAll("input")[1].Change(lastName);
        cut.FindAll("input")[2].Change(email);
    }
}
