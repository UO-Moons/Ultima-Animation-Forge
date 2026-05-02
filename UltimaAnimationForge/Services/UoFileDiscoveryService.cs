using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UltimaAnimationForge.Models;

namespace UltimaAnimationForge.Services;

public class UoFileDiscoveryService
{
    private static readonly Regex AnimMulRegex = new Regex(@"^anim(\d*)\.mul$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AnimationFrameRegex = new Regex(@"^AnimationFrame\d+\.uop$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public List<UoAnimationFile> FindAnimationFiles(string folderPath)
    {
        List<UoAnimationFile> files = new List<UoAnimationFile>();

        if (!Directory.Exists(folderPath))
        {
            return files;
        }

        AddDiscoveredMulFiles(files, folderPath);
        AddDiscoveredUopFiles(files, folderPath);

        return files
            .OrderBy(file => file.Type, StringComparer.OrdinalIgnoreCase)
            .ThenBy(file => GetSortKey(file.FileName), StringComparer.OrdinalIgnoreCase)
            .ThenBy(file => file.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void AddDiscoveredMulFiles(List<UoAnimationFile> files, string folderPath)
    {
        string[] mulPaths = Directory.GetFiles(folderPath, "*.mul", SearchOption.TopDirectoryOnly);

        foreach (string mulPath in mulPaths)
        {
            string fileName = Path.GetFileName(mulPath);
            if (!AnimMulRegex.IsMatch(fileName))
            {
                continue;
            }

            string idxPath = Path.ChangeExtension(mulPath, ".idx");
            if (!File.Exists(idxPath))
            {
                continue;
            }

            files.Add(new UoAnimationFile
            {
                FileName = fileName,
                FullPath = mulPath,
                Type = "MUL"
            });

            files.Add(new UoAnimationFile
            {
                FileName = Path.GetFileName(idxPath),
                FullPath = idxPath,
                Type = "MUL"
            });
        }
    }

    private void AddDiscoveredUopFiles(List<UoAnimationFile> files, string folderPath)
    {
        string[] uopPaths = Directory.GetFiles(folderPath, "*.uop", SearchOption.TopDirectoryOnly);

        foreach (string uopPath in uopPaths)
        {
            string fileName = Path.GetFileName(uopPath);
            if (!AnimationFrameRegex.IsMatch(fileName) &&
                !string.Equals(fileName, "AnimationSequence.uop", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            files.Add(new UoAnimationFile
            {
                FileName = fileName,
                FullPath = uopPath,
                Type = "UOP"
            });
        }
    }

    private string GetSortKey(string fileName)
    {
        if (string.Equals(fileName, "anim.mul", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, "anim.idx", StringComparison.OrdinalIgnoreCase))
        {
            return "anim0000";
        }

        Match match = AnimMulRegex.Match(fileName);
        if (match.Success)
        {
            string numberText = match.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(numberText))
            {
                return "anim0000";
            }

            if (int.TryParse(numberText, out int number))
            {
                return "anim" + number.ToString("D4");
            }
        }

        return fileName;
    }
}