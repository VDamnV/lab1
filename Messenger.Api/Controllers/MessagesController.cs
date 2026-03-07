using Microsoft.AspNetCore.Mvc;
using Messenger.Api.Services;

namespace Messenger.Api.Controllers;

[ApiController]
[Route("messages")]
public class MessagesController : ControllerBase
{
    private readonly MessageService _messageService;

    public MessagesController(MessageService messageService)
    {
        _messageService = messageService;
    }

    [HttpPost]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageDto request)
    {
        try
        {
            var message = await _messageService.SendMessageAsync(
                request.SenderId, 
                request.ConversationId, 
                request.Text
            );
            
            return Created($"/conversations/{message.ConversationId}/messages", message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    [HttpGet("/conversations/{conversationId}/messages")]
    public async Task<IActionResult> GetMessages(string conversationId)
    {
        try
        {
            var messages = await _messageService.GetMessagesAsync(conversationId);
            return Ok(messages);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { Error = ex.Message });
        }
    }
}

public record SendMessageDto(string SenderId, string ConversationId, string Text);