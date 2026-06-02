using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UltimaAnimationForge.Models;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace UltimaAnimationForge.Services;

public sealed class SoundDataService
{
    private const int MaxSounds = 0xFFF;
    private const int NameHeaderLength = 32;
    private const int WavHeaderLength = 44;
    private const int SampleRate = 22050;

    private readonly Dictionary<int, int> translations = new();
    private readonly Dictionary<int, SoundEntry> entries = new();
    private readonly Dictionary<int, byte[]> wavCache = new();
    private readonly HashSet<int> removed = new();

    private string folderPath = string.Empty;
    private string idxPath = string.Empty;
    private string mulPath = string.Empty;

    public IReadOnlyDictionary<int, SoundEntry> Entries => entries;

    public bool Load(string uoFolderPath)
    {
        entries.Clear();
        wavCache.Clear();
        removed.Clear();
        translations.Clear();

        folderPath = uoFolderPath ?? string.Empty;
        idxPath = Path.Combine(folderPath, "soundidx.mul");
        mulPath = Path.Combine(folderPath, "sound.mul");

        if (!File.Exists(idxPath) || !File.Exists(mulPath))
        {
            return false;
        }

        LoadSoundDef();

        for (int id = 0; id < MaxSounds; id++)
        {
            if (TryReadSound(id, out string name, out byte[] wavBytes, out bool translated))
            {
                wavCache[id] = wavBytes;

                entries[id] = new SoundEntry
                {
                    Id = id,
                    Name = name,
                    IsValid = true,
                    IsTranslated = translated,
                    LengthSeconds = GetLengthSeconds(wavBytes)
                };
            }
        }

        return true;
    }

    public List<SoundEntry> BuildEntries(bool showFreeSlots, string searchText, bool sortByName)
    {
        List<SoundEntry> result = new();

        for (int id = 0; id < MaxSounds; id++)
        {
            if (entries.TryGetValue(id, out SoundEntry? entry))
            {
                if (MatchesSearch(entry, searchText))
                {
                    result.Add(entry);
                }
            }
            else if (showFreeSlots)
            {
                SoundEntry free = new SoundEntry
                {
                    Id = id,
                    Name = string.Empty,
                    IsValid = false
                };

                if (MatchesSearch(free, searchText))
                {
                    result.Add(free);
                }
            }
        }

        if (sortByName)
        {
            result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            result.Sort((a, b) => a.Id.CompareTo(b.Id));
        }

        return result;
    }

    public byte[]? GetWavBytes(int id)
    {
        if (removed.Contains(id))
        {
            return null;
        }

        if (wavCache.TryGetValue(id, out byte[]? bytes))
        {
            return bytes;
        }

        return null;
    }

    public void Remove(int id)
    {
        removed.Add(id);
        entries.Remove(id);
        wavCache.Remove(id);
    }

    public void AddOrReplace(int id, string wavFilePath)
    {
        if (id < 0 || id >= MaxSounds)
        {
            throw new InvalidOperationException("Sound ID is outside the valid range.");
        }

        if (!File.Exists(wavFilePath))
        {
            throw new FileNotFoundException("Audio file was not found.", wavFilePath);
        }

        byte[] wavBytes = ConvertWaveToUoFormat(wavFilePath);

        // Final safety check. This confirms the converted file is exactly what UO expects.
        wavBytes = CheckAndFixWave(wavBytes);

        string name = CleanSoundName(Path.GetFileNameWithoutExtension(wavFilePath) + ".wav");

        removed.Remove(id);
        wavCache[id] = wavBytes;

        entries[id] = new SoundEntry
        {
            Id = id,
            Name = name,
            IsValid = true,
            LengthSeconds = GetLengthSeconds(wavBytes)
        };
    }

