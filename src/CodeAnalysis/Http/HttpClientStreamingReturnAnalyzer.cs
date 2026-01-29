using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Comptatata.CodeAnalysis.Http;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class HttpClientStreamingReturnAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "COMP0003";
    const string Category = "Usage";
    static readonly LocalizableString Title = "Use IAsyncEnumerable<T> instead of Task<IAsyncEnumerable<T>>";
    static readonly LocalizableString MessageFormat =
        "HTTP client method returns Task<IAsyncEnumerable<{0}>>. Return IAsyncEnumerable<{0}> directly instead.";
    static readonly LocalizableString Description =
        "Streaming HTTP methods should return IAsyncEnumerable<T> directly. Wrapping in Task<> adds unnecessary async overhead without benefits. The enumeration itself is already asynchronous.";

    static readonly DiagnosticDescriptor Rule = new(DiagnosticId,
                                                    Title,
                                                    MessageFormat,
                                                    Category,
                                                    DiagnosticSeverity.Warning,
                                                    true,
                                                    Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethodDeclaration, SyntaxKind.MethodDeclaration);
    }

    void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
    {
        var methodDeclaration = (MethodDeclarationSyntax)context.Node;

        // Only analyze methods in interfaces
        if (methodDeclaration.Parent is not InterfaceDeclarationSyntax) return;

        var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken);
        if (methodSymbol == null) return;

        var returnType = methodSymbol.ReturnType;

        // Check if return type is Task<T>
        if (returnType is not INamedTypeSymbol { IsGenericType: true } taskType) return;
        if (taskType.OriginalDefinition.ToDisplayString() != "System.Threading.Tasks.Task<TResult>") return;

        // Check if T is IAsyncEnumerable<U>
        var wrappedType = taskType.TypeArguments[0];
        if (wrappedType is not INamedTypeSymbol { IsGenericType: true } asyncEnumerableType) return;
        if (asyncEnumerableType.OriginalDefinition.ToDisplayString() !=
            "System.Collections.Generic.IAsyncEnumerable<T>")
            return;

        // Get the element type for the diagnostic message
        var elementType = asyncEnumerableType.TypeArguments[0];
        var elementTypeName = elementType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        var diagnostic = Diagnostic.Create(Rule, methodDeclaration.ReturnType.GetLocation(), elementTypeName);
        context.ReportDiagnostic(diagnostic);
    }
}