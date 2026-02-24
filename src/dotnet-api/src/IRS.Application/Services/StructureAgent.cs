namespace IRS.Application.Services;

public class StructureAgent : IStructureAgent
{
    public Task<List<string>> GetDefaultSectionsAsync(string securityType)
    {
        var sections = new List<string>();
        switch (securityType?.Trim())
        {
            case "Sovereign":
                sections.AddRange(new[] { "Economic Outlook", "Fiscal Policy", "Geopolitical Risk" });
                break;
            case "Corporate":
            default:
                sections.AddRange(new[] { "Market Data", "Business Model", "Financial Health", "Management Quality", "ESG" });
                break;
        }
        return Task.FromResult(sections);
    }
}
