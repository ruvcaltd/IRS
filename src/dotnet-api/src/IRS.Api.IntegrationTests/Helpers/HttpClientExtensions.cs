using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace IRS.Api.IntegrationTests.Helpers;

public static class HttpClientExtensions
{
    public static void SetBearerToken(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", token);
    }

    public static async Task<HttpResponseMessage> PostAsJsonAsync<T>(
        this HttpClient client, 
        string url, 
        T data,
        string? bearerToken = null)
    {
        if (!string.IsNullOrEmpty(bearerToken))
        {
            client.SetBearerToken(bearerToken);
        }

        return await client.PostAsJsonAsync(url, data);
    }

    public static async Task<HttpResponseMessage> PutAsJsonAsync<T>(
        this HttpClient client, 
        string url, 
        T data,
        string? bearerToken = null)
    {
        if (!string.IsNullOrEmpty(bearerToken))
        {
            client.SetBearerToken(bearerToken);
        }

        return await client.PutAsJsonAsync(url, data);
    }

    public static async Task<HttpResponseMessage> GetWithAuthAsync(
        this HttpClient client,
        string url,
        string bearerToken)
    {
        client.SetBearerToken(bearerToken);
        return await client.GetAsync(url);
    }

    public static async Task<HttpResponseMessage> DeleteWithAuthAsync(
        this HttpClient client,
        string url,
        string bearerToken)
    {
        client.SetBearerToken(bearerToken);
        return await client.DeleteAsync(url);
    }
}
