using System;
using System.Collections.Generic;

namespace CodeEcho.SonarQube.Ollama.Fixer.Sonar {

  public class TextRange {
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public int StartOffset { get; set; }
    public int EndOffset { get; set; }
  }

  public class Issue {
    public string? Key { get; set; }
    public string? Rule { get; set; }
    public string? Severity { get; set; }
    public string? Component { get; set; }
    public string? Project { get; set; }
    public int Line { get; set; }
    //public string? Hash { get; set; }
    public TextRange? TextRange { get; set; }
    public List<object>? Flows { get; set; }
    //public string? Status { get; set; }
    public string? Message { get; set; }
    //public string? Effort { get; set; }
    //public string? Debt { get; set; }
    //public string? Assignee { get; set; }
    //public string? Author { get; set; }
    //public List<string> Tags { get; set; }
    //public DateTime CreationDate { get; set; }
    //public DateTime UpdateDate { get; set; }
    //public string? Type { get; set; }
    //public string? Scope { get; set; }
    //public bool QuickFixAvailable { get; set; }
    //public List<object>? MessageFormattings { get; set; }
  }

  //public class Component {
  //  public string? Key { get; set; }
  //  public bool Enabled { get; set; }
  //  public string? Qualifier { get; set; }
  //  public string? Name { get; set; }
  //  public string? LongName { get; set; }
  //  public string? Path { get; set; }
  //}

  public class Paging {
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
  }

  public class RootDto {
    public int Total { get; set; }
    public int P { get; set; }
    public int Ps { get; set; }
    public Paging Paging { get; set; }
    //public int EffortTotal { get; set; }
    public List<Issue>? Issues { get; set; }
    //public List<Component> Components { get; set; }
    //public List<object> Facets { get; set; }
  }

}
