namespace AccountService.Models;

public class User
{
    public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int RoleId { get; set; }
        public Role Role { get; set; } = null!;
}
