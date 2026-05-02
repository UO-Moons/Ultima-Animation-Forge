using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace UltimaAnimationForge.Services;

public static class MythicDecompress
{
    public static byte[] Transform(byte[] buffer)
    {
        return MoveToFrontCoding.Encode(InternalCompress(buffer));
    }

    public static byte[] Detransform(byte[] buffer)
    {
        return InternalDecompress(MoveToFrontCoding.Decode(buffer));
    }

    public static byte[] Decompress(byte[] buffer)
    {
        using BinaryReader binaryReader = new BinaryReader(new MemoryStream(buffer));
        binaryReader.ReadUInt32();
        return InternalDecompress(MoveToFrontCoding.Decode(binaryReader.ReadBytes((int)(binaryReader.BaseStream.Length - 4L))));
    }

    public static byte[] InternalDecompress(ReadOnlySpan<byte> input)
    {
        try
        {
            Span<byte> table = stackalloc byte[256];
            Span<byte> frequencyOutput = stackalloc byte[256];
            Span<int> spans = stackalloc int[768];
            spans.Clear();

            for (int index = 0; index < 256; ++index)
            {
                table[index] = (byte)index;
            }

            input.Slice(0, 1024).CopyTo(MemoryMarshal.AsBytes(spans));

            int length = 0;
            for (int index = 0; index < 256; ++index)
            {
                length += spans[index];
            }

            if (length == 0)
            {
                return Array.Empty<byte>();
            }

            byte[] output = new byte[length];
            int outputIndex = 0;
            int element = 0;

            for (int index = 0; index < 256; ++index)
            {
                if (spans[index] != 0)
                {
                    ++element;
                }
            }

            Frequency(spans, frequencyOutput);

            int sortedIndex = 0;
            int runningCount = 0;

            for (; sortedIndex < element; ++sortedIndex)
            {
                byte value = frequencyOutput[sortedIndex];
                table[input[runningCount + 1024]] = value;
                spans[value + 256] = runningCount + 1;
                runningCount += spans[value];
                spans[value + 512] = runningCount;
            }

            byte current = table[0];

            if (length == 0)
            {
                return output;
            }

            do
            {
                ref int startRef = ref spans[current + 256];
                output[outputIndex] = current;

                if (startRef < spans[current + 512])
                {
                    byte nextValue = input[startRef + 1024];
                    ++startRef;

                    if (nextValue != 0)
                    {
                        ShiftLeft(table, nextValue);
                        table[nextValue] = current;
                        current = table[0];
                    }
                }
                else if (element-- > 0)
                {
                    ShiftLeft(table, element);
                    current = table[0];
                }

                ++outputIndex;
            }
            while (outputIndex < length);

            return output;
        }
        catch (Exception exception)
        {
            Console.WriteLine("Error during decompression: " + exception.Message);
            throw;
        }
    }

    private static void Frequency(ReadOnlySpan<int> input, Span<byte> output)
    {
        Span<int> working = stackalloc int[256];
        input.Slice(0, working.Length).CopyTo(working);

        for (int outputIndex = 0; outputIndex < 256; ++outputIndex)
        {
            uint highest = 0;
            byte foundIndex = 0;

            for (int index = 0; index < 256; ++index)
            {
                if ((uint)working[index] > highest)
                {
                    foundIndex = (byte)index;
                    highest = (uint)working[index];
                }
            }

            if (highest == 0)
            {
                break;
            }

            output[outputIndex] = foundIndex;
            working[foundIndex] = 0;
        }
    }

    private static void ShiftLeft(Span<byte> input, int element)
    {
        for (int index = 0; index < element; ++index)
        {
            input[index] = input[index + 1];
        }
    }

