using System.Collections.Generic;
using nook_desktop.Models;

namespace nook_desktop.Services;

/// <summary>群组 / 频道分组 / 频道的本地存储。目前只有 SQLite 一种实现。</summary>
public interface IGroupStore
{
    IReadOnlyList<Group> GetAll();

    void Add(string name, string accent);

    void AddCategory(string groupId, string name);

    void AddChannel(string groupId, string? categoryId, string name, ChannelKind kind);

    void AddMember(string groupId, string name);

    /// <summary>把域顶到域栏最上面(左栏是倒序渲染,序号排到最后就排最上面)。</summary>
    void Pin(string groupId);

    /// <summary>取消置顶:只把标记清掉,位置不动。</summary>
    void Unpin(string groupId);

    /// <summary>删一个频道(右键菜单里的「删除频道」)。</summary>
    void DeleteChannel(string channelId);

    /// <summary>聊天消息:追加一条 / 取某个频道最近 N 条(时间正序)。</summary>
    void AddMessage(ChatMessage message);

    IReadOnlyList<ChatMessage> GetMessages(string channelId, int take);

    void Delete(string groupId);
}
