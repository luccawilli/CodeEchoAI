using CodeEcho.NewFolder;
using CodeEcho.SonarQube.Ollama.Fixer;
using CodeEcho.SonarQube.Ollama.Fixer.File;
using CodeEcho.SonarQube.Ollama.Fixer.Ollama;
using CodeEcho.SonarQube.Ollama.Fixer.Sonar;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Text;

namespace CodeEcho {

  /// <summary>
  /// AI to auto fix SonarQube issues using Ollama. => Code Echo, the code is striking back
  /// </summary>
  public class Program {

    static async Task Main(string[] args) {
      var config = new ConfigurationBuilder()
          .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
          .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", true)
          .AddEnvironmentVariables()
          .Build();

      var codeEchoSection = config.GetSection(nameof(CodeEchoConfig));
      CodeEchoConfig? codeEchoConfig = codeEchoSection.Get<CodeEchoConfig>();
      if (!ValidateConfig(codeEchoConfig)) {
        return;
      }

      SonarQubeClient sonarQubeClient = new(
        codeEchoConfig!.SonarQubeUrl,
        codeEchoConfig.SonarQubeToken
      );
      HashSet<string> projectFilter = [.. codeEchoConfig.ProjectFilter];
      HashSet<string> ruleFilter = [.. codeEchoConfig.RuleFilter];
      List<Issue> onlyOneIssuePerFile = await GetFilteredIssues(sonarQubeClient, projectFilter, ruleFilter);

      Console.WriteLine($"Found {onlyOneIssuePerFile.Count} simple issues to address.");
      foreach (var issue in onlyOneIssuePerFile) {
        try {
          Console.WriteLine($"Issue: {issue.Key}, Message: {issue.Message}");
          string sourcePath = codeEchoConfig.SourceCodeRepositoryPath;
          string filePath = GetFilePath(sourcePath, issue);
          if (!File.Exists(filePath)) {
            Console.WriteLine($"File does not exists: {filePath}");
            return;
          }
          await GenerateFix(codeEchoConfig.OllamaUrl, filePath, issue);
          FormatFile(sourcePath, filePath);
        }
        catch (Exception ex) {
          Console.WriteLine($"Exception throw for Issue: {issue.Key} - {ex.Message} - {ex.InnerException?.Message}");
        }

        // todo git checkout + pull request --> dependency bot
      }
    }

    /// <summary>Validate the config values.</summary>
    private static bool ValidateConfig(CodeEchoConfig? codeEchoConfig) {
      if (codeEchoConfig == null) {
        Console.WriteLine($"Please supply an appsettings or env variables for {nameof(CodeEchoConfig)}");
        return false;
      }
      bool anyEmpty = string.IsNullOrWhiteSpace(codeEchoConfig.SonarQubeUrl)
        || string.IsNullOrWhiteSpace(codeEchoConfig.SonarQubeToken)
        || string.IsNullOrWhiteSpace(codeEchoConfig.OllamaUrl)
        || string.IsNullOrWhiteSpace(codeEchoConfig.SourceCodeRepositoryPath);
      if (anyEmpty) {
        Console.WriteLine($"Please provide all properties with valid non empty values");
        return false;
      }
      bool listEmpty = codeEchoConfig.ProjectFilter.Count == 0 || codeEchoConfig.RuleFilter.Count == 0;
      if (listEmpty) {
        Console.WriteLine($"Please provide at least one project and rule filter");
        return false;
      }
      return true;
    }

    private static async Task<List<Issue>> GetFilteredIssues(SonarQubeClient sonarQubeClient, HashSet<string> projectFilter, HashSet<string> ruleFilter) {
      List<Issue> issues = await sonarQubeClient.GetSonarIssues(projectFilter, ruleFilter);
      List<Issue> simpleIssues = FilterSimpleIssues(issues, projectFilter, ruleFilter);
      List<Issue> onlyOneIssuePerFile = simpleIssues
        .GroupBy(x => x.Component)
        .Select(x => x.First())
        .ToList();
      return onlyOneIssuePerFile;
    }

