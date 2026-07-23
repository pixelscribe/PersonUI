using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PersonUI.Models;

namespace PersonUI.Services;

public class PersonApiClient(HttpClient http)
{
    public async Task<List<Person>> GetAllAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<Person>>("api/person", ct) ?? [];

    public async Task<List<Person>> SearchAsync(string query, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<Person>>($"api/person/search?q={Uri.EscapeDataString(query)}", ct) ?? [];

    public async Task<Person?> GetByIdAsync(ulong id, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"api/person/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Person>(cancellationToken: ct);
    }

    public async Task<ApiResult<Person>> CreateAsync(PersonFormModel request, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("api/person", request, ct);
        if (!response.IsSuccessStatusCode)
        {
            return ApiResult<Person>.Fail(await ExtractErrorAsync(response, ct));
        }

        var person = await response.Content.ReadFromJsonAsync<Person>(cancellationToken: ct);
        return ApiResult<Person>.Ok(person!);
    }

    public async Task<ApiResult> UpdateAsync(ulong id, PersonFormModel request, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"api/person/{id}", request, ct);
        return response.IsSuccessStatusCode
            ? ApiResult.Ok()
            : ApiResult.Fail(await ExtractErrorAsync(response, ct));
    }

    public async Task<ApiResult> DeleteAsync(ulong id, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"api/person/{id}", ct);
        return response.IsSuccessStatusCode
            ? ApiResult.Ok()
            : ApiResult.Fail(await ExtractErrorAsync(response, ct));
    }

    // Validation failures (400) come back as a JSON ValidationProblemDetails body;
    // duplicate-email conflicts (409) come back as a plain-text string. Handle both
    // without taking a hard dependency on the ASP.NET Core MVC types.
    private static async Task<string> ExtractErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(body))
        {
            return $"Request failed ({(int)response.StatusCode} {response.ReasonPhrase}).";
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var messages = errors.EnumerateObject()
                    .SelectMany(p => p.Value.EnumerateArray())
                    .Select(v => v.GetString())
                    .Where(m => !string.IsNullOrWhiteSpace(m));

                var joined = string.Join(" ", messages);
                if (!string.IsNullOrWhiteSpace(joined))
                {
                    return joined;
                }
            }

            if (root.TryGetProperty("title", out var title))
            {
                var text = title.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON (e.g. the plain-text 409 conflict message) — fall through and return the raw body.
        }

        return body.Trim('"');
    }
}
