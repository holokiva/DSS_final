using System.ComponentModel.DataAnnotations;

namespace TodoApi.Models;

public class RegisterRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public UserDto User { get; set; } = new();
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateTodoRequest
{
    [Required]
    [MinLength(3)]
    [MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Details { get; set; }

    [Required]
    public TodoPriority Priority { get; set; }

    public DateTime? DueDate { get; set; }

    public bool IsPublic { get; set; }
}

public class UpdateTodoRequest
{
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
}

public class PatchTodoCompletionRequest
{
    [Required]
    public bool IsCompleted { get; set; }
}

public class TodoDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Details { get; set; }
    public TodoPriority Priority { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsPublic { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class TodoQueryParams
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 50)]
    public int PageSize { get; set; } = 10;

    [RegularExpression("^(all|active|completed)$", ErrorMessage = "status must be all|active|completed")]
    public string Status { get; set; } = "all";

    [RegularExpression("^(low|medium|high)?$", ErrorMessage = "priority must be low|medium|high")]
    public string? Priority { get; set; }

    public DateTime? DueFrom { get; set; }
    public DateTime? DueTo { get; set; }

    [RegularExpression("^(createdAt|dueDate|priority|title)$", ErrorMessage = "sortBy is invalid")]
    public string SortBy { get; set; } = "createdAt";

    [RegularExpression("^(asc|desc)$", ErrorMessage = "sortDir must be asc|desc")]
    public string SortDir { get; set; } = "desc";

    public string? Search { get; set; }
}

public class PaginatedResponse<T>
{
    public List<T> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
}
