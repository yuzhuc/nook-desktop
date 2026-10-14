using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using nook_desktop.Models;

namespace nook_desktop.Services;

/// <summary>
/// 群组 / 频道分组 / 频道的 SQLite 存储,库文件固定在 %APPDATA%\nook\nook.db。
/// 只有首次建库时插入演示数据,之后不再插入,所以删光了也不会自己长回来。
/// </summary>
public sealed class SqliteGroupStore : IGroupStore
{
    private const string CreateGroupsSql =
        "CREATE TABLE IF NOT EXISTS groups (" +
        "id TEXT PRIMARY KEY, name TEXT NOT NULL, description TEXT NULL, " +
        "accent TEXT NOT NULL, position INTEGER NOT NULL, pinned INTEGER NOT NULL DEFAULT 0)";

    private const string CreateCategoriesSql =
        "CREATE TABLE IF NOT EXISTS categories (" +
        "id TEXT PRIMARY KEY, group_id TEXT NOT NULL, name TEXT NOT NULL, position INTEGER NOT NULL)";

    private const string CreateChannelsSql =
        "CREATE TABLE IF NOT EXISTS channels (" +
        "id TEXT PRIMARY KEY, group_id TEXT NOT NULL, category_id TEXT NULL, " +
        "name TEXT NOT NULL, kind INTEGER NOT NULL, capacity INTEGER NOT NULL, position INTEGER NOT NULL)";

    private const string CreateMetaSql =
        "CREATE TABLE IF NOT EXISTS meta (key TEXT PRIMARY KEY, value TEXT NOT NULL)";

    private const string CreateMembersSql =
        "CREATE TABLE IF NOT EXISTS members (" +
        "id TEXT PRIMARY KEY, group_id TEXT NOT NULL, name TEXT NOT NULL, position INTEGER NOT NULL)";

    /// <summary>聊天消息表:本地存一份,重启还在。</summary>
    private const string CreateMessagesSql =
        "CREATE TABLE IF NOT EXISTS messages (" +
        "id TEXT PRIMARY KEY, channel_id TEXT NOT NULL, author_id TEXT NOT NULL, " +
        "author TEXT NOT NULL, content TEXT NOT NULL, sent_at INTEGER NOT NULL)";

    private const string InsertGroupSql =
        "INSERT INTO groups (id, name, description, accent, position) " +
        "VALUES ($id, $name, $description, $accent, " +
        "(SELECT COALESCE(MAX(position), 0) + 1 FROM groups))";

    private const string InsertCategorySql =
        "INSERT INTO categories (id, group_id, name, position) " +
        "VALUES ($id, $group, $name, " +
        "(SELECT COALESCE(MAX(position), 0) + 1 FROM categories WHERE group_id = $group))";

    private const string InsertChannelSql =
        "INSERT INTO channels (id, group_id, category_id, name, kind, capacity, position) " +
        "VALUES ($id, $group, $category, $name, $kind, $capacity, " +
        "(SELECT COALESCE(MAX(position), 0) + 1 FROM channels WHERE group_id = $group))";

    /// <summary>演示数据的版本号。改结构时 +1,老库会再补一次。</summary>
    private const int DemoDataVersion = 2;

    private readonly string _connectionString;

