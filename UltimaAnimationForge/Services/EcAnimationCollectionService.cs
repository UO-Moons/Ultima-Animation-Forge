using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public sealed class EcAnimationCollectionService
{
    private readonly List<EcAnimationCollectionEntry> entries = new();

    public IReadOnlyList<EcAnimationCollectionEntry> Entries => entries;

    public bool IsLoaded => entries.Count > 0;

    public bool Load(string xmlPath, out string message)
    {
        entries.Clear();

        if (string.IsNullOrWhiteSpace(xmlPath) || !File.Exists(xmlPath))
        {
            message = "Animation collection XML was not found.";
            return false;
        }

        try
        {
            XDocument document = XDocument.Load(xmlPath);

            foreach (XElement item in document.Descendants("Item"))
            {
                int bodyId = ReadInt(item, "Id", -1);
                if (bodyId < 0)
                {
                    continue;
                }

                string bodyName = ReadString(item, "Name");
                int bodyType = ReadInt(item, "Type", -1);
                int layer = ReadInt(item, "Layer", -1);

                foreach (XElement animation in item.Elements("Animation"))
                {
                    int actionId = ReadInt(animation, "Id", -1);
                    string uop = ReadString(animation, "UOP");
                    int block = ReadInt(animation, "Block", -1);
                    int file = ReadInt(animation, "File", -1);

                    if (actionId < 0 || block < 0 || file < 0 || string.IsNullOrWhiteSpace(uop))
                    {
                        continue;
                    }

                    entries.Add(new EcAnimationCollectionEntry
                    {
                        BodyId = bodyId,
                        BodyName = bodyName,
                        BodyType = bodyType,
                        Layer = layer,
                        ActionId = actionId,
                        UopFileName = Path.GetFileName(uop),
                        BlockIndex = block,
                        FileIndex = file
                    });
                }
            }

            message = "Loaded EC animation collection with " + entries.Count + " animation records.";
            return entries.Count > 0;
        }
        catch (Exception exception)
        {
            entries.Clear();
            message = "Failed to load animation collection XML: " + exception.Message;
            return false;
        }
    }

    public List<EcAnimationCollectionEntry> GetEntriesForUop(string uopFileName)
    {
        string wanted = Path.GetFileName(uopFileName);

        return entries
            .Where(x => string.Equals(Path.GetFileName(x.UopFileName), wanted, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static string ReadString(XElement element, string name)
    {
        return element.Attribute(name)?.Value?.Trim() ?? string.Empty;
    }

    private static int ReadInt(XElement element, string name, int fallback)
    {
        string value = ReadString(element, name);

        if (int.TryParse(value, out int result))
        {
            return result;
        }

        return fallback;
    }
}