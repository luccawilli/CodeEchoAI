using Newtonsoft.Json.Linq;
using System.Text.Json;

namespace CodeEcho.SonarQube.Ollama.Fixer.Sonar {

  /// <summary>Client to access sonar qube.</summary>
  public class SonarQubeClient {

    private string _sonarQubeUrl;
    private string _sonarQubeToken;

    public SonarQubeClient(string sonarQubeUrl, string sonarQubeToken) {
      _sonarQubeUrl = sonarQubeUrl;
      _sonarQubeToken = sonarQubeToken;
    }

    public async Task<List<Issue>> GetSonarIssues(IEnumerable<string> projectFilter, IEnumerable<string> ruleFilter) {
      using var httpClient = new HttpClient();
      // Basic Auth, Token as Password, https://docs.sonarsource.com/sonarqube/9.9/extension-guide/web-api/
      httpClient.DefaultRequestHeaders
        .Add("Authorization",
             $"Basic {Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(_sonarQubeToken + ":"))}");

      const int pageSize = 100;
      int index = 1;
      List<Issue> issues = new List<Issue>();
      while (true) {
        try {
          var result = await GetSonarIssuesPage(httpClient, projectFilter, ruleFilter, index, pageSize);
          if (result == null || result.Issues == null) {
            Console.WriteLine($"-- SonarQube returned no result");
            break;
          }
          issues.AddRange(result.Issues);
          index++;
          if (result.Issues.Count < pageSize) {
            break;
          }
        }
        catch (Exception ex) {
          Console.WriteLine($"-- SonarQube get issues has thrown an error: \n {ex.Message} \n {ex.InnerException?.Message}");
        }        
      }

      return issues;
    }

    private async Task<RootDto?> GetSonarIssuesPage(HttpClient httpClient, IEnumerable<string> projectFilter, IEnumerable<string> ruleFilter, int pageIndex, int pageSize) {
      // get open issues, first 100 and for the projects
      var response = await httpClient
        .GetAsync($"{_sonarQubeUrl}/api/issues/search?statuses=OPEN&p={pageIndex}&ps={pageSize}&componentKeys={string.Join(",", projectFilter)}&rules={string.Join(", ", ruleFilter)}");
      response.EnsureSuccessStatusCode();
      // Read the response content as a string
      string jsonResponse = await response.Content.ReadAsStringAsync();

      // Deserialize the JSON string into RootDto
      RootDto? result = JsonSerializer.Deserialize<RootDto>(jsonResponse, new JsonSerializerOptions() {
        PropertyNameCaseInsensitive = true
      });
      return result;
    }
  }
}
