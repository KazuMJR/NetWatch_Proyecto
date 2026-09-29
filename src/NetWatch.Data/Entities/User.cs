namespace NetWatch.Data.Entities;

public sealed class User
{
    public int Id { get; set; }
    public int RoleId { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Email { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime RegisteredAtUtc { get; set; } = DateTime.UtcNow;
    public Role Role { get; set; } = null!;
}

