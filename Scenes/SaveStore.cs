using Godot;
using FileAccess = Godot.FileAccess;

namespace SevenSpices;

/// <summary>读档结果：区分「成功」「文件不存在」「文件存在但读取失败」三种情况。</summary>
public enum SaveReadResult
{
    /// <summary>读取成功，<c>json</c> 为文件内容。</summary>
    Success,

    /// <summary>存档文件不存在。</summary>
    NotFound,

    /// <summary>存档文件存在但打开 / 读取失败（内容为空或不可读）。</summary>
    Failed,
}

/// <summary>
/// 存档文件读写（表现层）。使用 Godot <see cref="FileAccess"/> 读写 <c>user://</c> 下的 JSON 文件。
/// IO 失败不抛给调用方：<see cref="Read"/> 用结果码区分「不存在 / 读取失败」，
/// <see cref="Write"/> 返回 <c>false</c> 表示写盘失败，调用方据此给出正确反馈。
/// </summary>
public static class SaveStore
{
    /// <summary>存档文件路径（Godot user:// 目录）。</summary>
    public const string SavePath = "user://seven_spices_save.json";

    /// <summary>存档文件是否存在。</summary>
    public static bool Exists => FileAccess.FileExists(SavePath);

    /// <summary>读取存档内容；<paramref name="json"/> 仅在返回 <see cref="SaveReadResult.Success"/> 时有效。</summary>
    public static SaveReadResult Read(out string? json)
    {
        json = null;

        if (!FileAccess.FileExists(SavePath))
            return SaveReadResult.NotFound;

        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PushWarning($"SaveStore: failed to open save for read ({FileAccess.GetOpenError()}).");
            return SaveReadResult.Failed;
        }

        string text = file.GetAsText();
        if (string.IsNullOrEmpty(text))
        {
            GD.PushWarning("SaveStore: save file exists but is empty or unreadable.");
            return SaveReadResult.Failed;
        }

        json = text;
        return SaveReadResult.Success;
    }

    /// <summary>写入存档内容（覆盖）；打开 / 写入失败时记录警告并返回 <c>false</c>。</summary>
    public static bool Write(string json)
    {
        using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PushWarning($"SaveStore: failed to open save for write ({FileAccess.GetOpenError()}).");
            return false;
        }

        file.StoreString(json);

        Error error = file.GetError();
        if (error != Error.Ok)
        {
            GD.PushWarning($"SaveStore: failed to write save ({error}).");
            return false;
        }

        return true;
    }

    /// <summary>删除存档文件；不存在或删除失败时静默处理。</summary>
    public static void Delete()
    {
        if (!FileAccess.FileExists(SavePath))
            return;

        Error error = DirAccess.RemoveAbsolute(SavePath);
        if (error != Error.Ok)
            GD.PushWarning($"SaveStore: failed to delete save ({error}).");
    }
}
