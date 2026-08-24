using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dadstart.Labs.MediaForge.Analyzers;

/// <summary>
/// Ensures cmdlet catch blocks that log at error level also emit pipeline errors.
/// <see cref="Module.PowerShellLogger"/> maps <see cref="Microsoft.Extensions.Logging.LogLevel.Error"/>
/// to <c>WriteWarning</c>, so cmdlets must call <c>WriteError</c> (or equivalent) for user-facing failures.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CmdletCatchBlockWriteErrorAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MFPS001";

    private static readonly DiagnosticDescriptor _rule = new(
        DiagnosticId,
        "Cmdlet catch block must write a pipeline error",
        "Catch block logs an error but does not call WriteError, WriteStandardError, ThrowTerminatingError, HandleFileError, or rethrow. PowerShellLogger maps LogError to WriteWarning; call WriteError for user-facing failures.",
        "Design",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly ImmutableHashSet<string> _pipelineErrorMethodNames =
        ImmutableHashSet.Create(
            StringComparer.Ordinal,
            "WriteError",
            "WriteStandardError",
            "ThrowTerminatingError",
            "HandleFileError");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(_rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeCatchClause, SyntaxKind.CatchClause);
    }

    private static void AnalyzeCatchClause(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not CatchClauseSyntax catchClause)
            return;

        if (catchClause.Filter != null)
            return;

        if (CatchesOnlyCancellation(catchClause))
            return;

        var containingType = GetContainingNamedType(context.SemanticModel, catchClause);
        if (containingType == null || !IsCmdletType(containingType))
            return;

        if (!CatchBlockLogsError(catchClause, context.SemanticModel))
            return;

        if (CatchBlockReportsPipelineError(catchClause, context.SemanticModel))
            return;

        if (CatchBlockRethrows(catchClause))
            return;

        var location = catchClause.CatchKeyword.GetLocation();
        context.ReportDiagnostic(Diagnostic.Create(_rule, location));
    }

    private static INamedTypeSymbol? GetContainingNamedType(SemanticModel semanticModel, CatchClauseSyntax catchClause)
    {
        var symbol = semanticModel.GetEnclosingSymbol(catchClause.SpanStart);
        return symbol as INamedTypeSymbol ?? symbol?.ContainingType;
    }

    private static bool IsCmdletType(INamedTypeSymbol typeSymbol)
    {
        for (var current = typeSymbol; current != null; current = current.BaseType)
        {
            if (HasCmdletAttribute(current))
                return true;

            switch (current.Name)
            {
                case "CmdletBase":
                case "ProgressCmdletBase":
                case "PSCmdlet":
                    return true;
            }
        }

        return false;
    }

    private static bool HasCmdletAttribute(INamedTypeSymbol typeSymbol)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            var name = attribute.AttributeClass?.Name;
            if (name is "CmdletAttribute" or "Cmdlet")
                return true;
        }

        return false;
    }

    private static bool CatchesOnlyCancellation(CatchClauseSyntax catchClause)
    {
        if (catchClause.Declaration?.Type is not TypeSyntax caughtType)
            return false;

        var typeText = caughtType.ToString();
        return typeText.Contains("OperationCanceledException", StringComparison.Ordinal)
            || typeText.Contains("TaskCanceledException", StringComparison.Ordinal);
    }

    private static bool CatchBlockLogsError(CatchClauseSyntax catchClause, SemanticModel semanticModel)
    {
        foreach (var invocation in catchClause.Block.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (IsLogErrorInvocation(invocation, semanticModel))
                return true;
        }

        return false;
    }

    private static bool IsLogErrorInvocation(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
            return false;

        if (method.Name == "LogError")
            return true;

        if (method.Name != "Log")
            return false;

        if (invocation.ArgumentList.Arguments.Count == 0)
            return false;

        var firstArgument = invocation.ArgumentList.Arguments[0].Expression;
        var constant = semanticModel.GetConstantValue(firstArgument);
        if (constant.HasValue && constant.Value is int logLevelValue)
            return logLevelValue == 4;

        return firstArgument.ToString().Contains("LogLevel.Error", StringComparison.Ordinal);
    }

    private static bool CatchBlockReportsPipelineError(CatchClauseSyntax catchClause, SemanticModel semanticModel)
    {
        foreach (var invocation in catchClause.Block.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
                continue;

            if (_pipelineErrorMethodNames.Contains(method.Name))
                return true;
        }

        return false;
    }

    private static bool CatchBlockRethrows(CatchClauseSyntax catchClause)
    {
        return catchClause.Block.DescendantNodes().OfType<ThrowStatementSyntax>().Any();
    }
}
