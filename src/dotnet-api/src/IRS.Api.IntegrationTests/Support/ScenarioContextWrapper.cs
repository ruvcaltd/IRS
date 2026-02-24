using System.Net.Http;
using System.Collections.Generic;

namespace IRS.Api.IntegrationTests.Support;

public class ScenarioContextWrapper
{
    public HttpClient HttpClient { get; }
    public HttpResponseMessage? LastResponse { get; set; }
    public string? AuthToken { get; set; }
    public Dictionary<string, object> Data { get; } = new();

    public ScenarioContextWrapper(HttpClient httpClient)
    {
        HttpClient = httpClient;
    }

    public void Set<T>(string key, T value) where T : notnull
    {
        Data[key] = value;
    }

    public T Get<T>(string key)
    {
        return (T)Data[key];
    }

    public bool TryGet<T>(string key, out T? value)
    {
        if (Data.TryGetValue(key, out var obj) && obj is T typedValue)
        {
            value = typedValue;
            return true;
        }
        value = default;
        return false;
    }
}
