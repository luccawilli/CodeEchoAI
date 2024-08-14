using CodeEcho.NewFolder;
using CodeEcho.SonarQube.Ollama.Fixer.Sonar;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace CodeEcho.SonarQube.Ollama.Fixer.File {
  public class RoslynFileAnalyzer {

    public static ErrorContext GetErrorContext(string fileContent, string[] fileLines, TextRange issueLocation) {
      // Parse the content into a SyntaxTree
      var syntaxTree = CSharpSyntaxTree.ParseText(fileContent);

      // Get the root of the syntax tree
      var root = syntaxTree.GetRoot();

      // Calculate the text span based on line range
      var startOffset = fileLines.Take(issueLocation.StartLine - 1).Sum(line => line.Length + Environment.NewLine.Length);
      var endOffset = startOffset + fileLines.Skip(issueLocation.StartLine - 1).Take(issueLocation.EndLine - issueLocation.StartLine + 1).Sum(line => line.Length + Environment.NewLine.Length);
      var textSpan = TextSpan.FromBounds(startOffset, endOffset);

      // Find the node that spans the text range
      var node = root.FindNode(textSpan);

      // Find the closest method, property, or class containing the node
      var containingMethod = node.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
      var containingProperty = node.AncestorsAndSelf().OfType<PropertyDeclarationSyntax>().FirstOrDefault();
      var containingClass = node.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().FirstOrDefault();

      // Determine the context (method, property, or class) and get its text
      SyntaxNode contextNode = null;
      if (containingMethod != null) {
        contextNode = containingMethod;
      }
      else if (containingProperty != null) {
        contextNode = containingProperty;
      }
      else if (containingClass != null) {
        contextNode = containingClass;
      }

      if (contextNode != null) {
        // Get the full string of the context
        string context = contextNode.ToFullString();

        // Get the line span of the context node
        var lineSpan = contextNode.GetLocation().GetLineSpan();
        var startLineNumber = lineSpan.StartLinePosition.Line;
        var endLineNumber = lineSpan.EndLinePosition.Line + 1;

        return new ErrorContext() {
          StartLine = startLineNumber,
          EndLine = endLineNumber,
          Context = context,
        };
      }
      return new ErrorContext();
    }

  }
}
