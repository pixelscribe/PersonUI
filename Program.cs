using PersonUI.Components;
using PersonUI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var personApiBaseUrl = builder.Configuration["PersonApi:BaseUrl"]
    ?? throw new InvalidOperationException(
        "Missing configuration 'PersonApi:BaseUrl'. Set it in appsettings.json or the " +
        "PersonApi__BaseUrl environment variable.");

builder.Services.AddHttpClient<PersonApiClient>(client =>
{
    client.BaseAddress = new Uri(personApiBaseUrl);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
