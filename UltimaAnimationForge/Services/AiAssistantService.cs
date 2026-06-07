using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public sealed class AiAssistantService
{
    private readonly HttpClient httpClient = new()
    {
        Timeout = TimeSpan.FromMinutes(5)
    };

    public async Task<string> AskAsync(string userQuestion, string toolContext)
    {
        string prompt =
            "You are an assistant inside Ultima Animation Forge, an Ultima Online MUL/UOP animation editor.\n" +
            "Help with animation frames, body IDs, actions, directions, offsets, UOP, MUL, IDX, VD files, bodyconv.def, and mobtypes.txt.\n" +
            "Keep answers short, practical, and code-focused when needed.\n\n" +
            "Current tool context:\n" +
            toolContext + "\n\n" +
            "User question:\n" +
            userQuestion;

        var body = new
        {
            model = "llama3.1:8b",
            prompt = prompt,
            stream = false
        };

        using HttpRequestMessage request = new(
            HttpMethod.Post,
            "http://localhost:11434/api/generate");

        request.Content = new StringContent(
            JsonSerializer.Serialize(body),
            Encoding.UTF8,
            "application/json");

        try
        {
            using HttpResponseMessage response = await httpClient.SendAsync(request);
            string json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return "Ollama request failed: " + response.StatusCode + "\n" + json;
            }

            using JsonDocument doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("response", out JsonElement responseText))
            {
                return responseText.GetString() ?? string.Empty;
            }

            return json;
        }
        catch (HttpRequestException)
        {
            return
                "Could not connect to Ollama.\n\n" +
                "Make sure Ollama is installed and running, then run:\n" +
                "ollama pull llama3.1:8b";
        }
        catch (TaskCanceledException)
        {
            return "Ollama took too long to answer. Try a smaller model like llama3.2:3b.";
        }
        catch (Exception ex)
        {
            return "Ollama error: " + ex.Message;
        }
    }
}