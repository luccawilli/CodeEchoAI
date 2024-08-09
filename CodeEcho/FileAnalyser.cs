﻿using CodeEcho.NewFolder;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace CodeEcho {
  public class FileAnalyzer {

    /// <summary>Gets the exact spot / text that is defined by the issue.</summary>
    /// <param name="fileLines"></param>
    /// <param name="issueLocation"></param>
    /// <returns></returns>
    public static string GetExactErrorSpot(string[] fileLines, JToken issueLocation) {
      int startLine = issueLocation["startLine"].Value<int>();
      int endLine = issueLocation["endLine"].Value<int>();
      int startOffset = issueLocation["startOffset"].Value<int>();
      int endOffset = issueLocation["endOffset"].Value<int>();

      if (startLine == endLine) {
        return fileLines[startLine - 1].Substring(startOffset, endOffset - startOffset);
      }

      var errorLines = new List<string>();
      errorLines.Add(fileLines[startLine - 1].Substring(startOffset));
      for (int i = startLine; i < endLine - 1; i++) {
        errorLines.Add(fileLines[i]);
      }
      errorLines.Add(fileLines[endLine - 1].Substring(0, endOffset));

      return string.Join(Environment.NewLine, errorLines);
    }

    /// <summary>Gets the context of the error. So the full method.</summary>
    /// <param name="fileLines"></param>
    /// <param name="issueLocation"></param>
    /// <returns></returns>
    public static ErrorContext GetErrorContext(string[] fileLines, JToken issueLocation) {
      int startLine = issueLocation["startLine"].Value<int>();
      int endLine = issueLocation["endLine"].Value<int>();

      int startContextLine = Math.Max(startLine, 0);
      int endContextLine = Math.Min(endLine, fileLines.Length);
      int s = startContextLine;
      for (int i = endContextLine - 1; i > 0; i--) {
        var c = fileLines[i];
        s = i;
        if (IsMethodOrSomething(c)) {
          break;
        }
      }


      int e = endContextLine;
      for (int i = startContextLine + 1; i <= fileLines.Length - 1; i++) {
        var c = fileLines[i];
        e = i;
        if (IsMethodOrSomething(c)) {
          break;
        }
      }

      startContextLine = Math.Max(s, 0);
      endContextLine = Math.Min(e, fileLines.Length);



      string context = string.Join(Environment.NewLine, fileLines.Skip(startContextLine - 1).Take((endContextLine - startContextLine) + 1));
      return new ErrorContext() {
        StartLine = startContextLine,
        EndLine = endContextLine,
        Context = context,
      };
    }

    static Regex IsMethodOrSomethingRegex = new Regex(@"(public|private|internal|protected)[^\(\)]*\([^\(\)]*\)( )*\{");
    private static bool IsMethodOrSomething(string c) {
      return IsMethodOrSomethingRegex.IsMatch(c);
    }
  }
}
