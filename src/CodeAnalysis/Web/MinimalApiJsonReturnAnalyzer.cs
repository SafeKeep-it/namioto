using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Comptatata.CodeAnalysis.Web;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MinimalApiJsonReturnAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "COMP0001";
    private static readonly LocalizableString Title = "Use direct return instead of Results.Json";
    private static readonly LocalizableString MessageFormat = "Minimal API handler returns Results.Json or TypedResults.Json. Return the object directly instead.";
    private static readonly LocalizableString Description = "Returning the object directly is preferred in Minimal APIs as it allows for better type inference and is more AOT-friendly when combined with the COAST generator.";
    private const string Category = "Usage";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        
        // Check if it's a call to .Json()
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess || 
            memberAccess.Name.Identifier.Text != "Json") return;

        // Check if it's on TypedResults or Results
        var symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        if (symbol == null) return;

        var containingType = symbol.ContainingType.ToDisplayString();
        if (containingType != "Microsoft.AspNetCore.Http.TypedResults" && 
            containingType != "Microsoft.AspNetCore.Http.Results") return;

        // Verify it's inside a MapGet/MapPost etc call (Minimal API handler)
        if (!IsInsideMinimalApiHandler(invocation)) return;

        var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation());
        context.ReportDiagnostic(diagnostic);
    }

    private bool IsInsideMinimalApiHandler(SyntaxNode node)
    {
        var current = node.Parent;
        while (current != null)
        {
            if (current is LambdaExpressionSyntax or LocalFunctionStatementSyntax)
            {
                // Check if this lambda/function is passed to a Map... method
                var parentInvocation = current.Ancestors().OfType<InvocationExpressionSyntax>().FirstOrDefault();
                if (parentInvocation != null)
                {
                    if (parentInvocation.Expression is MemberAccessExpressionSyntax ma && 
                        ma.Name.Identifier.Text.StartsWith("Map")) return true;
                    if (parentInvocation.Expression is IdentifierNameSyntax id && 
                        id.Identifier.Text.StartsWith("Map")) return true;
                }
            }
            current = current.Parent;
        }
        return false;
    }
}
