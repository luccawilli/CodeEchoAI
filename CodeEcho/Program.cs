using CodeEcho.NewFolder;
using CodeEcho.SonarQube.Ollama.Fixer;
using CodeEcho.SonarQube.Ollama.Fixer.File;
using CodeEcho.SonarQube.Ollama.Fixer.Ollama;
using CodeEcho.SonarQube.Ollama.Fixer.Sonar;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace CodeEcho {

  /// <summary>
  /// AI to auto fix SonarQube issues using Ollama. => Code Echo, the code is striking back
  /// </summary>
  public class Program {
    private static readonly HashSet<string> ruleFilter = new HashSet<string>() {
      //"csharpsquid:S1481", // remove unused local variables
      //"csharpsquid:S1144", // remove unused private types
      "csharpsquid:S1643", // use string builder
      //"csharpsquid:S3265", // flagged enums should not use bitwise operations
      //"csharpsquid:S2259", // possible null reference
      //"csharpsquid:S1905", // redundant casts
      //"csharpsquid:S1125", // redundant boolean literals
      //"csharpsquid:S4487", // remove unused private fields
      //"csharpsquid:S1155", // use Any() for empty check
      //"csharpsquid:S1118", // add protected constructor or static keyword to helper class
      //"csharpsquid:S3440", // remove useless condition
      //"csharpsquid:S3442", // change visibility of constructor
      //"csharpsquid:S1116", // remove empty statement
      //"csharpsquid:S1939", // remove double defined implementation 
      //"csharpsquid:S1450", // make field a local variable in the relevant method --> will need the full file?
      //"csharpsquid:S1199", // extract code to method
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

      var codeEchoSection = config.GetSection(nameof(CodeEchoConfig));
      CodeEchoConfig? codeEchoConfig = codeEchoSection.Get<CodeEchoConfig>();
      if (codeEchoConfig == null) {
        Console.WriteLine($"Please supply an appsettings or env variables for {nameof(CodeEchoConfig)}");
        return;
      }
      bool anyEmpty = string.IsNullOrWhiteSpace(codeEchoConfig.SonarQubeUrl)
        || string.IsNullOrWhiteSpace(codeEchoConfig.SonarQubeToken)
        || string.IsNullOrWhiteSpace(codeEchoConfig.OllamaUrl)
        || string.IsNullOrWhiteSpace(codeEchoConfig.SourceCodeRepositoryPath);
      if (anyEmpty) {
        Console.WriteLine($"Please provide all properties with valid non empty values");
        return;
      }

      SonarQubeClient sonarQubeClient = new SonarQubeClient(
        codeEchoConfig.SonarQubeUrl,
        codeEchoConfig.SonarQubeToken
      );
      List<Issue> onlyOneIssuePerFile = await GetFilteredIssues(sonarQubeClient);

      await FixIssues(codeEchoConfig.OllamaUrl, codeEchoConfig.SourceCodeRepositoryPath, onlyOneIssuePerFile);
    }

    private static async Task<List<Issue>> GetFilteredIssues(SonarQubeClient sonarQubeClient) {
      List<Issue> issues = await sonarQubeClient.GetSonarIssues(projectFilter, ruleFilter);
      List<Issue> simpleIssues = FilterSimpleIssues(issues);
      List<Issue> onlyOneIssuePerFile = simpleIssues
        .GroupBy(x => x.Component)
        .Select(x => x.First())
        .ToList();
      return onlyOneIssuePerFile;
    }

    private static async Task FixIssues(string ollamaUrl, string sourcePath, List<Issue> onlyOneIssuePerFile) {
      Console.WriteLine($"Found {onlyOneIssuePerFile.Count} simple issues to address.");
      foreach (var issue in onlyOneIssuePerFile) {
        Console.WriteLine($"Issue: {issue.Key}, Message: {issue.Message}");
        await GenerateFixWithOllama(ollamaUrl, sourcePath, issue);
      }
    }

    private static List<Issue> FilterSimpleIssues(List<Issue> issues) {
      if (issues == null) {
        return new List<Issue>();
      }
      var simpleIssues = new List<Issue>();
      foreach (var issue in issues) {
        bool containsRule = ruleFilter.Contains(issue.Rule ?? "");
        bool containsProject = projectFilter.Contains(issue.Project ?? "");
        if (containsProject && containsRule) {
          simpleIssues.Add(issue);
        }
      }
      return simpleIssues;
    }

    private static async Task GenerateFixWithOllama(string ollamaUrl, string sourcePath, Issue issue) {
      string component = issue.Component ?? "";
      string project = issue.Project ?? "";
      string pathToFile = component.Remove(0, project.Length + 1);
      string filePath = Path.Combine(sourcePath, pathToFile);
      if (!File.Exists(filePath)) {
        Console.WriteLine($"File does not exists: {filePath}");
        return;
      }

      var textRange = issue.TextRange;
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
                   $"Issue: {issue.Message}\n" +
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

      string fixedCodeContext = contentResult.Substring(startMarker.Length).Trim().Replace("```", "");
      string newText = RebuildFile(fileLines, contextWithLines, fixedCodeContext);
      File.WriteAllText(filePath, newText, new UTF8Encoding(true));

      // todo: update usings
      // todo: dotnet format --files filePath
      // dotnet format C:\Users\wlu\source\repos\CodeEcho --include C:\Users\wlu\source\repos\CodeEcho\CodeEcho\SonarQubeClient.cs

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
