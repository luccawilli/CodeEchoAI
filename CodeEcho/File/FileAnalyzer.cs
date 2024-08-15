using CodeEcho.NewFolder;
using CodeEcho.SonarQube.Ollama.Fixer.Sonar;
using System.Text.RegularExpressions;

namespace CodeEcho.SonarQube.Ollama.Fixer.File {
  public class FileAnalyzer {

    /// <summary>Gets the exact spot / text that is defined by the issue.</summary>
    /// <param name="fileLines"></param>
    /// <param name="issueLocation"></param>
    /// <returns></returns>
    public static string GetExactErrorSpot(string[] fileLines, TextRange issueLocation) {
      int startLine = issueLocation.StartLine;
      int endLine = issueLocation.EndLine;
      int startOffset = issueLocation.StartOffset;
      int endOffset = issueLocation.EndOffset;

      if (startLine == endLine) {
        string r = fileLines[Math.Max(startLine - 1, 0)];
        if (string.IsNullOrWhiteSpace(r)) {
          return r;
        }
        return r?.Substring(startOffset, endOffset - startOffset) ?? "";
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
    public static ErrorContext GetErrorContext(string[] fileLines, TextRange issueLocation) {
      int startContextLine = issueLocation.StartLine;
      int endContextLine = issueLocation.EndLine;

      FixRange(fileLines, ref startContextLine, ref endContextLine);
      FilterForMethods(fileLines, ref startContextLine, ref endContextLine);
      FixRange(fileLines, ref startContextLine, ref endContextLine);
      //FilterForCommentsAndAttributes(fileLines, ref startContextLine, ref endContextLine);
      startContextLine--;
      startContextLine--;
      endContextLine++;
      endContextLine++;
      FixRange(fileLines, ref startContextLine, ref endContextLine);

      string context = string.Join(Environment.NewLine, fileLines.Skip(startContextLine).Take(Math.Max(endContextLine - startContextLine, 1)));
      return new ErrorContext() {
        StartLine = startContextLine,
        EndLine = endContextLine,
        Context = context,
      };
    }

    private static void FilterForCommentsAndAttributes(string[] fileLines, ref int startContextLine, ref int endContextLine) {
      for (int i = startContextLine; i < endContextLine; i++) {
        string line = fileLines[i];
        bool isEmptyOrWhiteSpace = string.IsNullOrWhiteSpace(line);
        bool isAttribute = line.Contains("[") && line.Contains("]");
        bool isComment = line.Contains("//--");
        bool isRegion = line.Contains("#region");
        if (!(isEmptyOrWhiteSpace && isAttribute && isComment && isRegion)) {
          startContextLine = i;
          break;
        }
      }


      for (int i = endContextLine; i > startContextLine; i--) {
        string line = fileLines[i];
        bool isEmptyOrWhiteSpace = string.IsNullOrWhiteSpace(line);
        bool isAttribute = line.Contains("[") && line.Contains("]");
        bool isComment = line.Contains("//--");
        bool isRegion = line.Contains("#region");
        if (!(isEmptyOrWhiteSpace && isAttribute && isComment && isRegion)) {
          endContextLine = i;
          break;
        }
      }
    }

    private static void FixRange(string[] fileLines, ref int startContextLine, ref int endContextLine) {
      startContextLine = Math.Max(startContextLine, 0);
      endContextLine = Math.Min(endContextLine, fileLines.Length);
    }

    private static void FilterForMethods(string[] fileLines, ref int startContextLine, ref int endContextLine) {
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

    }

    static Regex IsMethodOrSomethingRegex = new Regex(@"(public|private|internal|protected)[^\(\)]*\([^\(\)]*\)( )*\{");
    private static bool IsMethodOrSomething(string c) {
      return IsMethodOrSomethingRegex.IsMatch(c);
    }
  }
}
