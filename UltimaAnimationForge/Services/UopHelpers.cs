using System;
using System.IO;
using System.IO.Compression;

namespace UltimaAnimationForge.Services;

public static class UopHashUtility
{
    public static ulong HashFileName(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        uint eax;
        uint ecx = eax = 0;
        uint ebx = (uint)(value.Length - 0xDEADBEEF);
        uint edi = ebx;
        uint esi = ebx;

        int index;
        for (index = 0; index + 12 < value.Length; index += 12)
        {
            edi = (uint)((value[index + 7] << 24) | (value[index + 6] << 16) | (value[index + 5] << 8) | value[index + 4]) + edi;
            esi = (uint)((value[index + 11] << 24) | (value[index + 10] << 16) | (value[index + 9] << 8) | value[index + 8]) + esi;
            uint num = ((uint)((value[index + 3] << 24) | (value[index + 2] << 16) | (value[index + 1] << 8) | value[index]) - esi) + ebx ^ (esi >> 28) ^ (esi << 4);
            uint num2 = esi + edi;
            uint num3 = (edi - num) ^ (num >> 26) ^ (num << 6);
            uint num4 = num + num2;
            uint num5 = (num2 - num3) ^ (num3 >> 24) ^ (num3 << 8);
            uint num6 = num3 + num4;
            uint num7 = (num4 - num5) ^ (num5 >> 16) ^ (num5 << 16);
            uint num8 = num5 + num6;
            uint num9 = (num6 - num7) ^ (num7 >> 13) ^ (num7 << 19);

            ebx = num7 + num8;
            esi = (num8 - num9) ^ (num9 >> 28) ^ (num9 << 4);
            edi = num9 + ebx;
        }

        if (value.Length - index > 0)
        {
            switch (value.Length - index)
            {
                case 12: esi += (uint)value[index + 11] << 24; goto case 11;
                case 11: esi += (uint)value[index + 10] << 16; goto case 10;
                case 10: esi += (uint)value[index + 9] << 8; goto case 9;
                case 9: esi += value[index + 8]; goto case 8;
                case 8: edi += (uint)value[index + 7] << 24; goto case 7;
                case 7: edi += (uint)value[index + 6] << 16; goto case 6;
                case 6: edi += (uint)value[index + 5] << 8; goto case 5;
                case 5: edi += value[index + 4]; goto case 4;
                case 4: ebx += (uint)value[index + 3] << 24; goto case 3;
                case 3: ebx += (uint)value[index + 2] << 16; goto case 2;
                case 2: ebx += (uint)value[index + 1] << 8; goto case 1;
                case 1: ebx += value[index + 0]; break;
            }

            uint num10 = (esi ^ edi) - ((edi >> 18) ^ (edi << 14));
            uint num11 = (num10 ^ ebx) - ((num10 >> 21) ^ (num10 << 11));
            uint num12 = (edi ^ num11) - ((num11 >> 7) ^ (num11 << 25));
            uint num13 = (num10 ^ num12) - ((num12 >> 16) ^ (num12 << 16));
            uint num14 = (num13 ^ num11) - ((num13 >> 28) ^ (num13 << 4));
            uint num15 = (num12 ^ num14) - ((num14 >> 18) ^ (num14 << 14));
            ecx = (num13 ^ num15) - ((num15 >> 8) ^ (num15 << 24));
            eax = num15;
        }

        return ((ulong)eax << 32) | ecx;
    }
}

public static class UopUtils
{
    public static ulong HashFileName(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return 0;
        }

        uint eax;
        uint ecx = eax = 0;
        uint ebx = (uint)(value.Length - 0xDEADBEEF);
        uint edi = ebx;
        uint esi = ebx;

