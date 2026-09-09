namespace PdfToMarkdown.Application.Documents;

/// <summary>Result of one of the mandatory checks run over the generated output.</summary>
public sealed record ValidationFinding(int RuleNumber, string RuleName, string Target, bool Passed, string Detail);
