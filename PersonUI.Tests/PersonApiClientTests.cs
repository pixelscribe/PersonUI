using System.Net;
using PersonUI.Models;
using PersonUI.Services;

namespace PersonUI.Tests;

public class PersonApiClientTests
{
    private static PersonApiClient CreateClient(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });

    private static readonly PersonFormModel SampleForm = new()
    {
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane.doe@example.com",
    };

    [Fact]
    public async Task GetAllAsync_ReturnsPeople()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK,
            """[{"id":1,"firstName":"Jane","lastName":"Doe","email":"jane.doe@example.com","createdAt":"2026-01-01T00:00:00"}]""");
        var client = CreateClient(handler);

        var people = await client.GetAllAsync();

        Assert.Single(people);
        Assert.Equal("Jane", people[0].FirstName);
    }

    [Fact]
    public async Task SearchAsync_EncodesQueryInRequestUri()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, "[]");
        var client = CreateClient(handler);

        await client.SearchAsync("jane doe");

        Assert.Equal("/api/person/search?q=jane%20doe", handler.LastRequest!.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsPerson_WhenFound()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK,
            """{"id":1,"firstName":"Jane","lastName":"Doe","email":"jane.doe@example.com","createdAt":"2026-01-01T00:00:00"}""");
        var client = CreateClient(handler);

        var person = await client.GetByIdAsync(1);

        Assert.NotNull(person);
        Assert.Equal(1ul, person!.Id);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
    {
        var handler = FakeHttpMessageHandler.Empty(HttpStatusCode.NotFound);
        var client = CreateClient(handler);

        var person = await client.GetByIdAsync(999);

        Assert.Null(person);
    }

    [Fact]
    public async Task GetByIdAsync_Throws_OnServerError()
    {
        var handler = FakeHttpMessageHandler.Empty(HttpStatusCode.InternalServerError);
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetByIdAsync(1));
    }

    [Fact]
    public async Task CreateAsync_ReturnsOk_WhenSuccessful()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.Created,
            """{"id":1,"firstName":"Jane","lastName":"Doe","email":"jane.doe@example.com","createdAt":"2026-01-01T00:00:00"}""");
        var client = CreateClient(handler);

        var result = await client.CreateAsync(SampleForm);

        Assert.True(result.Success);
        Assert.Equal("Jane", result.Data!.FirstName);
    }

    [Fact]
    public async Task CreateAsync_ReturnsJoinedValidationMessages_OnBadRequest()
    {
        var handler = FakeHttpMessageHandler.Json(HttpStatusCode.BadRequest,
            """{"errors":{"Email":["Enter a valid email address."],"FirstName":["First name is required."]}}""");
        var client = CreateClient(handler);

        var result = await client.CreateAsync(SampleForm);

        Assert.False(result.Success);
        Assert.Contains("Enter a valid email address.", result.Error);
        Assert.Contains("First name is required.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_ReturnsPlainTextMessage_OnConflict()
    {
        var handler = FakeHttpMessageHandler.PlainText(HttpStatusCode.Conflict,
            "\"A person with email 'jane.doe@example.com' already exists.\"");
        var client = CreateClient(handler);

        var result = await client.CreateAsync(SampleForm);

        Assert.False(result.Success);
        Assert.Equal("A person with email 'jane.doe@example.com' already exists.", result.Error);
    }

    [Fact]
    public async Task CreateAsync_FallsBackToStatusMessage_OnEmptyErrorBody()
    {
        var handler = FakeHttpMessageHandler.Empty(HttpStatusCode.BadRequest);
        var client = CreateClient(handler);

        var result = await client.CreateAsync(SampleForm);

        Assert.False(result.Success);
        Assert.Contains("400", result.Error);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsOk_WhenSuccessful()
    {
        var handler = FakeHttpMessageHandler.Empty(HttpStatusCode.NoContent);
        var client = CreateClient(handler);

        var result = await client.UpdateAsync(1, SampleForm);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsFail_WhenNotFound()
    {
        var handler = FakeHttpMessageHandler.Empty(HttpStatusCode.NotFound);
        var client = CreateClient(handler);

        var result = await client.UpdateAsync(999, SampleForm);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsOk_WhenSuccessful()
    {
        var handler = FakeHttpMessageHandler.Empty(HttpStatusCode.NoContent);
        var client = CreateClient(handler);

        var result = await client.DeleteAsync(1);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFail_WhenNotFound()
    {
        var handler = FakeHttpMessageHandler.Empty(HttpStatusCode.NotFound);
        var client = CreateClient(handler);

        var result = await client.DeleteAsync(999);

        Assert.False(result.Success);
    }
}