        int index;
        for (index = 0; index + 12 < value.Length; index += 12)
        {
            edi = (uint)((value[index + 7] << 24) | (value[index + 6] << 16) | (value[index + 5] << 8) | value[index + 4]) + edi;
            esi = (uint)((value[index + 11] << 24) | (value[index + 10] << 16) | (value[index + 9] << 8) | value[index + 8]) + esi;

            uint num9 = (uint)((((uint)((value[index + 3] << 24) | (value[index + 2] << 16) | (value[index + 1] << 8) | value[index]) - esi) + ebx) ^ (esi >> 28) ^ (esi << 4));
            uint num10 = esi + edi;
            uint num11 = (uint)((edi - num9) ^ (num9 >> 26) ^ (num9 << 6));
            uint num12 = num9 + num10;
            uint num13 = (uint)((num10 - num11) ^ (num11 >> 24) ^ (num11 << 8));
            uint num14 = num11 + num12;
            uint num15 = (uint)((num12 - num13) ^ (num13 >> 16) ^ (num13 << 16));
            uint num16 = num13 + num14;
            uint num17 = (uint)((num14 - num15) ^ (num15 >> 13) ^ (num15 << 19));

            ebx = num15 + num16;
            esi = (uint)((num16 - num17) ^ (num17 >> 28) ^ (num17 << 4));
            edi = num17 + ebx;
        }

        if (value.Length - index > 0)
        {
            switch (value.Length - index)
            {
                case 12:
                    esi += (uint)value[index + 11] << 24;
                    goto case 11;
                case 11:
                    esi += (uint)value[index + 10] << 16;
                    goto case 10;
                case 10:
                    esi += (uint)value[index + 9] << 8;
                    goto case 9;
                case 9:
                    esi += value[index + 8];
                    goto case 8;
                case 8:
                    edi += (uint)value[index + 7] << 24;
                    goto case 7;
                case 7:
                    edi += (uint)value[index + 6] << 16;
                    goto case 6;
                case 6:
                    edi += (uint)value[index + 5] << 8;
                    goto case 5;
                case 5:
                    edi += value[index + 4];
                    goto case 4;
                case 4:
                    ebx += (uint)value[index + 3] << 24;
                    goto case 3;
                case 3:
                    ebx += (uint)value[index + 2] << 16;
                    goto case 2;
                case 2:
                    ebx += (uint)value[index + 1] << 8;
                    goto case 1;
                case 1:
                    ebx += value[index + 0];
                    break;
            }

            uint num18 = (uint)((esi ^ edi) - ((edi >> 18) ^ (edi << 14)));
            uint num19 = (uint)((num18 ^ ebx) - ((num18 >> 21) ^ (num18 << 11)));
            uint num20 = (uint)((edi ^ num19) - ((num19 >> 7) ^ (num19 << 25)));
            uint num21 = (uint)((num18 ^ num20) - ((num20 >> 16) ^ (num20 << 16)));
            uint num22 = (uint)((num21 ^ num19) - ((num21 >> 28) ^ (num21 << 4)));
            uint num23 = (uint)((num20 ^ num22) - ((num22 >> 18) ^ (num22 << 14)));
            uint num24 = (uint)((num21 ^ num23) - ((num23 >> 8) ^ (num23 << 24)));

            eax = num23;
            ecx = num24;
        }

        return ((ulong)eax << 32) | ecx;
    }

    public static (bool success, byte[] data) Decompress(byte[] compressedData)
    {
        if (compressedData == null || compressedData.Length == 0)
        {
            return (false, Array.Empty<byte>());
        }

        try
        {
            using MemoryStream memoryStream = new MemoryStream(compressedData);
            using ZLibStream zlibStream = new ZLibStream(memoryStream, CompressionMode.Decompress, false);
            using MemoryStream destination = new MemoryStream();

            zlibStream.CopyTo(destination);
            destination.Flush();

            return (true, destination.ToArray());
        }
        catch
        {
            return (false, Array.Empty<byte>());
        }
    }

    public static (bool success, byte[] compressedData) Compress(byte[] rawData)
    {
        if (rawData == null || rawData.Length == 0)
        {
            return (false, Array.Empty<byte>());
        }

        try
        {
            using MemoryStream input = new MemoryStream(rawData);
            using MemoryStream output = new MemoryStream();
            using (ZLibStream destination = new ZLibStream(output, CompressionLevel.Optimal, false))
            {
                input.CopyTo(destination);
                destination.Flush();
            }

            return (true, output.ToArray());
        }
        catch
        {
            return (false, Array.Empty<byte>());
        }
    }
}
