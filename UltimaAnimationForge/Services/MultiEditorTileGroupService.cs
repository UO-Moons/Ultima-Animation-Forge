using System.IO;
using System.Text.Json;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public sealed class MultiEditorTileGroupService
{
    public MultiEditorTileGroupConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            return new MultiEditorTileGroupConfig();
        }

        string json = File.ReadAllText(path);

        return JsonSerializer.Deserialize<MultiEditorTileGroupConfig>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new MultiEditorTileGroupConfig();
    }
}