using Microsoft.EntityFrameworkCore;
using Messenger.Api.Models;
using Messenger.Api.Storage;

namespace Messenger.Api.Services;

public class MessageService
{
    private readonly ApplicationDbContext _context;

    public MessageService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Message> SendMessageAsync(string senderId, string conversationId, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Message text cannot be empty.");

        var senderExists = await _context.Users.AnyAsync(u => u.Id == senderId);
        if (!senderExists)
            throw new ArgumentException($"User with ID '{senderId}' does not exist.");

        var conversationExists = await _context.Conversations.AnyAsync(c => c.Id == conversationId);
        if (!conversationExists)
            throw new ArgumentException($"Conversation with ID '{conversationId}' does not exist.");

        var message = new Message
        {
            SenderId = senderId,
            ConversationId = conversationId,
            Text = text
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        return message;
    }

    public async Task<List<Message>> GetMessagesAsync(string conversationId)
    {
        var conversationExists = await _context.Conversations.AnyAsync(c => c.Id == conversationId);
        if (!conversationExists)
            throw new ArgumentException($"Conversation with ID '{conversationId}' does not exist.");

        return await _context.Messages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
    }
}