    private static byte[] ConvertWaveToUoFormat(string wavFilePath)
    {
        using AudioFileReader sourceReader = new AudioFileReader(wavFilePath);

        ISampleProvider sampleProvider = sourceReader.ToSampleProvider();

        if (sampleProvider.WaveFormat.Channels > 1)
        {
            sampleProvider = new StereoToMonoSampleProvider(sampleProvider)
            {
                LeftVolume = 0.5f,
                RightVolume = 0.5f
            };
        }

        WaveFormat targetFormat = WaveFormat.CreateIeeeFloatWaveFormat(22050, 1);

        if (sampleProvider.WaveFormat.SampleRate != 22050)
        {
            sampleProvider = new WdlResamplingSampleProvider(sampleProvider, 22050);
        }

        WaveFormat pcmFormat = new WaveFormat(22050, 16, 1);

        using MemoryStream output = new MemoryStream();

        using (WaveFileWriter writer = new WaveFileWriter(output, pcmFormat))
        {
            float[] buffer = new float[4096];

            int samplesRead;
            while ((samplesRead = sampleProvider.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < samplesRead; i++)
                {
                    float sample = Math.Clamp(buffer[i], -1.0f, 1.0f);
                    short pcm = (short)(sample * short.MaxValue);
                    writer.WriteByte((byte)(pcm & 0xFF));
                    writer.WriteByte((byte)((pcm >> 8) & 0xFF));
                }
            }
        }

        return output.ToArray();
    }

