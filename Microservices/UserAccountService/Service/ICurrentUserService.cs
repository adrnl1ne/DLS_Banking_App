namespace UserAccountService.Service;

public interface ICurrentUserService
{
    int? UserId { get; }
    string Role { get; }
}