    public SqliteGroupStore()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "nook");
        Directory.CreateDirectory(directory);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(directory, "nook.db"),
        }.ToString();

        EnsureSchema();
    }

    public IReadOnlyList<Group> GetAll()
    {
        using var connection = Open();

        var categoryRows = ReadCategories(connection);
        var channelRows = ReadChannels(connection);

        var channelsByCategory = channelRows
            .Where(row => row.CategoryId is not null)
            .GroupBy(row => row.CategoryId!)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Channel>)g.Select(ToChannel).ToList());

        var membersByGroup = ReadMembers(connection);

        var looseChannelsByGroup = channelRows
            .Where(row => row.CategoryId is null)
            .GroupBy(row => row.GroupId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Channel>)g.Select(ToChannel).ToList());

        var categoriesByGroup = categoryRows
            .GroupBy(row => row.GroupId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ChannelCategory>)g.Select(row => new ChannelCategory
                {
                    Id = row.Id,
                    Name = row.Name,
                    Channels = channelsByCategory.TryGetValue(row.Id, out var channels)
                        ? channels
                        : Array.Empty<Channel>(),
                }).ToList());

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, description, accent, pinned FROM groups ORDER BY position";

        using var reader = command.ExecuteReader();
        var groups = new List<Group>();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            groups.Add(new Group
            {
                Id = id,
                Name = reader.GetString(1),
                Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                AccentColor = reader.GetString(3),
                IsPinned = reader.GetInt32(4) != 0,
                Categories = categoriesByGroup.TryGetValue(id, out var categories)
                    ? categories
                    : Array.Empty<ChannelCategory>(),
                LooseChannels = looseChannelsByGroup.TryGetValue(id, out var loose)
                    ? loose
                    : Array.Empty<Channel>(),
                MemberNames = membersByGroup.TryGetValue(id, out var members)
                    ? members
                    : Array.Empty<string>(),
            });
        }

        return groups;
    }

    public void Add(string name, string accent)
    {
        using var connection = Open();
        var groupId = InsertGroup(connection, name, null, accent);

        // 临时:新群组先塞 3 个未分组频道,免得进去一片空。接入频道管理后删掉。
        InsertChannel(connection, groupId, null, "大厅", ChannelKind.Text, 0);
        InsertChannel(connection, groupId, null, "闲聊", ChannelKind.Text, 0);
        InsertChannel(connection, groupId, null, "语音频道", ChannelKind.Voice, 20);
    }

    public void AddCategory(string groupId, string name)
    {
        using var connection = Open();
        InsertCategory(connection, groupId, name);
    }

    public void AddChannel(string groupId, string? categoryId, string name, ChannelKind kind)
    {
        using var connection = Open();
        InsertChannel(connection, groupId, categoryId, name, kind, kind == ChannelKind.Voice ? 20 : 0);
    }

    /// <summary>手动往群组的成员列表里加一个名字。</summary>
    public void AddMember(string groupId, string name)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO members (id, group_id, name, position) " +
            "VALUES ($id, $group, $name, " +
            "(SELECT COALESCE(MAX(position), 0) + 1 FROM members WHERE group_id = $group))";
        command.Parameters.AddWithValue("$id", NextId(connection, "members"));
        command.Parameters.AddWithValue("$group", groupId);
        command.Parameters.AddWithValue("$name", name);
        command.ExecuteNonQuery();
    }

    public void Unpin(string groupId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE groups SET pinned = 0 WHERE id = $id";
        command.Parameters.AddWithValue("$id", groupId);
        command.ExecuteNonQuery();
    }

    public void AddMessage(ChatMessage message)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT OR REPLACE INTO messages (id, channel_id, author_id, author, content, sent_at) " +
            "VALUES ($id, $channel, $authorId, $author, $content, $sent)";
        command.Parameters.AddWithValue("$id", message.Id);
        command.Parameters.AddWithValue("$channel", message.ChannelId);
        command.Parameters.AddWithValue("$authorId", message.AuthorId);
        command.Parameters.AddWithValue("$author", message.AuthorName);
        command.Parameters.AddWithValue("$content", message.Content);
        command.Parameters.AddWithValue("$sent", message.SentAt.ToUnixTimeMilliseconds());
        command.ExecuteNonQuery();
    }

    public IReadOnlyList<ChatMessage> GetMessages(string channelId, int take)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT id, author_id, author, content, sent_at FROM messages WHERE channel_id = $channel " +
            "ORDER BY sent_at DESC LIMIT $take";
        command.Parameters.AddWithValue("$channel", channelId);
        command.Parameters.AddWithValue("$take", take);

        using var reader = command.ExecuteReader();
        var rows = new List<ChatMessage>();

        while (reader.Read())
        {
            rows.Add(new ChatMessage(
                reader.GetString(0),
                channelId,
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(4))));
        }

        rows.Reverse();   // 先按时间倒序取最近 N 条,再翻回正序
        return rows;
    }

    public void DeleteChannel(string channelId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM channels WHERE id = $id";
        command.Parameters.AddWithValue("$id", channelId);
        command.ExecuteNonQuery();
    }

    public void Pin(string groupId)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "UPDATE groups SET pinned = 1, position = (SELECT COALESCE(MAX(position), 0) + 1 FROM groups) WHERE id = $id";
        command.Parameters.AddWithValue("$id", groupId);
        command.ExecuteNonQuery();
    }

    public void Delete(string groupId)
    {
        using var connection = Open();

        // 频道和分组跟着群组一起走
        foreach (var sql in new[]
                 {
                     "DELETE FROM members WHERE group_id = $id",
                     "DELETE FROM channels WHERE group_id = $id",
                     "DELETE FROM categories WHERE group_id = $id",
                     "DELETE FROM groups WHERE id = $id",
                 })
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("$id", groupId);
            command.ExecuteNonQuery();
        }
    }

    private static Channel ToChannel(ChannelRow row) => new()
    {
        Id = row.Id,
        Name = row.Name,
        Kind = row.Kind,
        Capacity = row.Capacity,
    };

    private static List<CategoryRow> ReadCategories(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, group_id, name FROM categories ORDER BY group_id, position";

        using var reader = command.ExecuteReader();
        var rows = new List<CategoryRow>();
        while (reader.Read())
        {
            rows.Add(new CategoryRow(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        }

        return rows;
    }

    private static Dictionary<string, List<string>> ReadMembers(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT group_id, name FROM members ORDER BY group_id, position";

        using var reader = command.ExecuteReader();
        var members = new Dictionary<string, List<string>>();
        while (reader.Read())
        {
            var groupId = reader.GetString(0);
            if (!members.TryGetValue(groupId, out var names))
            {
                names = new List<string>();
                members[groupId] = names;
            }

            names.Add(reader.GetString(1));
        }

        return members;
    }

    private static List<ChannelRow> ReadChannels(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT id, group_id, category_id, name, kind, capacity FROM channels ORDER BY group_id, position";

        using var reader = command.ExecuteReader();
        var rows = new List<ChannelRow>();
        while (reader.Read())
        {
            rows.Add(new ChannelRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3),
                (ChannelKind)reader.GetInt32(4),
                reader.GetInt32(5)));
        }

        return rows;
    }

    /// <summary>老库的 groups 表没有 pinned 列,补一列(建表语句只对新库生效)。</summary>
    private static void EnsurePinnedColumn(SqliteConnection connection)
    {
        using (var probe = connection.CreateCommand())
        {
            probe.CommandText = "SELECT COUNT(*) FROM pragma_table_info('groups') WHERE name = 'pinned'";

            if (Convert.ToInt64(probe.ExecuteScalar()) > 0)
            {
                return;
            }
        }

        using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE groups ADD COLUMN pinned INTEGER NOT NULL DEFAULT 0";
        alter.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void EnsureSchema()
    {
        using var connection = Open();

        var isNewDatabase = !TableExists(connection, "groups");

        foreach (var sql in new[]
        {
            CreateGroupsSql,
            CreateCategoriesSql,
            CreateChannelsSql,
            CreateMembersSql,
            CreateMetaSql,
            CreateMessagesSql,
        })
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        EnsurePinnedColumn(connection);

        if (isNewDatabase)
        {
            Seed(connection);
        }

        NormalizeIds(connection);
        EnsureDemoChannels(connection);
    }

    private static bool TableExists(SqliteConnection connection, string table)
    {
        using var probe = connection.CreateCommand();
        probe.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
        probe.Parameters.AddWithValue("$name", table);
        return Convert.ToInt64(probe.ExecuteScalar()) > 0;
    }

    /// <summary>老库里的 ID 是 seed1 / GUID 这种,统一换成纯数字。换完之后每次启动都是空转。</summary>
    private static void NormalizeIds(SqliteConnection connection)
    {
        var stale = new List<string>();
        using (var select = connection.CreateCommand())
        {
            select.CommandText = "SELECT id FROM groups WHERE id GLOB '*[^0-9]*'";
            using var reader = select.ExecuteReader();
            while (reader.Read())
            {
                stale.Add(reader.GetString(0));
            }
        }

        foreach (var oldId in stale)
        {
            var newId = NextId(connection, "groups");

            using (var update = connection.CreateCommand())
            {
                update.CommandText = "UPDATE groups SET id = $new WHERE id = $old";
                update.Parameters.AddWithValue("$new", newId);
                update.Parameters.AddWithValue("$old", oldId);
                update.ExecuteNonQuery();
            }

            // 分组和频道跟着换 group_id
            foreach (var table in new[] { "categories", "channels" })
            {
                using var move = connection.CreateCommand();
                move.CommandText = $"UPDATE {table} SET group_id = $new WHERE group_id = $old";
                move.Parameters.AddWithValue("$new", newId);
                move.Parameters.AddWithValue("$old", oldId);
                move.ExecuteNonQuery();
            }
        }
    }

    /// <summary>
    /// 演示用的分组和频道:两个分组各 4 个频道,再加一个未分组频道。
    /// 靠 meta 里的版本号只跑一次,而且只补带描述的演示群组 —— 用户自己建的群组保持空的,
    /// 否则新建的空群组下次启动就会被塞满频道。
    /// </summary>
    private static void EnsureDemoChannels(SqliteConnection connection)
    {
        if (ReadMeta(connection, "demoChannels") == DemoDataVersion.ToString())
        {
            return;
        }

        var groupIds = new List<string>();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT id FROM groups WHERE description IS NOT NULL";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                groupIds.Add(reader.GetString(0));
            }
        }

        foreach (var groupId in groupIds)
        {
            InsertDemoChannels(connection, groupId);
        }

        WriteMeta(connection, "demoChannels", DemoDataVersion.ToString());
    }

    /// <summary>两个分组各 4 个频道 + 一个未分组频道。按名字跳过已有的,重复跑也不会翻倍。</summary>
    private static void InsertDemoChannels(SqliteConnection connection, string groupId)
    {
        EnsureCategoryChannels(
            connection,
            groupId,
            "常规",
            new[] { "大厅", "闲聊", "资源", "活动" },
            ChannelKind.Text,
            0);

        EnsureCategoryChannels(
            connection,
            groupId,
            "语音",
            new[] { "语音频道", "开黑房", "听歌房", "挂机房" },
            ChannelKind.Voice,
            20);

        EnsureChannel(connection, groupId, null, "茶水间", ChannelKind.Text, 0);
    }

    private static void EnsureCategoryChannels(
        SqliteConnection connection,
        string groupId,
        string categoryName,
        string[] channelNames,
        ChannelKind kind,
        int capacity)
    {
        var categoryId = FindCategoryId(connection, groupId, categoryName)
            ?? InsertCategory(connection, groupId, categoryName);

        foreach (var name in channelNames)
        {
            EnsureChannel(connection, groupId, categoryId, name, kind, capacity);
        }
    }

    private static void EnsureChannel(
        SqliteConnection connection,
        string groupId,
        string? categoryId,
        string name,
        ChannelKind kind,
        int capacity)
    {
        using (var probe = connection.CreateCommand())
        {
            probe.CommandText = "SELECT COUNT(*) FROM channels WHERE group_id = $group AND name = $name";
            probe.Parameters.AddWithValue("$group", groupId);
            probe.Parameters.AddWithValue("$name", name);

            if (Convert.ToInt64(probe.ExecuteScalar()) > 0)
            {
                return;
            }
        }

        InsertChannel(connection, groupId, categoryId, name, kind, capacity);
    }

    private static string? FindCategoryId(SqliteConnection connection, string groupId, string name)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id FROM categories WHERE group_id = $group AND name = $name LIMIT 1";
        command.Parameters.AddWithValue("$group", groupId);
        command.Parameters.AddWithValue("$name", name);
        return command.ExecuteScalar() as string;
    }

    private static string? ReadMeta(SqliteConnection connection, string key)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM meta WHERE key = $key";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() as string;
    }

    private static void WriteMeta(SqliteConnection connection, string key, string value)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO meta (key, value) VALUES ($key, $value) " +
            "ON CONFLICT(key) DO UPDATE SET value = $value";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    /// <summary>9 位纯数字 ID,撞号就重摇;ID 只在各自表里唯一。</summary>
    private static string NextId(SqliteConnection connection, string table)
    {
        while (true)
        {
            var id = Random.Shared.Next(100_000_000, 1_000_000_000).ToString();
            using var probe = connection.CreateCommand();
            probe.CommandText = $"SELECT COUNT(*) FROM {table} WHERE id = $id";
            probe.Parameters.AddWithValue("$id", id);
            if (Convert.ToInt64(probe.ExecuteScalar()) == 0)
            {
                return id;
            }
        }
    }

    private static string InsertGroup(SqliteConnection connection, string name, string? description, string accent)
    {
        var id = NextId(connection, "groups");

        using var command = connection.CreateCommand();
        command.CommandText = InsertGroupSql;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$description", (object?)description ?? DBNull.Value);
        command.Parameters.AddWithValue("$accent", accent);
        command.ExecuteNonQuery();

        return id;
    }

    private static string InsertCategory(SqliteConnection connection, string groupId, string name)
    {
        var id = NextId(connection, "categories");

        using var command = connection.CreateCommand();
        command.CommandText = InsertCategorySql;
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$group", groupId);
        command.Parameters.AddWithValue("$name", name);
        command.ExecuteNonQuery();

        return id;
    }

    private static void InsertChannel(
        SqliteConnection connection,
        string groupId,
        string? categoryId,
        string name,
        ChannelKind kind,
        int capacity)
    {
        using var command = connection.CreateCommand();
        command.CommandText = InsertChannelSql;
        command.Parameters.AddWithValue("$id", NextId(connection, "channels"));
        command.Parameters.AddWithValue("$group", groupId);
        command.Parameters.AddWithValue("$category", (object?)categoryId ?? DBNull.Value);
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$kind", (int)kind);
        command.Parameters.AddWithValue("$capacity", capacity);
        command.ExecuteNonQuery();
    }

    /// <summary>演示群组。带描述是为了让 EnsureDemoChannels 认得出它们,频道由那边统一补。</summary>
    private static void Seed(SqliteConnection connection)
    {
        InsertGroup(connection, "开黑小队", "日常开黑与闲聊,氛围轻松", "#3B9EFF");
        InsertGroup(connection, "游戏开发", "项目讨论与技术交流", "#3DD68C");
        InsertGroup(connection, "音乐房", "一起听歌,共享耳机时刻", "#F5A623");
        InsertGroup(connection, "深夜电台", "凌晨两点的语音房,进来坐坐", "#F2555A");
    }

    private sealed record CategoryRow(string Id, string GroupId, string Name);

    private sealed record ChannelRow(string Id, string GroupId, string? CategoryId, string Name, ChannelKind Kind, int Capacity);
}
