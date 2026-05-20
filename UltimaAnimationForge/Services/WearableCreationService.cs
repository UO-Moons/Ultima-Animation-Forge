using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public sealed class WearableCreationService
{
    private const ulong WeaponFlag = 1UL << 0;
    private const ulong PartialHueFlag = 1UL << 13;
    private const ulong WearableFlag = 1UL << 22;

    public sealed class Request
    {
        public bool PartialHue { get; set; }
        public string FolderPath { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Layer { get; set; } = string.Empty;

        public string MaleGumpImagePath { get; set; } = string.Empty;
        public string FemaleGumpImagePath { get; set; } = string.Empty;
        public string ArtImagePath { get; set; } = string.Empty;

        public bool CreateFemaleVariant { get; set; }

        public int MaleGumpId { get; set; }
        public int FemaleGumpId { get; set; }
        public int ArtId { get; set; }
        public int AnimationId { get; set; }
        public int ExistingAnimationId { get; set; }
        public string Hue { get; set; } = "0";
        public bool WriteBodyDef { get; set; } = true;
    }

    public sealed class Result
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public Result Apply(Request request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.FolderPath) || !Directory.Exists(request.FolderPath))
            {
                return Fail("Choose a valid UO folder first.");
            }

            if (string.IsNullOrWhiteSpace(request.MaleGumpImagePath) || !File.Exists(request.MaleGumpImagePath))
            {
                return Fail("Male/default paperdoll image is required.");
            }

            if (string.IsNullOrWhiteSpace(request.ArtImagePath) || !File.Exists(request.ArtImagePath))
            {
                return Fail("Inventory/world art image is required.");
            }

            List<string> messages = new();

            GumpDataService gumpService = new();
            if (!gumpService.Initialize(request.FolderPath))
            {
                return Fail("Could not load gump files.");
            }

            GumpSaveResult maleGumpResult = gumpService.ImportPngToSelectedGump(
                request.MaleGumpImagePath,
                request.MaleGumpId);

            messages.Add(maleGumpResult.Message);
            if (!maleGumpResult.Success)
            {
                return Fail(string.Join(Environment.NewLine, messages));
            }

            if (request.CreateFemaleVariant)
            {
                string femaleSource = !string.IsNullOrWhiteSpace(request.FemaleGumpImagePath) &&
                                      File.Exists(request.FemaleGumpImagePath)
                    ? request.FemaleGumpImagePath
                    : request.MaleGumpImagePath;

                GumpSaveResult femaleGumpResult = gumpService.ImportPngToSelectedGump(
                    femaleSource,
                    request.FemaleGumpId);

                messages.Add(femaleGumpResult.Message);
                if (!femaleGumpResult.Success)
                {
                    return Fail(string.Join(Environment.NewLine, messages));
                }
            }

            ArtDataService artService = new();
            if (!artService.Initialize(request.FolderPath))
            {
                return Fail("Could not load art files.");
            }

            ArtEntry artEntry = new()
            {
                ArtId = request.ArtId,
                FileIndex = 0x4000 + request.ArtId,
                Type = "Static",
                IsFreeSlot = true
            };

            if (!artService.ImportBitmapToArt(artEntry, request.ArtImagePath, out string artQueueMessage))
            {
                messages.Add(artQueueMessage);
                return Fail(string.Join(Environment.NewLine, messages));
            }

            messages.Add(artQueueMessage);

            if (!artService.SavePendingArtChanges(out string artSaveMessage))
            {
                messages.Add(artSaveMessage);
                return Fail(string.Join(Environment.NewLine, messages));
            }

            messages.Add(artSaveMessage);

            TileDataMulService tileDataService = new();
            string tileDataPath = Path.Combine(request.FolderPath, "tiledata.mul");
            List<TileDataEntry> tileDataEntries = tileDataService.Load(tileDataPath);

            TileDataEntry? itemEntry = tileDataEntries.FirstOrDefault(x => !x.IsLand && x.Id == request.ArtId);
            if (itemEntry == null)
            {
                return Fail("TileData item entry was not found for ID " + request.ArtId + ".");
            }

            itemEntry.Name = string.IsNullOrWhiteSpace(request.Name) ? "custom wearable" : request.Name.Trim();
            itemEntry.Flags |= WeaponFlag;
            itemEntry.Flags |= WearableFlag;
            if (request.PartialHue)
            {
                itemEntry.Flags |= PartialHueFlag;
            }
            else
            {
                itemEntry.Flags &= ~PartialHueFlag;
            }
            itemEntry.Animation = checked((short)request.AnimationId);
            itemEntry.Quality = GetLayerNumber(request.Layer);
            itemEntry.IsEdited = true;

            if (!tileDataService.SaveTileData(request.FolderPath, tileDataEntries, out string tileDataMessage))
            {
                messages.Add(tileDataMessage);
                return Fail(string.Join(Environment.NewLine, messages));
            }

            messages.Add(tileDataMessage);

            if (request.WriteBodyDef)
            {
                string bodyDefMessage = AddOrUpdateBodyDef(
                    request.FolderPath,
                    request.AnimationId,
                    request.ExistingAnimationId,
                    request.Hue);

                messages.Add(bodyDefMessage);
            }

            return new Result
            {
                Success = true,
                Message = string.Join(Environment.NewLine, messages)
            };
        }
        catch (Exception exception)
        {
            return Fail("Wearable wizard failed: " + exception.Message);
        }
    }

    private static byte GetLayerNumber(string layer)
    {
        if (string.IsNullOrWhiteSpace(layer))
        {
            return 0;
        }

        string value = layer.Trim();

        int dashIndex = value.IndexOf('-');
        string firstPart = dashIndex >= 0
            ? value.Substring(0, dashIndex).Trim()
            : value;

        if (firstPart.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            byte.TryParse(firstPart.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out byte hexLayer))
        {
            return hexLayer;
        }

        return value switch
        {
            "Shoes" => 0x03,
            "Pants" => 0x04,
            "Shirt" => 0x05,
            "Helm" => 0x06,
            "Gloves" => 0x07,
            "Waist" => 0x0C,
            "InnerTorso" => 0x0D,
            "MiddleTorso" => 0x11,
            "Arms" => 0x13,
            "Cloak" => 0x14,
            "OuterTorso" => 0x16,
            _ => 0
        };
    }

    private static string AddOrUpdateBodyDef(string folderPath, int animId, int existingAnimationId, string hue)
    {
        string bodyDefPath = Path.Combine(folderPath, "Body.def");
        string normalizedHue = string.IsNullOrWhiteSpace(hue) ? "0" : hue.Trim();
        string newLine = animId + " {" + existingAnimationId + "} " + normalizedHue;

        List<string> lines = File.Exists(bodyDefPath)
            ? File.ReadAllLines(bodyDefPath).ToList()
            : new List<string>();

        bool replaced = false;

        for (int i = 0; i < lines.Count; i++)
        {
            string trimmed = lines[i].Trim();

            if (trimmed.StartsWith("#") || trimmed.Length == 0)
            {
                continue;
            }

            string firstPart = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;

            if (int.TryParse(firstPart, out int existingId) && existingId == animId)
            {
                lines[i] = newLine;
                replaced = true;
                break;
            }
        }

        if (!replaced)
        {
            lines.Add(newLine);
        }

        string backupPath = bodyDefPath + ".bak";
        if (File.Exists(bodyDefPath) && !File.Exists(backupPath))
        {
            File.Copy(bodyDefPath, backupPath, false);
        }

        File.WriteAllLines(bodyDefPath, lines);

        return replaced
            ? "Updated Body.def: " + newLine
            : "Added Body.def: " + newLine;
    }

    private static Result Fail(string message)
    {
        return new Result
        {
            Success = false,
            Message = message
        };
    }
}
