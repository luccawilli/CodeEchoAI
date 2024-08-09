using AhabAI.SonarQube.Ollama.Fixer.Dto;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Json;

namespace AhabAI.SonarQube.Ollama.Fixer {

  /// <summary>Client to access Ollama.</summary>
  public class OllamaClient {

    private string _ollamaUrl;

    public OllamaClient(string ollamaUrl) {
      _ollamaUrl = ollamaUrl;
    }

    public async Task<string> GetResponse(OllamaRequestBody ollamaRequestBody) {
      var requestBody = new {
        model = ollamaRequestBody.Model,
        prompt = ollamaRequestBody.Prompt,
        stream = ollamaRequestBody.IsStream
      };

      using var httpClient = new HttpClient();
      var response = await httpClient.PostAsJsonAsync($"{_ollamaUrl}/api/generate", requestBody); // takes about 50s
      response.EnsureSuccessStatusCode();

      var content = await response.Content.ReadAsStringAsync();
      var result = JsonConvert.DeserializeObject<JObject>(content);
      var contentResult = result["response"].ToString();
      return contentResult;
    }
  }
}
