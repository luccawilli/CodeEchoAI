﻿namespace AhabAI.SonarQube.Ollama.Fixer.Dto {
  public class OllamaRequestBody {

    public string Prompt { get; set; }

    public string Model { get; set; }

    public bool IsStream { get; set; }


  }
}
