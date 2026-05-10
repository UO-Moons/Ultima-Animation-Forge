using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.IO;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public sealed class EquipmentBinderService
{
    private readonly BodyDefService bodyDefService = new();
    private readonly BodyConvAssignmentService bodyConvAssignmentService = new();
    private readonly MobTypeAssignmentService mobTypeAssignmentService = new();
    private readonly TileDataService tileDataService = new();

    public EquipmentBinderPreview BuildPreview(
        string uoFolderPath,
        int itemArtId,
        int displayBodyId,
        int animationBodyId,
        MulSlotEntry targetSlot,
        string equipmentName)
    {
        if (targetSlot == null)
        {
            throw new InvalidOperationException("Target MUL slot is required.");
        }

        string bodyDefPath = Path.Combine(uoFolderPath, "body.def");
        string bodyConvPath = Path.Combine(uoFolderPath, "bodyconv.def");
        string mobTypesPath = Path.Combine(uoFolderPath, "mobtypes.txt");

        string cleanedName = string.IsNullOrWhiteSpace(equipmentName)
            ? "equipment"
            : equipmentName.Trim();

        bool bodyDefExists = bodyDefService.EntryExists(bodyDefPath, displayBodyId);
        bool bodyConvExists = bodyConvAssignmentService.BodyIdExists(bodyConvPath, animationBodyId);
        bool mobTypeExists = mobTypeAssignmentService.BodyIdExists(mobTypesPath, animationBodyId);

        BodyConvAssignmentService.PreviewResult bodyConvPreview =
            bodyConvAssignmentService.BuildPreview(
                bodyConvPath,
                animationBodyId,
                targetSlot.FileType,
                targetSlot.BodyIndex,
                cleanedName);

        string bodyDefLine = bodyDefService.BuildLine(displayBodyId, animationBodyId, 0, cleanedName);
        string mobTypeLine = mobTypeAssignmentService.BuildPreviewLine(animationBodyId, "EQUIPMENT", cleanedName);
        string tileDataLine = tileDataService.BuildPreviewLine(itemArtId, displayBodyId);
        return new EquipmentBinderPreview
        {
            ItemArtId = itemArtId,
            TileDataAnimationId = displayBodyId,

            DisplayBodyId = displayBodyId,
            AnimationBodyId = animationBodyId,

            FileType = targetSlot.FileType,
            SlotBodyIndex = targetSlot.BodyIndex,

            EquipmentName = cleanedName,
            MobType = "EQUIPMENT",

            BodyDefLine = bodyDefLine,
            BodyConvLine = bodyConvPreview.PreviewLine,
            MobTypeLine = mobTypeLine,

            BodyDefExists = bodyDefExists,
            BodyConvExists = bodyConvExists,
            MobTypeExists = mobTypeExists,
            TileDataLine = tileDataLine,

            Summary =
                "Item art " + itemArtId +
                " should use TileData animation/display body " + displayBodyId +
                ", body.def redirects " + displayBodyId + " -> " + animationBodyId +
                ", and bodyconv maps " + animationBodyId +
                " to " + targetSlot.FileName +
                " body slot " + targetSlot.BodyIndex + "."
        };
    }

    public EquipmentBinderResult Apply(
        string uoFolderPath,
        int itemArtId,
        int displayBodyId,
        int animationBodyId,
        MulSlotEntry targetSlot,
        string equipmentName,
        bool overwriteBodyDef = true,
        bool overwriteMobType = true,
        bool updateTileDataAnimation = true)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(uoFolderPath) || !Directory.Exists(uoFolderPath))
            {
                return Fail("UO folder path is invalid.");
            }

            if (targetSlot == null)
            {
                return Fail("Target MUL slot is required.");
            }

            if (displayBodyId < 0 || displayBodyId > 65534)
            {
                return Fail("Display body/gump ID must be between 0 and 65534.");
            }

            if (animationBodyId < 1)
            {
                return Fail("Animation body ID must be 1 or greater.");
            }

            string bodyDefPath = Path.Combine(uoFolderPath, "body.def");
            string bodyConvPath = Path.Combine(uoFolderPath, "bodyconv.def");
            string mobTypesPath = Path.Combine(uoFolderPath, "mobtypes.txt");

            string cleanedName = string.IsNullOrWhiteSpace(equipmentName)
                ? "equipment"
                : equipmentName.Trim();

            if (bodyDefService.EntryExists(bodyDefPath, displayBodyId) && !overwriteBodyDef)
            {
                return Fail("Body.def already has display body " + displayBodyId + ".");
            }

            bodyDefService.AddOrUpdateEntry(
                bodyDefPath,
                displayBodyId,
                animationBodyId,
                0,
                cleanedName);

            if (!bodyConvAssignmentService.BodyIdExists(bodyConvPath, animationBodyId))
            {
                bodyConvAssignmentService.AddNewEntry(
                    bodyConvPath,
                    animationBodyId,
                    targetSlot.FileType,
                    targetSlot.BodyIndex,
                    cleanedName);
            }

            if (!mobTypeAssignmentService.BodyIdExists(mobTypesPath, animationBodyId) || overwriteMobType)
            {
                mobTypeAssignmentService.AddOrUpdateEntry(
                    mobTypesPath,
                    animationBodyId,
                    "EQUIPMENT",
                    cleanedName);
            }

            if (updateTileDataAnimation)
            {
                string tileDataPath = Path.Combine(uoFolderPath, "tiledata.mul");

                TileDataService.TileDataItemAnimationInfo tileResult =
                    tileDataService.WriteItemAnimation(tileDataPath, itemArtId, displayBodyId);

                if (!tileResult.Success)
                {
                    return Fail(tileResult.Message);
                }
            }

            return new EquipmentBinderResult
            {
                Success = true,
                Message =
                    "Equipment binder applied. Item art " + itemArtId +
                    " should now use TileData animation " + displayBodyId +
                    ", body.def redirects to animation body " + animationBodyId +
                    ", and bodyconv points that body to " + targetSlot.FileName +
                    " slot " + targetSlot.BodyIndex + "."
            };
        }
        catch (Exception exception)
        {
            return Fail("Equipment binder failed: " + exception.Message);
        }
    }

    public int FindNextFreeDisplayBodyId(string uoFolderPath, int startAt = 4000)
    {
        string bodyDefPath = Path.Combine(uoFolderPath, "body.def");
        return bodyDefService.FindNextFreeDisplayBodyId(bodyDefPath, startAt);
    }

    private static EquipmentBinderResult Fail(string message)
    {
        return new EquipmentBinderResult
        {
            Success = false,
            Message = message
        };
    }
}