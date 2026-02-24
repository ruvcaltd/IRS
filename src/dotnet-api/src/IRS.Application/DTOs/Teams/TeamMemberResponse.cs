namespace IRS.Application.DTOs.Teams;

public class TeamMemberResponse
{
    public int user_id { get; set; }
    public int team_id { get; set; }
    public int team_role_id { get; set; }
    public string team_role_name { get; set; } = null!;
    public string user_full_name { get; set; } = null!;
    public string user_email { get; set; } = null!;
    public string status { get; set; } = null!;
    public DateTime created_at { get; set; }
}
