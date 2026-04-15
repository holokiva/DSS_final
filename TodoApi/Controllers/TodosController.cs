using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoApi.Models;
using TodoApi.Services;

namespace TodoApi.Controllers;

[ApiController]
[Route("api/todos")]
public class TodosController(ITodoService todoService) : ControllerBase
{
    [HttpGet("public")]
    [ProducesResponseType<PaginatedResponse<TodoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPublic([FromQuery] TodoQueryParams query)
    {
        var todos = await todoService.GetPublicTodosAsync(query);
        return Ok(todos);
    }

    [Authorize]
    [HttpGet]
    [ProducesResponseType<PaginatedResponse<TodoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMine([FromQuery] TodoQueryParams query)
    {
        var todos = await todoService.GetUserTodosAsync(GetCurrentUserId(), query);
        return Ok(todos);
    }

    [Authorize]
    [HttpPost]
    [ProducesResponseType<TodoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateTodoRequest request)
    {
        var todo = await todoService.CreateAsync(GetCurrentUserId(), request);
        return CreatedAtAction(nameof(GetById), new { id = todo.Id }, todo);
    }

    [Authorize]
    [HttpGet("{id:guid}")]
    [ProducesResponseType<TodoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var (todo, forbidden) = await todoService.GetByIdAsync(GetCurrentUserId(), id);
        if (forbidden)
        {
            return ForbiddenProblem();
        }

        if (todo is null)
        {
            return NotFoundProblem();
        }

        return Ok(todo);
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    [ProducesResponseType<TodoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTodoRequest request)
    {
        var (todo, forbidden) = await todoService.UpdateAsync(GetCurrentUserId(), id, request);
        if (forbidden)
        {
            return ForbiddenProblem();
        }

        if (todo is null)
        {
            return NotFoundProblem();
        }

        return Ok(todo);
    }

    [Authorize]
    [HttpPatch("{id:guid}/completion")]
    [ProducesResponseType<TodoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PatchCompletion(Guid id, [FromBody] PatchTodoCompletionRequest request)
    {
        var (todo, forbidden) = await todoService.PatchCompletionAsync(GetCurrentUserId(), id, request.IsCompleted);
        if (forbidden)
        {
            return ForbiddenProblem();
        }

        if (todo is null)
        {
            return NotFoundProblem();
        }

        return Ok(todo);
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await todoService.DeleteAsync(GetCurrentUserId(), id);
        if (result.Forbidden)
        {
            return ForbiddenProblem();
        }

        if (result.NotFound)
        {
            return NotFoundProblem();
        }

        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                  User.FindFirstValue("sub") ??
                  throw new UnauthorizedAccessException("Missing user id claim.");

        return Guid.Parse(sub);
    }

    private ObjectResult ForbiddenProblem() => StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
    {
        Status = StatusCodes.Status403Forbidden,
        Title = "Forbidden.",
        Type = "https://tools.ietf.org/html/rfc9110#section-15.5.4"
    });

    private NotFoundObjectResult NotFoundProblem() => NotFound(new ProblemDetails
    {
        Status = StatusCodes.Status404NotFound,
        Title = "Todo not found.",
        Type = "https://tools.ietf.org/html/rfc9110#section-15.5.5"
    });
}
