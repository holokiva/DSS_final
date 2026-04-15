using System.ComponentModel.DataAnnotations;

namespace TodoApi.Models;

public enum TodoPriority
{
    Low = 0,
    Medium = 1,
    High = 2
}

public class User
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public ICollection<TodoItem> Todos { get; set; } = new List<TodoItem>();
}

public class TodoItem
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MinLength(3)]
    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Details { get; set; }

    [Required]
    public TodoPriority Priority { get; set; }

    public DateTime? DueDate { get; set; }

    public bool IsCompleted { get; set; }

    public bool IsPublic { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public User? User { get; set; }
}
