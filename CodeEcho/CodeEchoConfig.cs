namespace CodeEcho.SonarQube.Ollama.Fixer {
  public class CodeEchoConfig {

    /// <summary>The url of sonar qube.</summary>
    public string SonarQubeUrl { get; set; } = string.Empty;

    /// <summary>The access / api token for sonar qube.</summary>
    public string SonarQubeToken { get; set; } = string.Empty;

    /// <summary>The url of Ollama</summary>
    public string OllamaUrl { get; set; } = string.Empty;

    /// <summary>The path to the code source repository.</summary>
    public string SourceCodeRepositoryPath { get; set; } = string.Empty;

    /// <summary>The project filter to filter the issues from sonar qube for specific project names.</summary>
    public List<string> ProjectFilter { get; set; } = new List<string>();

    /// <summary>The rule filter to filter the issues from sonar qube for specific rule keys.</summary>
    public List<string> RuleFilter { get; set; } = new List<string>();
  }
}
