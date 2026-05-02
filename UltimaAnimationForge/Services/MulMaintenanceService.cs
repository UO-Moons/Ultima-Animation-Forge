using System;
using System.Collections.Generic;
using System.IO;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public sealed class MulSlotDeleteService
{
    public sealed class DeleteResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public DeleteResult DeleteBodySlot(string uoFolderPath, string idxFileName, int fileType, int bodyIndex, int animLength)
    {
        if (string.IsNullOrWhiteSpace(uoFolderPath) || !Directory.Exists(uoFolderPath))
        {
            return Fail("UO folder path is invalid.");
        }

        if (bodyIndex < 0)
        {
            return Fail("Body index is invalid.");
        }

        if (animLength != 13 && animLength != 22 && animLength != 35)
        {
            return Fail("Animation length is invalid.");
        }

        if (string.IsNullOrWhiteSpace(idxFileName))
        {
            return Fail("IDX file name is invalid.");
        }

        string idxPath = Path.Combine(uoFolderPath, idxFileName);

        if (!File.Exists(idxPath))
        {
            return Fail("IDX file not found: " + Path.GetFileName(idxPath));
        }

        try
        {
            List<AnimationIdxEntry> entries = ReadIdxEntries(idxPath);

            int expectedAnimLength = GetAnimLengthForFileType(fileType, bodyIndex);
            if (expectedAnimLength != animLength)
            {
                animLength = expectedAnimLength;
            }

            int baseIndex = GetBaseIndexForBody(fileType, bodyIndex);
            int slotCount = animLength * 5;

            if (baseIndex < 0 || baseIndex >= entries.Count)
            {
                return Fail("Body slot is outside IDX range.");
            }

            int endIndex = Math.Min(baseIndex + slotCount, entries.Count);

            for (int i = baseIndex; i < endIndex; i++)
            {
                entries[i].Offset = -1;
                entries[i].Length = -1;
                entries[i].Extra = -1;
            }

            WriteIdxEntries(idxPath, entries);

            return new DeleteResult
            {
                Success = true,
                Message =
                    "Deleted animation slot from " + Path.GetFileName(idxPath) +
                    " at body index " + bodyIndex +
                    " (base index " + baseIndex + ")."
            };
        }
        catch (UnauthorizedAccessException)
        {
            return Fail("Access denied while updating IDX file.");
        }
        catch (IOException exception)
        {
            return Fail("I/O error while deleting slot: " + exception.Message);
        }
        catch (Exception exception)
        {
            return Fail("Failed to delete slot: " + exception.Message);
        }
    }

    private static List<AnimationIdxEntry> ReadIdxEntries(string idxPath)
    {
        List<AnimationIdxEntry> entries = new List<AnimationIdxEntry>();

        using BinaryReader reader = new BinaryReader(File.Open(idxPath, FileMode.Open, FileAccess.Read, FileShare.Read));

        int index = 0;
        while (reader.BaseStream.Position + 12 <= reader.BaseStream.Length)
        {
            entries.Add(new AnimationIdxEntry
            {
                Offset = reader.ReadInt32(),
                Length = reader.ReadInt32(),
                Extra = reader.ReadInt32(),
                Index = index
            });

            index++;
        }

        return entries;
    }

    private static void WriteIdxEntries(string idxPath, List<AnimationIdxEntry> entries)
    {
        using BinaryWriter writer = new BinaryWriter(File.Open(idxPath, FileMode.Create, FileAccess.Write, FileShare.None));

        foreach (AnimationIdxEntry entry in entries)
        {
            writer.Write(entry.Offset);
            writer.Write(entry.Length);
            writer.Write(entry.Extra);
        }
    }

    private static int GetBaseIndexForBody(int fileType, int bodyIndex)
    {
        return fileType switch
        {
            2 => bodyIndex < 200
                ? bodyIndex * 110
                : 22000 + ((bodyIndex - 200) * 65),

            3 => bodyIndex < 300
                ? bodyIndex * 65
                : bodyIndex < 400
                    ? 33000 + ((bodyIndex - 300) * 110)
                    : 35000 + ((bodyIndex - 400) * 175),

            4 => bodyIndex < 200
                ? bodyIndex * 110
                : bodyIndex < 400
                    ? 22000 + ((bodyIndex - 200) * 65)
                    : 35000 + ((bodyIndex - 400) * 175),

            5 => (bodyIndex < 200 && bodyIndex != 34)
                ? bodyIndex * 110
                : bodyIndex < 400
                    ? 22000 + ((bodyIndex - 200) * 65)
                    : 35000 + ((bodyIndex - 400) * 175),

            // anim, anim6, anim7, anim8, etc. use standard CUO layout
            _ => bodyIndex < 200
                ? bodyIndex * 110
                : bodyIndex < 400
                    ? 22000 + ((bodyIndex - 200) * 65)
                    : 35000 + ((bodyIndex - 400) * 175)
        };
    }

    private static int GetAnimLengthForFileType(int fileType, int bodyIndex)
    {
        return fileType switch
        {
            2 => bodyIndex < 200 ? 22 : 13,
            3 => bodyIndex < 300 ? 13 : bodyIndex < 400 ? 22 : 35,
            4 => bodyIndex < 200 ? 22 : bodyIndex < 400 ? 13 : 35,
            5 => bodyIndex < 200 ? 22 : bodyIndex < 400 ? 13 : 35,
            _ => bodyIndex < 200 ? 22 : bodyIndex < 400 ? 13 : 35
        };
    }

    private static DeleteResult Fail(string message)
    {
        return new DeleteResult
        {
            Success = false,
            Message = message
        };
    }
}

