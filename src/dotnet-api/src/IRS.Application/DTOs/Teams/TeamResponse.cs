namespace IRS.Application.DTOs.Teams;

public class TeamResponse
{
    public int id { get; set; }
    public string name { get; set; } = null!;
    public DateTime created_at { get; set; }
    public string current_user_role { get; set; } = null!;
}
