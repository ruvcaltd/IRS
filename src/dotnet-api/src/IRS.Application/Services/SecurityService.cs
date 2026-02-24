using System.Text.Json;
using IRS.Application.DTOs.Securities;
using IRS.Infrastructure;
using IRS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IRS.Application.Services;

public class SecurityService : ISecurityService
{
    private readonly IrsDbContext _db;
    private readonly ILogger<SecurityService> _logger;
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private const string OpenFigiEndpoint = "https://api.openfigi.com/v3/mapping";

    public SecurityService(IrsDbContext db, IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<SecurityService> logger)
    {
        _db = db;
        _http = httpClientFactory.CreateClient("openfigi");
        _config = config;
        _logger = logger;
    }

    public async Task<IEnumerable<SecuritySearchItem>> SearchAsync(string query, int take = 20)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Enumerable.Empty<SecuritySearchItem>();

        query = query.Trim();

        // First search local DB
        var local = await _db.Securities
            .Where(s => (s.ticker != null && EF.Functions.Like(s.ticker, $"%{query}%")) ||
                        (s.name != null && EF.Functions.Like(s.name, $"%{query}%")))
            .OrderBy(s => s.ticker)
            .Take(take)
            .Select(s => new SecuritySearchItem
            {
                figi = s.figi,
                ticker = s.ticker,
                name = s.name,
                market_sector = s.market_sector,
                security_type = s.security_type,
                exchange_code = s.exchange_code,
                mic_code = s.mic_code,
                share_class_figi = s.share_class_figi,
                composite_figi = s.composite_figi,
                security_type2 = s.security_type2,
                security_description = s.security_description
            })
            .ToListAsync();

        if (local.Count > 0)
        {
            _logger.LogInformation("Found {Count} securities in local DB for query '{Query}'", local.Count, query);
            return local;
        }

        // Search OpenFIGI if nothing found locally
        return await SearchOpenFigiAsync(query, take);
    }

    private async Task<IEnumerable<SecuritySearchItem>> SearchOpenFigiAsync(string query, int take)
    {
        try
        {
            _logger.LogInformation("Searching OpenFIGI for query '{Query}'", query);

            // Try searching by ticker first, then by name
            var tickerResults = await QueryOpenFigiAsync(new OpenFigiRequest
            {
                idType = "TICKER",
                idValue = query.ToUpper()
            });

            var nameResults = new List<OpenFigiResult>();
            if (tickerResults.Count == 0)
            {
                nameResults = await QueryOpenFigiAsync(new OpenFigiRequest
                {
                    idType = "NAME",
                    idValue = query
                });
            }

            var allResults = tickerResults.Concat(nameResults).Take(take).ToList();

            if (allResults.Count == 0)
            {
                _logger.LogInformation("No results found in OpenFIGI for query '{Query}'", query);
                return Enumerable.Empty<SecuritySearchItem>();
            }

            // Store results in local DB for future queries
            await StoreSecuritiesAsync(allResults);

            // Map to search items
            var searchItems = allResults.Select(r => new SecuritySearchItem
            {
                figi = r.figi,
                ticker = r.ticker,
                name = r.name,
                market_sector = r.marketSector,
                security_type = MapSecurityType(r.securityType),
                exchange_code = r.exchCode,
                mic_code = r.micCode,
                share_class_figi = r.shareClassFIGI,
                composite_figi = r.compositeFIGI,
                security_type2 = r.securityType2,
                security_description = r.securityDescription
            }).ToList();

            _logger.LogInformation("Found {Count} securities from OpenFIGI for query '{Query}'", searchItems.Count, query);
            return searchItems;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenFIGI search failed for query '{Query}'", query);
            return Enumerable.Empty<SecuritySearchItem>();
        }
    }

    private async Task<List<OpenFigiResult>> QueryOpenFigiAsync(OpenFigiRequest request)
    {
        var requestBody = new[] { request };
        var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, OpenFigiEndpoint);

        // Add API key if configured (increases rate limit from 25/hour to 500/hour)
        var apiKey = _config["OpenFigi:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            httpRequest.Headers.TryAddWithoutValidation("X-OPENFIGI-APIKEY", apiKey);
        }

        httpRequest.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var results = JsonSerializer.Deserialize<List<OpenFigiResponse>>(responseJson, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        if (results == null || results.Count == 0)
            return new List<OpenFigiResult>();

        var firstResult = results[0];

        if (!string.IsNullOrEmpty(firstResult.error))
        {
            _logger.LogWarning("OpenFIGI returned error: {Error}", firstResult.error);
            return new List<OpenFigiResult>();
        }

        return firstResult.data ?? new List<OpenFigiResult>();
    }

    private async Task StoreSecuritiesAsync(List<OpenFigiResult> results)
    {
        foreach (var result in results)
        {
            if (string.IsNullOrWhiteSpace(result.figi))
                continue;

            // Check if already exists
            var exists = await _db.Securities.AnyAsync(s => s.figi == result.figi);
            if (exists)
                continue;

            var security = new Security
            {
                figi = result.figi,
                ticker = result.ticker,
                name = result.name ?? result.securityDescription,
                market_sector = result.marketSector,
                security_type = MapSecurityType(result.securityType),
                composite_figi = result.compositeFIGI,
                exchange_code = result.exchCode,
                mic_code = result.micCode,
                share_class_figi = result.shareClassFIGI,
                security_type2 = result.securityType2,
                security_description = result.securityDescription,
                last_synced_at = DateTime.UtcNow,
            };

            _db.Securities.Add(security);
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Stored {Count} new securities in local DB", results.Count);
    }

    private static string MapSecurityType(string? openFigiType)
    {
        if (string.IsNullOrWhiteSpace(openFigiType))
            return "Corporate";

        return openFigiType.ToUpperInvariant() switch
        {
            "COMMON STOCK" => "Corporate",
            "EQUITY" => "Corporate",
            "BOND" => "Corporate",
            "CORP" => "Corporate",
            "GOVT" => "Sovereign",
            "TREASURY" => "Sovereign",
            _ => "Corporate"
        };
    }
}

// OpenFIGI Request/Response Models
internal class OpenFigiRequest
{
    public string? idType { get; set; }
    public string? idValue { get; set; }
    public string? exchCode { get; set; }
    public string? micCode { get; set; }
    public string? currency { get; set; }
    public string? marketSecDes { get; set; }
    public string? securityType { get; set; }
    public string? securityType2 { get; set; }
}

internal class OpenFigiResponse
{
    public List<OpenFigiResult>? data { get; set; }
    public string? error { get; set; }
    public string? warning { get; set; }
}

internal class OpenFigiResult
{
    public string figi { get; set; } = string.Empty;
    public string? securityType { get; set; }
    public string? marketSector { get; set; }
    public string? ticker { get; set; }
    public string? name { get; set; }
    public string? exchCode { get; set; }
    public string? micCode { get; set; }
    public string? shareClassFIGI { get; set; }
    public string? compositeFIGI { get; set; }
    public string? securityType2 { get; set; }
    public string? securityDescription { get; set; }
}