    private static List<Issue> FilterSimpleIssues(List<Issue> issues, HashSet<string> projectFilter, HashSet<string> ruleFilter) {
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

    private static async Task GenerateFix(string ollamaUrl, string filePath, Issue issue) {
      var textRange = issue.TextRange;
      if (textRange == null) {
        Console.WriteLine($"Issue is not in the source code: {filePath}");
        return;
      }
      string allText = File.ReadAllText(filePath, new UTF8Encoding(true));
      string[] fileLines = allText.Split(Environment.NewLine);
      var exactErrorSpot = FileAnalyzer.GetExactErrorSpot(fileLines, textRange);
      var contextWithLines = RoslynFileAnalyzer.GetErrorContext(allText, fileLines, textRange);

      if (string.IsNullOrWhiteSpace(exactErrorSpot) && string.IsNullOrWhiteSpace(contextWithLines.Context)) {
        Console.WriteLine($"No context");
        return;
      }

      const string startMarker = "xxxx";
      var prompt = $"The following C# code has an issue identified by SonarQube:\n\n" +
                   $"Issue: {issue.Message}\n" +
                   $"Error:\n{exactErrorSpot}\n" +
                   $"Code Context:\n{contextWithLines.Context}\n" +
                   $"Please fix the code like a programmer would fix it. (Keep the context in mind)\n" +
                   $"Do not change formatting and do not fix other issues.\n" +
                   $"Always start the answer with {startMarker}, than write down the fixed code with context, no explanations.\n";

      OllamaRequestBody ollamaRequestBody = new() {
        Model = "llama3",
        Prompt = prompt,
        IsStream = false
      };

      OllamaClient ollamaClient = new(ollamaUrl);
      string contentResult = await ollamaClient.GetResponse(ollamaRequestBody);

      bool validResponse = ValidateResponse(contentResult, contextWithLines, startMarker);
      if (!validResponse) {
        return;
      }

      string fixedCodeContext = contentResult.Substring(startMarker.Length).Trim().Replace("```csharp", "").Replace("```", "");
      string newText = RebuildFile(fileLines, contextWithLines, fixedCodeContext);
      File.WriteAllText(filePath, newText, new UTF8Encoding(true));
      Console.WriteLine($"Added AI advice to {filePath}");
    }

    private static bool ValidateResponse(string contentResult, ErrorContext contextWithLines, string startMarker) {
      if (string.IsNullOrWhiteSpace(contentResult)) {
        Console.WriteLine("No response from Ollama");
        return false;
      }
      if (!contentResult.StartsWith(startMarker)) {
        Console.WriteLine($"No start marker in the response, probably bad response.");
        return false;
      }
      var contentResultLines = contentResult?.Split(Environment.NewLine).ToList() ?? new List<string>();
      var contextLines = contextWithLines.Context?.Split(Environment.NewLine).ToList() ?? new List<string>();
      if (contentResultLines.Count + 5 < contextLines.Count) {
        Console.WriteLine($"To many changes, probably bad response.");
        return false;
      }

      Console.WriteLine($"Response looks good.");
      return true;
    }

    private static string GetFilePath(string sourcePath, Issue issue) {
      string component = issue.Component ?? "";
      string project = issue.Project ?? "";
      string pathToFile = component.Remove(0, project.Length + 1);
      string filePath = Path.Combine(sourcePath, pathToFile);
      return filePath;
    }

    private static string RebuildFile(string[] fileLines, ErrorContext contextWithLines, string fixedCodeContext) {
      var newContent = fileLines.Take(contextWithLines.StartLine).ToList();
      fixedCodeContext.Split(Environment.NewLine).ToList().ForEach(newContent.Add);
      fileLines.Skip(contextWithLines.EndLine).ToList().ForEach(newContent.Add);
      string newText = string.Join(Environment.NewLine, newContent);
      return newText;
    }

    private static void FormatFile(string sourcePath, string file) {
      string arguments = $"--folder \"{sourcePath}\" --include \"{file.Replace(sourcePath,"").Replace("\\", "")}\"";
      var processInfo = new ProcessStartInfo {
        FileName = "dotnet-format",
        Arguments = arguments,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
      }; 
      try {
        using (var process = Process.Start(processInfo)) {
          if (process == null) {
           Console.WriteLine("Process not started");
            return;
          }
          // Capture the output and errors
          string output = process.StandardOutput.ReadToEnd();
          string errors = process.StandardError.ReadToEnd();

          process.WaitForExit();

          // Print the output (or handle it as needed)
          Console.WriteLine("Output:");
          Console.WriteLine(output);

          // Print the errors (or handle it as needed)
          if (!string.IsNullOrWhiteSpace(errors)) {
            Console.WriteLine("Errors:");
            Console.WriteLine(errors);
          }
        }
      }
      catch (Exception ex) {
        Console.WriteLine($"An error occurred: {ex.Message}");
      }

      Console.WriteLine($"File formatted {file}");
    }

  }
}
