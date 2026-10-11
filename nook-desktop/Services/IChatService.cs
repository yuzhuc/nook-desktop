using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using nook_desktop.Models;

namespace nook_desktop.Services;

public interface IChatService
{
    event EventHandler<ChatMessage>? MessageReceived;

    IReadOnlyList<ChatMessage> GetHistory(string channelId, int take = 50);

    Task SendAsync(string channelId, string content, CancellationToken cancellationToken = default);
}
