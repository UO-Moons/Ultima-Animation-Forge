using System.IO;
using System.Text.Json;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public sealed class MultiEditorRoofProfileService
{
    public MultiEditorRoofProfileConfig Load(string path)
    {
        if (!File.Exists(path))
        {
            return new MultiEditorRoofProfileConfig();
        }

        string json = File.ReadAllText(path);

        return JsonSerializer.Deserialize<MultiEditorRoofProfileConfig>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new MultiEditorRoofProfileConfig();
    }
}