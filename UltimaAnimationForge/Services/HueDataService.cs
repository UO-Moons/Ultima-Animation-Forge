using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UltimaAnimationForge.Services;

public sealed class HueDataService
{
    public sealed class HueEntry
    {
        public int HueId { get; set; }
        public string Name { get; set; } = string.Empty;
        public ushort TableStart { get; set; }
        public ushort TableEnd { get; set; }
        public List<Color> Colors { get; set; } = new List<Color>(32);

        public string DisplayText =>
            string.IsNullOrWhiteSpace(Name)
                ? HueId.ToString()
                : HueId + " - " + Name;
    }

    public List<HueEntry> LoadHueEntries(string hueFilePath)
    {
        List<HueEntry> hueEntries = new List<HueEntry>();

        if (string.IsNullOrWhiteSpace(hueFilePath) || !File.Exists(hueFilePath))
        {
            return hueEntries;
        }

        using FileStream fileStream = File.OpenRead(hueFilePath);
        using BinaryReader binaryReader = new BinaryReader(fileStream);

        int hueId = 0;

        while (fileStream.Position <= fileStream.Length - 708)
        {
            binaryReader.ReadInt32();

            for (int entryIndex = 0; entryIndex < 8; entryIndex++)
            {
                List<Color> colors = new List<Color>(32);

                for (int colorIndex = 0; colorIndex < 32; colorIndex++)
                {
                    ushort rawColor = binaryReader.ReadUInt16();
                    colors.Add(ConvertUoColorToAvaloniaColor(rawColor));
                }

                ushort tableStart = binaryReader.ReadUInt16();
                ushort tableEnd = binaryReader.ReadUInt16();

                byte[] nameBytes = binaryReader.ReadBytes(20);
                string hueName = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0', ' ');

                hueEntries.Add(new HueEntry
                {
                    HueId = hueId,
                    Name = hueName,
                    TableStart = tableStart,
                    TableEnd = tableEnd,
                    Colors = colors
                });

                hueId++;
            }
        }

        return hueEntries;
    }

    private static Color ConvertUoColorToAvaloniaColor(ushort rawColor)
    {
        int red = (rawColor >> 10) & 0x1F;
        int green = (rawColor >> 5) & 0x1F;
        int blue = rawColor & 0x1F;

        byte redByte = (byte)((red * 255) / 31);
        byte greenByte = (byte)((green * 255) / 31);
        byte blueByte = (byte)((blue * 255) / 31);

        return Color.FromRgb(redByte, greenByte, blueByte);
    }
}