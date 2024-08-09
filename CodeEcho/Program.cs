using CodeEcho.Dto;
using CodeEcho.NewFolder;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System.Text;

namespace CodeEcho {

  /// <summary>
  /// AI to auto fix SonarQube issues using Ollama. => Code Echo, the code is striking back
  /// </summary>
  public class Program {
    private static readonly HashSet<string> ruleFilter = new HashSet<string>() {
      //"csharpsquid:S1481" // remove unused local variables
      "csharpsquid:S1643" // use stringbuilder
    };
    private static readonly HashSet<string> projectFilter = new HashSet<string>() {
      "perigonRC"
    };

    // How to run Ollama:
    // 1. Start Docker
    // 2. Run Ollama: docker run -d -v ollama:/root/.ollama -p 11434:11434 --name ollama ollama/ollama
    // 4. Pull llama3 model: docker exec -it ollama /bin/bash -c "ollama pull llama3"

    static async Task Main(string[] args) {
      var config = new ConfigurationBuilder()
          .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
          .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", true)
          .AddEnvironmentVariables()
          .Build();

      string sonarQubeUrl = config["SonarQubeUrl"];
      string sonarQubeToken = config["SonarQubeToken"];
      string ollamaUrl = config["OllamaUrl"];
      string sourcePath = config["SourceCodeRepositoryPath"];

      SonarQubeClient sonarQubeClient = new SonarQubeClient(sonarQubeUrl, sonarQubeToken);
      JObject issues = await sonarQubeClient.GetSonarIssues(projectFilter, ruleFilter);
      List<JObject> simpleIssues = FilterSimpleIssues(issues);
      List<JObject> onlyOneIssuePerFile = simpleIssues.GroupBy(x => x["project"]).Select(x => x.First()).ToList();

      await FixIssues(ollamaUrl, sourcePath, onlyOneIssuePerFile);
    }

    private static async Task FixIssues(string ollamaUrl, string sourcePath, List<JObject> onlyOneIssuePerFile) {
      Console.WriteLine($"Found {onlyOneIssuePerFile.Count} simple issues to address.");
      foreach (var issue in onlyOneIssuePerFile) {
        Console.WriteLine($"Issue: {issue["key"]}, Message: {issue["message"]}");
        await GenerateFixWithOllama(ollamaUrl, sourcePath, issue);
      }
    }

    private static List<JObject> FilterSimpleIssues(JObject issues) {
      var simpleIssues = new List<JObject>();
      foreach (var issue in issues["issues"]) {
        bool containsRule = ruleFilter.Contains(issue["rule"].ToString());
        bool containsProject = projectFilter.Contains(issue["project"].ToString());
        if (containsProject && containsRule) {
          simpleIssues.Add((JObject)issue);
        }
      }
      return simpleIssues;
    }

    private static async Task GenerateFixWithOllama(string ollamaUrl, string sourcePath, JObject issue) {
      string component = issue["component"].ToString();
      string project = issue["project"].ToString();
      string pathToFile = component.Remove(0, project.Length + 1);
      string filePath = Path.Combine(sourcePath, pathToFile);
      if (!File.Exists(filePath)) {
        Console.WriteLine($"File does not exists: {filePath}");
        return;
      }

      var textRange = issue["textRange"];
      if (textRange == null) {
        Console.WriteLine($"Issue is not in the source code: {filePath}");
        return;
      }
      string allText = File.ReadAllText(filePath, new UTF8Encoding(true));
      string[] fileLines = allText.Split(Environment.NewLine);
      var exactErrorSpot = FileAnalyzer.GetExactErrorSpot(fileLines, textRange);
      var contextWithLines = FileAnalyzer.GetErrorContext(fileLines, textRange);

      const string startMarker = "xxxx";
      var prompt = $"The following C# code has an issue identified by SonarQube:\n\n" +
                   $"Issue: {issue["message"]}\n" +
                   $"Error:\n{exactErrorSpot}\n" +
                   $"Code Context:\n{contextWithLines.Context}\n" +
                   $"Please fix the code like a programmer would fix it. (Keep the context in mind)\n" +
                   $"Do not change formatting and do not fix other issues.\n" +
                   $"Always start the answer with {startMarker}, than write down the fixed code with context, no explanations.\n";

      OllamaRequestBody ollamaRequestBody = new OllamaRequestBody() {
        Model = "llama3",
        Prompt = prompt,
        IsStream = false
      };

      OllamaClient ollamaClient = new OllamaClient(ollamaUrl);
      string contentResult = await ollamaClient.GetResponse(ollamaRequestBody);

      if (!contentResult.StartsWith(startMarker)) {
        Console.WriteLine($"No start marker in the response, probably bad response.");
        return;
      }

      Console.WriteLine($"Response looks good.");

      string fixedCodeContext = contentResult.Substring(startMarker.Length).Trim();
      string newText = RebuildFile(fileLines, contextWithLines, fixedCodeContext);
      File.WriteAllText(filePath, newText, new UTF8Encoding(true));

      // todo: update usings
      // todo: dotnet format --files filePath
      // dotnet format C:\Users\wlu\source\repos\AhabAI\AhabAI-SonarQube-Ollama-Fixer --include C:\Users\wlu\source\repos\AhabAI\AhabAI-SonarQube-Ollama-Fixer\SonarQubeClient.cs

      Console.WriteLine($"{filePath} updated");
    }

    private static string RebuildFile(string[] fileLines, ErrorContext contextWithLines, string fixedCodeContext) {
      var newContent = fileLines.Take(contextWithLines.StartLine).ToList();
      fixedCodeContext.Split(Environment.NewLine).ToList().ForEach(newContent.Add);
      fileLines.Skip(contextWithLines.EndLine).ToList().ForEach(newContent.Add);
      string newText = string.Join(Environment.NewLine, newContent);
      return newText;
    }
  }
}