public sealed class LegacyMulIdxCreationService
{
    public sealed class CreateResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public CreateResult CreateEmptyLegacyMulIdx(
        string folderPath,
        string baseFileName,
        char typeLetter,
        int startBody,
        int endBody)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            return Fail("UO folder path is invalid.");
        }

        string trimmedBaseFileName = (baseFileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmedBaseFileName))
        {
            return Fail("Base file name is required.");
        }

        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            if (trimmedBaseFileName.Contains(invalidChar))
            {
                return Fail("Base file name contains invalid characters.");
            }
        }

        if (startBody < 0 || endBody < startBody)
        {
            return Fail("Body range is invalid.");
        }
        string normalizedBaseName = trimmedBaseFileName;
        if (normalizedBaseName.EndsWith(".mul", StringComparison.OrdinalIgnoreCase))
        {
            normalizedBaseName = normalizedBaseName[..^4];
        }
        else if (normalizedBaseName.EndsWith(".idx", StringComparison.OrdinalIgnoreCase))
        {
            normalizedBaseName = normalizedBaseName[..^4];
        }

        bool isAnimSeries = string.Equals(normalizedBaseName, "anim", StringComparison.OrdinalIgnoreCase) ||
                            (normalizedBaseName.StartsWith("anim", StringComparison.OrdinalIgnoreCase) &&
                             int.TryParse(normalizedBaseName.Substring(4), out int parsedNumber) &&
                             parsedNumber > 0);

        long entryCount;

        if (isAnimSeries)
        {
            int highestIndexExclusive = 0;

            for (int bodyIndex = startBody; bodyIndex <= endBody; bodyIndex++)
            {
                int animLength = GetAnimLengthForStandardLayout(bodyIndex);
                int baseIndex = GetBaseIndexForStandardLayout(bodyIndex);
                int endIndexExclusive = baseIndex + (animLength * 5);

                if (endIndexExclusive > highestIndexExclusive)
                {
                    highestIndexExclusive = endIndexExclusive;
                }
            }

            entryCount = highestIndexExclusive;
        }
        else
        {
            int animLength = char.ToUpperInvariant(typeLetter) switch
            {
                'L' => 13,
                'H' => 22,
                'P' => 35,
                _ => -1
            };

            if (animLength <= 0)
            {
                return Fail("Type must be H, L, or P.");
            }

            int slotsPerBody = animLength * 5;
            entryCount = ((long)endBody + 1L) * slotsPerBody;
        }

        if (entryCount <= 0 || entryCount > int.MaxValue)
        {
            return Fail("Requested IDX size is too large.");
        }

        string mulPath = Path.Combine(folderPath, normalizedBaseName + ".mul");
        string idxPath = Path.Combine(folderPath, normalizedBaseName + ".idx");

        if (File.Exists(mulPath) || File.Exists(idxPath))
        {
            return Fail("Target file already exists. Choose a different base file name.");
        }

        try
        {
            using (FileStream mulStream = new FileStream(mulPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
            }

            using (BinaryWriter writer = new BinaryWriter(new FileStream(idxPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)))
            {
                for (int index = 0; index < entryCount; index++)
                {
                    writer.Write(-1);
                    writer.Write(-1);
                    writer.Write(-1);
                }
            }

            return new CreateResult
            {
                Success = true,
                Message = isAnimSeries
                    ? "Created empty CUO-compatible " + normalizedBaseName + ".mul / " + normalizedBaseName + ".idx " +
                      "covering body IDs " + startBody + " through " + endBody + "."
                    : "Created empty fixed-layout " + normalizedBaseName + ".mul / " + normalizedBaseName + ".idx " +
                      "for type " + char.ToUpperInvariant(typeLetter) +
                      " covering body IDs " + startBody + " through " + endBody + "."
            };
        }
        catch (UnauthorizedAccessException)
        {
            return Fail("Access denied while creating MUL/IDX files.");
        }
        catch (IOException exception)
        {
            return Fail("I/O error creating MUL/IDX files: " + exception.Message);
        }
        catch (Exception exception)
        {
            return Fail("Failed to create MUL/IDX files: " + exception.Message);
        }
    }

    private static CreateResult Fail(string message)
    {
        return new CreateResult
        {
            Success = false,
            Message = message
        };
    }

    private static int GetAnimLengthForStandardLayout(int bodyIndex)
    {
        return bodyIndex < 200 ? 22 : bodyIndex < 400 ? 13 : 35;
    }

    private static int GetBaseIndexForStandardLayout(int bodyIndex)
    {
        return bodyIndex < 200
            ? bodyIndex * 110
            : bodyIndex < 400
                ? 22000 + ((bodyIndex - 200) * 65)
                : 35000 + ((bodyIndex - 400) * 175);
    }
}
