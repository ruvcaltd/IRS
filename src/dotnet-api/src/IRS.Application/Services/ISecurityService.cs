using IRS.Application.DTOs.Securities;

namespace IRS.Application.Services;

public interface ISecurityService
{
    Task<IEnumerable<SecuritySearchItem>> SearchAsync(string query, int take = 20);
}
