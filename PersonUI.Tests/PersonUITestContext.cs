using Bunit;
using Microsoft.Extensions.DependencyInjection;
using PersonUI.Services;

namespace PersonUI.Tests;

public abstract class PersonUITestContext : BunitContext
{
    protected PersonUITestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    protected PersonApiClient RegisterApiClient(FakeHttpMessageHandler handler)
    {
        var client = new PersonApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") });
        Services.AddSingleton(client);
        return client;
    }
}
