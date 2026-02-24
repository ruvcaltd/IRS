namespace IRS.Application.DTOs.Securities;

public class SecuritySearchItem
{
    public string? figi { get; set; }
    public string? ticker { get; set; }
    public string? name { get; set; }
    public string? market_sector { get; set; }
    public string? security_type { get; set; }
    public string? exchange_code { get; set; }
    public string? mic_code { get; set; }
    public string? share_class_figi { get; set; }
    public string? composite_figi { get; set; }
    public string? security_type2 { get; set; }
    public string? security_description { get; set; }
}
