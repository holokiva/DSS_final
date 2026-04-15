using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TodoApi.Data;
using TodoApi.Models;

namespace TodoApi.Services;

public interface IPasswordHasherService
{
    string HashPassword(User user, string password);
    bool Verify(User user, string providedPassword);
}

public class PasswordHasherService : IPasswordHasherService
{
    private readonly PasswordHasher<User> _hasher = new();

    public string HashPassword(User user, string password) => _hasher.HashPassword(user, password);

    public bool Verify(User user, string providedPassword)
        => _hasher.VerifyHashedPassword(user, user.PasswordHash, providedPassword) != PasswordVerificationResult.Failed;
}

public interface IJwtTokenService
{
    string CreateToken(User user);
}

public class JwtTokenService(IConfiguration configuration) : IJwtTokenService
{
    public string CreateToken(User user)
    {
        var key = configuration["Jwt:Key"] ?? throw new InvalidOperationException("Missing JWT key");
        var issuer = configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Missing JWT issuer");
        var audience = configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Missing JWT audience");
        var expirationMinutes = int.TryParse(configuration["Jwt:ExpirationMinutes"], out var parsed) ? parsed : 120;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("displayName", user.DisplayName)
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public interface IAuthService
{
    Task<(bool Success, string? Error, AuthResponse? Response)> RegisterAsync(RegisterRequest request);
    Task<(bool Success, AuthResponse? Response)> LoginAsync(LoginRequest request);
}

public class AuthService(
    AppDbContext dbContext,
    IPasswordHasherService passwordHasher,
    IJwtTokenService jwtTokenService) : IAuthService
{
    public async Task<(bool Success, string? Error, AuthResponse? Response)> RegisterAsync(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var existing = await dbContext.Users.AsNoTracking().AnyAsync(x => x.Email == email);
        if (existing)
        {
            return (false, "Email already exists", null);
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var token = jwtTokenService.CreateToken(user);
        return (true, null, new AuthResponse
        {
            Token = token,
            User = MapUser(user)
        });
    }

    public async Task<(bool Success, AuthResponse? Response)> LoginAsync(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Email == email);
        if (user is null || !passwordHasher.Verify(user, request.Password))
        {
            return (false, null);
        }

        var token = jwtTokenService.CreateToken(user);
        return (true, new AuthResponse
        {
            Token = token,
            User = MapUser(user)
        });
    }

    private static UserDto MapUser(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        DisplayName = user.DisplayName,
        CreatedAt = user.CreatedAt
    };
}

public interface ITodoService
{
    Task<PaginatedResponse<TodoDto>> GetPublicTodosAsync(TodoQueryParams query);
    Task<PaginatedResponse<TodoDto>> GetUserTodosAsync(Guid userId, TodoQueryParams query);
    Task<(TodoDto? Todo, bool Forbidden)> GetByIdAsync(Guid currentUserId, Guid todoId);
    Task<TodoDto> CreateAsync(Guid userId, CreateTodoRequest request);
    Task<(TodoDto? Todo, bool Forbidden)> UpdateAsync(Guid currentUserId, Guid todoId, UpdateTodoRequest request);
    Task<(TodoDto? Todo, bool Forbidden)> PatchCompletionAsync(Guid currentUserId, Guid todoId, bool isCompleted);
    Task<(bool Deleted, bool Forbidden, bool NotFound)> DeleteAsync(Guid currentUserId, Guid todoId);
}

public class TodoService(AppDbContext dbContext) : ITodoService
{
    public Task<PaginatedResponse<TodoDto>> GetPublicTodosAsync(TodoQueryParams query)
        => ApplyQueryAsync(dbContext.TodoItems.AsNoTracking().Where(x => x.IsPublic), query);

    public Task<PaginatedResponse<TodoDto>> GetUserTodosAsync(Guid userId, TodoQueryParams query)
        => ApplyQueryAsync(dbContext.TodoItems.AsNoTracking().Where(x => x.UserId == userId), query);

    public async Task<(TodoDto? Todo, bool Forbidden)> GetByIdAsync(Guid currentUserId, Guid todoId)
    {
        var entity = await dbContext.TodoItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == todoId);
        if (entity is null)
        {
            return (null, false);
        }

        if (entity.UserId != currentUserId)
        {
            return (null, true);
        }

        return (MapTodo(entity), false);
    }

    public async Task<TodoDto> CreateAsync(Guid userId, CreateTodoRequest request)
    {
        var now = DateTime.UtcNow;
        var entity = new TodoItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = request.Title.Trim(),
            Details = request.Details?.Trim(),
            Priority = request.Priority,
            DueDate = request.DueDate,
            IsCompleted = false,
            IsPublic = request.IsPublic,
            CreatedAt = now,
            UpdatedAt = now
        };

        dbContext.TodoItems.Add(entity);
        await dbContext.SaveChangesAsync();
        return MapTodo(entity);
    }

