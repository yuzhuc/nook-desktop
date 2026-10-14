using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using nook_desktop.Models;

namespace nook_desktop.Services;

/// <summary>
/// 本地聊天:消息直接写进 SQLite(和群组/频道同一个库),重启后还在。
/// 还没有真实后端,所以作者先固定是自己。
/// </summary>
public sealed class SqliteChatService(IGroupStore store) : IChatService
{
    public event EventHandler<ChatMessage>? MessageReceived;

    public IReadOnlyList<ChatMessage> GetHistory(string channelId, int take = 50)
        => store.GetMessages(channelId, take);

    public Task SendAsync(string channelId, string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Task.CompletedTask;
        }

        var message = new ChatMessage(
            Guid.NewGuid().ToString("N"),
            channelId,
            "self",
            "nook 用户",
            content.Trim(),
            DateTimeOffset.Now);

        store.AddMessage(message);
        MessageReceived?.Invoke(this, message);
        return Task.CompletedTask;
    }
}