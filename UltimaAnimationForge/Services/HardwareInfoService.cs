using System;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace UltimaAnimationForge.Services;

public static class HardwareInfoService
{
    private const ulong RequiredAiMemoryBytes = 16UL * 1024UL * 1024UL * 1024UL;
    private const string RequiredModelName = "llama3.1:8b";

    public static async Task<AiAvailabilityResult> CheckAiAvailabilityAsync()
    {
        ulong totalBytes = GetTotalPhysicalMemoryBytes();
        double totalGb = totalBytes / 1024.0 / 1024.0 / 1024.0;

        if (totalBytes < RequiredAiMemoryBytes)
        {
            return new AiAvailabilityResult
            {
                IsAvailable = false,
                Message = "AI Assistant hidden. This PC has " + totalGb.ToString("0.0") + " GB RAM. 16 GB required."
            };
        }

        bool ollamaRunning = await IsOllamaRunningAsync();

        if (!ollamaRunning)
        {
            return new AiAvailabilityResult
            {
                IsAvailable = false,
                Message = "AI Assistant hidden. Ollama is not installed or not running."
            };
        }

        bool modelInstalled = await IsModelInstalledAsync(RequiredModelName);

        if (!modelInstalled)
        {
            return new AiAvailabilityResult
            {
                IsAvailable = false,
                Message = "AI Assistant hidden. Missing Ollama model. Run: ollama pull " + RequiredModelName
            };
        }

        return new AiAvailabilityResult
        {
            IsAvailable = true,
            Message = "AI Assistant available. RAM: " + totalGb.ToString("0.0") + " GB. Ollama model: " + RequiredModelName
        };
    }

    private static async Task<bool> IsOllamaRunningAsync()
    {
        try
        {
            using HttpClient client = new()
            {
                Timeout = TimeSpan.FromSeconds(2)
            };

            using HttpResponseMessage response =
                await client.GetAsync("http://localhost:11434/api/tags");

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> IsModelInstalledAsync(string modelName)
    {
        try
        {
            using HttpClient client = new()
            {
                Timeout = TimeSpan.FromSeconds(3)
            };

            string json = await client.GetStringAsync("http://localhost:11434/api/tags");

            using JsonDocument doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("models", out JsonElement models))
            {
                return false;
            }

            foreach (JsonElement model in models.EnumerateArray())
            {
                if (!model.TryGetProperty("name", out JsonElement nameElement))
                {
                    continue;
                }

                string? name = nameElement.GetString();

                if (string.Equals(name, modelName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public static ulong GetTotalPhysicalMemoryBytes()
    {
        MEMORYSTATUSEX status = new();
        status.dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();

        if (GlobalMemoryStatusEx(ref status))
        {
            return status.ullTotalPhys;
        }

        return 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}

public sealed class AiAvailabilityResult
{
    public bool IsAvailable { get; set; }
    public string Message { get; set; } = string.Empty;
}