    public static byte[] InternalCompress(ReadOnlySpan<byte> input)
    {
        Span<byte> table = stackalloc byte[256];
        Span<byte> frequencyOutput = stackalloc byte[256];
        Span<int> spans = stackalloc int[768];

        for (int index = 0; index < input.Length; ++index)
        {
            ++spans[input[index]];
        }

        Frequency(spans, frequencyOutput);

        int nonZeroCount = 0;
        for (int index = 0; index < 256; ++index)
        {
            if (spans[index] != 0)
            {
                ++nonZeroCount;
            }
        }

        byte[] output = new byte[input.Length + nonZeroCount + 1024];
        int sortedIndex = 0;
        int runningCount = 0;

        for (; sortedIndex < nonZeroCount; ++sortedIndex)
        {
            byte value = frequencyOutput[sortedIndex];
            spans[value + 256] = runningCount + 1;
            runningCount += spans[value];
            spans[value + 512] = runningCount;
        }

        for (int index = 0; index < 256; ++index)
        {
            byte[] bytes = BitConverter.GetBytes(spans[index]);
            output[index * 4] = bytes[0];
            output[index * 4 + 1] = bytes[1];
            output[index * 4 + 2] = bytes[2];
            output[index * 4 + 3] = bytes[3];
        }

        int inputIndex = input.Length - 1;
        List<byte> seenValues = new List<byte>(256);

        do
        {
            byte value = input[inputIndex];
            ref int endRef = ref spans[value + 512];
            int outputIndex = endRef + 1024;

            if (!seenValues.Contains(value))
            {
                ShiftRight(table, seenValues.Count);
                table[0] = value;
                seenValues.Add(value);
                output[outputIndex] = 0;
            }
            else if (endRef >= spans[value + 256])
            {
                byte idx = GetIdx(table, value, seenValues.Count);
                ShiftRight(table, idx);
                table[0] = value;
                output[outputIndex] = idx;
            }

            --endRef;
            --inputIndex;
        }
        while (inputIndex >= 0);

        int outputStartIndex = 0;
        int runningOutputCount = 0;

        for (; outputStartIndex < nonZeroCount; ++outputStartIndex)
        {
            byte value = frequencyOutput[outputStartIndex];
            output[runningOutputCount + 1024] = GetIdx(table, value, nonZeroCount);
            runningOutputCount += spans[value];
        }

        return output;
    }

    private static byte GetIdx(ReadOnlySpan<byte> input, byte value, int nonZeroCount)
    {
        for (byte index = 0; index < input.Length && index < nonZeroCount; ++index)
        {
            if (input[index] == value)
            {
                return index;
            }
        }

        return 0;
    }

    private static void ShiftRight(Span<byte> input, int element)
    {
        for (int index = element; index >= 1; --index)
        {
            input[index] = input[index - 1];
        }
    }
}

public static class MoveToFrontCoding
{
    public static byte[] Encode(byte[] input)
    {
        Span<byte> table = stackalloc byte[256];
        byte[] output = new byte[input.Length];

        for (int index = 0; index < 256; ++index)
        {
            table[index] = (byte)index;
        }

        for (int index = 0; index < input.Length; ++index)
        {
            int front = MoveToFront(table, input[index]);
            output[index] = (byte)front;
        }

        return output;
    }

    public static byte[] Decode(byte[] input)
    {
        Span<byte> table = stackalloc byte[256];
        byte[] output = new byte[input.Length];

        for (int index = 0; index < 256; ++index)
        {
            table[index] = (byte)index;
        }

        for (int index = 0; index < input.Length; ++index)
        {
            int value = input[index];
            output[index] = table[value];
            MoveToFront(table, value);
        }

        return output;
    }

    private static int MoveToFront(Span<byte> table, byte element)
    {
        if (table[0] == element)
        {
            return 0;
        }

        int front = -1;

        for (int index = table.Length - 1; index > 0; --index)
        {
            if (table[index] == element)
            {
                front = index;
            }

            if (front != -1)
            {
                table[index] = table[index - 1];
            }
        }

        table[0] = element;
        return front;
    }

    private static void MoveToFront(Span<byte> table, int elementIndex)
    {
        byte value = table[elementIndex];

        for (int index = elementIndex; index > 0; --index)
        {
            table[index] = table[index - 1];
        }

        table[0] = value;
    }
}
