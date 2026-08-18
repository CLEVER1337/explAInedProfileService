using System.Net.Http.Json;

internal static class UpstreamJson
{
    public const string TotalCountHeader = "X-Total-Count";

    public static async Task<UpstreamPage<T>> ReadPageAsync<T>(
        HttpClient http, string service, string path, CancellationToken ct)
    {
        using var response = await http.GetAsync(path, ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"{service} returned {(int)response.StatusCode} for {path}");
        }

        var items = await response.Content.ReadFromJsonAsync<List<T>>(ct) ?? [];

        return new UpstreamPage<T>(items, TotalOf(response, items.Count));
    }

    private static int TotalOf(HttpResponseMessage response, int fallback) =>
        response.Headers.TryGetValues(TotalCountHeader, out var values)
        && int.TryParse(values.FirstOrDefault(), out var total)
            ? total
            : fallback;
}
