namespace QueryService.DTO;

public class UserDocument
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
}