using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Messenger.Api.Models;
using Messenger.Api.Storage;

namespace Messenger.Tests.IntegrationTests;

public class MessageApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public MessageApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient(); 
    }

    [Fact]
    public async Task SendMessage_And_RetrieveHistory_ShouldWork()
    {
        var createUserResponse = await _client.PostAsJsonAsync("/users", new { Name = "Alice" });
        createUserResponse.EnsureSuccessStatusCode();
        var user = await createUserResponse.Content.ReadFromJsonAsync<User>();
        
        Assert.NotNull(user);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var conversation = new Conversation { Type = "direct" };
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync();

        var sendMessageDto = new
        {
            SenderId = user.Id,
            ConversationId = conversation.Id,
            Text = "Hello, offline world!"
        };

        var sendMessageResponse = await _client.PostAsJsonAsync("/messages", sendMessageDto);
        
        Assert.Equal(HttpStatusCode.Created, sendMessageResponse.StatusCode); 

        var getMessagesResponse = await _client.GetAsync($"/conversations/{conversation.Id}/messages");
        getMessagesResponse.EnsureSuccessStatusCode();
        
        var messages = await getMessagesResponse.Content.ReadFromJsonAsync<List<Message>>();
        
        Assert.NotNull(messages);
        Assert.Single(messages);
        Assert.Equal("Hello, offline world!", messages[0].Text);
        Assert.Equal(user.Id, messages[0].SenderId);
        
        Assert.Equal("PersistedInQueue", messages[0].Status); 
    }
}