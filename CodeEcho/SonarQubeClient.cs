using Newtonsoft.Json.Linq;

namespace CodeEcho {

  /// <summary>Client to access sonar qube.</summary>
  public class SonarQubeClient {

    private string _sonarQubeUrl;
    private string _sonarQubeToken;

    public SonarQubeClient(string sonarQubeUrl, string sonarQubeToken) {
      _sonarQubeUrl = sonarQubeUrl;
      _sonarQubeToken = sonarQubeToken;
    }

    public async Task<JObject> GetSonarIssues(IEnumerable<string> projectFilter, IEnumerable<string> ruleFilter) {
      using var httpClient = new HttpClient();
      // Basic Auth, Token as Password, https://docs.sonarsource.com/sonarqube/9.9/extension-guide/web-api/
      httpClient.DefaultRequestHeaders
        .Add("Authorization",
             $"Basic {Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(_sonarQubeToken + ":"))}");

      // get open issues, first 100 and for the projects
      var response = await httpClient
        .GetAsync($"{_sonarQubeUrl}/api/issues/search?statuses=OPEN&ps=100&componentKeys={string.Join(",", projectFilter)}&rules={string.Join(", ", ruleFilter)}");
      response.EnsureSuccessStatusCode();

      var content = await response.Content.ReadAsStringAsync();
      return JObject.Parse(content);
    }

  }
}