    public async Task<(TodoDto? Todo, bool Forbidden)> UpdateAsync(Guid currentUserId, Guid todoId, UpdateTodoRequest request)
    {
        var entity = await dbContext.TodoItems.FirstOrDefaultAsync(x => x.Id == todoId);
        if (entity is null)
        {
            return (null, false);
        }

        if (entity.UserId != currentUserId)
        {
            return (null, true);
        }

        entity.Title = request.Title.Trim();
        entity.Details = request.Details?.Trim();
        entity.Priority = request.Priority;
        entity.DueDate = request.DueDate;
        entity.IsCompleted = request.IsCompleted;
        entity.IsPublic = request.IsPublic;
        entity.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();
        return (MapTodo(entity), false);
    }

    public async Task<(TodoDto? Todo, bool Forbidden)> PatchCompletionAsync(Guid currentUserId, Guid todoId, bool isCompleted)
    {
        var entity = await dbContext.TodoItems.FirstOrDefaultAsync(x => x.Id == todoId);
        if (entity is null)
        {
            return (null, false);
        }

        if (entity.UserId != currentUserId)
        {
            return (null, true);
        }

        entity.IsCompleted = isCompleted;
        entity.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();
        return (MapTodo(entity), false);
    }

    public async Task<(bool Deleted, bool Forbidden, bool NotFound)> DeleteAsync(Guid currentUserId, Guid todoId)
    {
        var entity = await dbContext.TodoItems.FirstOrDefaultAsync(x => x.Id == todoId);
        if (entity is null)
        {
            return (false, false, true);
        }

        if (entity.UserId != currentUserId)
        {
            return (false, true, false);
        }

        dbContext.TodoItems.Remove(entity);
        await dbContext.SaveChangesAsync();
        return (true, false, false);
    }

    private static TodoDto MapTodo(TodoItem entity) => new()
    {
        Id = entity.Id,
        UserId = entity.UserId,
        Title = entity.Title,
        Details = entity.Details,
        Priority = entity.Priority,
        DueDate = entity.DueDate,
        IsCompleted = entity.IsCompleted,
        IsPublic = entity.IsPublic,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };

    private static async Task<PaginatedResponse<TodoDto>> ApplyQueryAsync(IQueryable<TodoItem> source, TodoQueryParams query)
    {
        if (query.Status == "active")
        {
            source = source.Where(x => !x.IsCompleted);
        }
        else if (query.Status == "completed")
        {
            source = source.Where(x => x.IsCompleted);
        }

        if (!string.IsNullOrWhiteSpace(query.Priority) &&
            Enum.TryParse<TodoPriority>(query.Priority, true, out var parsedPriority))
        {
            source = source.Where(x => x.Priority == parsedPriority);
        }

        if (query.DueFrom.HasValue)
        {
            source = source.Where(x => x.DueDate >= query.DueFrom.Value);
        }

        if (query.DueTo.HasValue)
        {
            source = source.Where(x => x.DueDate <= query.DueTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            source = source.Where(x =>
                x.Title.ToLower().Contains(search) ||
                (x.Details != null && x.Details.ToLower().Contains(search)));
        }

        source = (query.SortBy, query.SortDir) switch
        {
            ("dueDate", "asc") => source.OrderBy(x => x.DueDate),
            ("dueDate", "desc") => source.OrderByDescending(x => x.DueDate),
            ("priority", "asc") => source.OrderBy(x => x.Priority),
            ("priority", "desc") => source.OrderByDescending(x => x.Priority),
            ("title", "asc") => source.OrderBy(x => x.Title),
            ("title", "desc") => source.OrderByDescending(x => x.Title),
            ("createdAt", "asc") => source.OrderBy(x => x.CreatedAt),
            _ => source.OrderByDescending(x => x.CreatedAt)
        };

        var totalItems = await source.CountAsync();
        var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)query.PageSize);

        var items = await source
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => MapTodo(x))
            .ToListAsync();

        return new PaginatedResponse<TodoDto>
        {
            Items = items,
            Page = query.Page,
            PageSize = query.PageSize,
            TotalItems = totalItems,
            TotalPages = totalPages
        };
    }
}
