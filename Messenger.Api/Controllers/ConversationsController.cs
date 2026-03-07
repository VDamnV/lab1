using Microsoft.AspNetCore.Mvc;
using Messenger.Api.Models;
using Messenger.Api.Storage;

namespace Messenger.Api.Controllers;

[ApiController]
[Route("conversations")]
public class ConversationsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ConversationsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost]
    public async Task<IActionResult> CreateConversation()
    {
        var conversation = new Conversation { Type = "direct" };
        _context.Conversations.Add(conversation);
        await _context.SaveChangesAsync();
        
        return Created($"/conversations/{conversation.Id}", conversation);
    }
}