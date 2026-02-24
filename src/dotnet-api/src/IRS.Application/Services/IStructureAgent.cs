namespace IRS.Application.Services;

public interface IStructureAgent
{
    Task<List<string>> GetDefaultSectionsAsync(string securityType);
}