    public void ExportSound(int id, string outputFolder)
    {
        if (!entries.TryGetValue(id, out SoundEntry? entry))
        {
            return;
        }

        byte[]? wavBytes = GetWavBytes(id);
        if (wavBytes == null)
        {
            return;
        }

        Directory.CreateDirectory(outputFolder);

        string safeName = MakeSafeFileName(entry.Name);
        if (!safeName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
        {
            safeName += ".wav";
        }

        File.WriteAllBytes(Path.Combine(outputFolder, safeName), wavBytes);
    }

    public void ExportAll(string outputFolder, bool includeSoundId)
    {
        Directory.CreateDirectory(outputFolder);

        foreach (SoundEntry entry in entries.Values)
        {
            byte[]? wavBytes = GetWavBytes(entry.Id);
            if (wavBytes == null)
            {
                continue;
            }

            string fileName = includeSoundId
                ? entry.IdHex + " " + entry.Name
                : entry.Name;

            fileName = MakeSafeFileName(fileName);

            if (!fileName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".wav";
            }

            File.WriteAllBytes(Path.Combine(outputFolder, fileName), wavBytes);
        }
    }

    public void Save()
    {
        using FileStream idxStream = File.Create(idxPath);
        using FileStream mulStream = File.Create(mulPath);
        using BinaryWriter idxWriter = new(idxStream);
        using BinaryWriter mulWriter = new(mulStream);

        for (int id = 0; id < MaxSounds; id++)
        {
            if (removed.Contains(id) || !entries.TryGetValue(id, out SoundEntry? entry))
            {
                idxWriter.Write(-1);
                idxWriter.Write(-1);
                idxWriter.Write(-1);
                continue;
            }

            byte[]? wavBytes = GetWavBytes(id);
            if (wavBytes == null || wavBytes.Length <= WavHeaderLength)
            {
                idxWriter.Write(-1);
                idxWriter.Write(-1);
                idxWriter.Write(-1);
                continue;
            }

            int lookup = checked((int)mulStream.Position);

            byte[] nameBytes = new byte[NameHeaderLength];

            string safeName = CleanSoundName(entry.Name);

            byte[] rawName = Encoding.ASCII.GetBytes(safeName);
            Array.Copy(rawName, nameBytes, Math.Min(rawName.Length, NameHeaderLength));

            mulWriter.Write(nameBytes);
            mulWriter.Write(wavBytes, WavHeaderLength, wavBytes.Length - WavHeaderLength);

            int length = checked((int)mulStream.Position - lookup);

            idxWriter.Write(lookup);
            idxWriter.Write(length);
            idxWriter.Write(id + 1);
        }
    }

    private static string CleanSoundName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string name = value.Trim();

        int nullIndex = name.IndexOf('\0');
        if (nullIndex >= 0)
        {
            name = name.Substring(0, nullIndex);
        }

        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalidChar, '_');
        }

        if (name.Length > NameHeaderLength)
        {
            name = name.Substring(0, NameHeaderLength);
        }

        return name;
    }

    private void LoadSoundDef()
    {
        string defPath = Path.Combine(folderPath, "Sound.def");
        if (!File.Exists(defPath))
        {
            return;
        }

        Regex regex = new(@"(\d{1,4}) \{(\d{1,4})\} (\d{1,3})", RegexOptions.Compiled);

        foreach (string rawLine in File.ReadAllLines(defPath))
        {
            string line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith("#"))
            {
                continue;
            }

            Match match = regex.Match(line);
            if (match.Success)
            {
                translations[int.Parse(match.Groups[1].Value)] = int.Parse(match.Groups[2].Value);
            }
        }
    }

    private bool TryReadSound(int id, out string name, out byte[] wavBytes, out bool translated)
    {
        name = string.Empty;
        wavBytes = Array.Empty<byte>();
        translated = false;

        if (!TryReadMulSound(id, out name, out wavBytes))
        {
            if (!translations.TryGetValue(id, out int translatedId))
            {
                return false;
            }

            if (!TryReadMulSound(translatedId, out name, out wavBytes))
            {
                return false;
            }

            translated = true;
        }

        return true;
    }

    private bool TryReadMulSound(int id, out string name, out byte[] wavBytes)
    {
        name = string.Empty;
        wavBytes = Array.Empty<byte>();

        using FileStream idxStream = File.OpenRead(idxPath);
        using BinaryReader idxReader = new(idxStream);

        long idxOffset = id * 12L;
        if (idxOffset + 12 > idxStream.Length)
        {
            return false;
        }

        idxStream.Seek(idxOffset, SeekOrigin.Begin);

        int lookup = idxReader.ReadInt32();
        int length = idxReader.ReadInt32();
        _ = idxReader.ReadInt32();

        if (lookup < 0 || length <= NameHeaderLength)
        {
            return false;
        }

        using FileStream mulStream = File.OpenRead(mulPath);
        using BinaryReader mulReader = new(mulStream);

        if (lookup + length > mulStream.Length)
        {
            return false;
        }

        mulStream.Seek(lookup, SeekOrigin.Begin);

        byte[] nameBuffer = mulReader.ReadBytes(NameHeaderLength);
        byte[] pcm = mulReader.ReadBytes(length - NameHeaderLength);

        name = DecodeSoundName(nameBuffer);

        byte[] header = BuildWaveHeader(pcm.Length);
        wavBytes = new byte[header.Length + pcm.Length];

        Buffer.BlockCopy(header, 0, wavBytes, 0, header.Length);
        Buffer.BlockCopy(pcm, 0, wavBytes, header.Length, pcm.Length);

        return true;
    }

    private static string DecodeSoundName(byte[] nameBuffer)
    {
        string name = Encoding.ASCII.GetString(nameBuffer);

        int nullIndex = name.IndexOf('\0');
        if (nullIndex >= 0)
        {
            name = name.Substring(0, nullIndex);
        }

        return name.Trim();
    }

    private static byte[] BuildWaveHeader(int pcmLength)
    {
        using MemoryStream stream = new();
        using BinaryWriter writer = new(stream);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(pcmLength + 36);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(pcmLength);

        return stream.ToArray();
    }

    private static double GetLengthSeconds(byte[] wavBytes)
    {
        if (wavBytes.Length <= WavHeaderLength)
        {
            return 0;
        }

        double pcmLength = wavBytes.Length - WavHeaderLength;
        return pcmLength / SampleRate / 2.0;
    }

    private static bool MatchesSearch(SoundEntry entry, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        string text = searchText.Trim();

        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(text[2..], System.Globalization.NumberStyles.HexNumber, null, out int hexId))
        {
            return entry.Id == hexId;
        }

        if (int.TryParse(text, out int id))
        {
            return entry.Id == id;
        }

        return entry.Name.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    private static string MakeSafeFileName(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '_');
        }

        return string.IsNullOrWhiteSpace(value) ? "sound.wav" : value;
    }

    private static byte[] CheckAndFixWave(byte[] input)
    {
        if (input.Length < WavHeaderLength)
        {
            throw new InvalidOperationException("Invalid WAV file.");
        }

        using MemoryStream inputStream = new(input);
        using BinaryReader reader = new(inputStream);

        string riff = Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (riff != "RIFF")
        {
            throw new InvalidOperationException("RIFF header not found.");
        }

        _ = reader.ReadUInt32();

        string wave = Encoding.ASCII.GetString(reader.ReadBytes(4));
        if (wave != "WAVE")
        {
            throw new InvalidOperationException("WAVE header not found.");
        }

        short channels = 0;
        int sampleRate = 0;
        short bits = 0;
        byte[]? data = null;

        while (inputStream.Position + 8 <= inputStream.Length)
        {
            string chunk = Encoding.ASCII.GetString(reader.ReadBytes(4));
            int size = reader.ReadInt32();

            if (chunk == "fmt ")
            {
                short format = reader.ReadInt16();
                channels = reader.ReadInt16();
                sampleRate = reader.ReadInt32();
                _ = reader.ReadInt32();
                _ = reader.ReadInt16();
                bits = reader.ReadInt16();

                if (size > 16)
                {
                    reader.ReadBytes(size - 16);
                }

                if (format != 1)
                {
                    throw new InvalidOperationException("Only PCM WAV files are supported.");
                }
            }
            else if (chunk == "data")
            {
                data = reader.ReadBytes(size);
            }
            else
            {
                reader.ReadBytes(size);
            }
        }

        if (channels != 1 || sampleRate != SampleRate || bits != 16)
        {
            throw new InvalidOperationException("Sound WAV must be mono, 22050 Hz, 16-bit PCM.");
        }

        if (data == null)
        {
            throw new InvalidOperationException("WAV data chunk was not found.");
        }

        byte[] header = BuildWaveHeader(data.Length);
        byte[] output = new byte[header.Length + data.Length];

        Buffer.BlockCopy(header, 0, output, 0, header.Length);
        Buffer.BlockCopy(data, 0, output, header.Length, data.Length);

        return output;
    }

    public SoundEntry? FindFirstFreeSlot()
    {
        for (int id = 0; id < MaxSounds; id++)
        {
            if (!entries.ContainsKey(id) || removed.Contains(id))
            {
                return new SoundEntry
                {
                    Id = id,
                    Name = string.Empty,
                    IsValid = false
                };
            }
        }

        return null;
    }

    public List<WaveformPeak> BuildSoundWaveform(int soundId, int peakCount = 180)
    {
        byte[]? wavBytes = GetWavBytes(soundId);
        if (wavBytes == null || wavBytes.Length <= WavHeaderLength)
        {
            return new List<WaveformPeak>();
        }

        return BuildWaveformFromWavBytes(wavBytes, peakCount);
    }

    public static List<WaveformPeak> BuildWaveformFromWavBytes(byte[] wavBytes, int peakCount = 180)
    {
        List<WaveformPeak> peaks = new();

        if (wavBytes == null || wavBytes.Length <= WavHeaderLength)
        {
            return peaks;
        }

        int pcmOffset = WavHeaderLength;
        int pcmLength = wavBytes.Length - WavHeaderLength;
        int sampleCount = pcmLength / 2;

        if (sampleCount <= 0)
        {
            return peaks;
        }

        peakCount = Math.Clamp(peakCount, 20, 600);

        int samplesPerPeak = Math.Max(1, sampleCount / peakCount);

        for (int peakIndex = 0; peakIndex < peakCount; peakIndex++)
        {
            int startSample = peakIndex * samplesPerPeak;
            int endSample = Math.Min(sampleCount, startSample + samplesPerPeak);

            if (startSample >= sampleCount)
            {
                break;
            }

            double maxPositive = 0;
            double maxNegative = 0;

            for (int sampleIndex = startSample; sampleIndex < endSample; sampleIndex++)
            {
                int byteIndex = pcmOffset + (sampleIndex * 2);

                if (byteIndex + 1 >= wavBytes.Length)
                {
                    break;
                }

                short sample = BitConverter.ToInt16(wavBytes, byteIndex);
                double normalized = sample / 32768.0;

                if (normalized > maxPositive)
                {
                    maxPositive = normalized;
                }

                if (normalized < maxNegative)
                {
                    maxNegative = normalized;
                }
            }

            peaks.Add(new WaveformPeak
            {
                Index = peakIndex,
                Positive = maxPositive,
                Negative = Math.Abs(maxNegative)
            });
        }

        return peaks;
    }
}
