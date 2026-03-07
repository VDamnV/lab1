using Microsoft.AspNetCore.Mvc;
using Messenger.Api.Models;
using Messenger.Api.Storage;

namespace Messenger.Api.Controllers;

[ApiController]
[Route("users")]
public class UsersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public UsersController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { Error = "User name cannot be empty." });
        }

        var user = new User
        {
            Name = request.Name
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return Created($"/users/{user.Id}", user);
    }
}

public record CreateUserDto(string Name);