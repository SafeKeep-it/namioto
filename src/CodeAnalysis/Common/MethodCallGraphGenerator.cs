using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Comptatata.CodeAnalysis.Common;

[Generator(LanguageNames.CSharp)]
public class MethodCallGraphGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var assemblies = context.AdditionalTextsProvider.Where(static f => f.Path.EndsWith("assemblies.txt"))
                                .Select(static (f, ct) =>
                                            f.GetText(ct)
                                             ?.ToString()
                                             .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                             .ToImmutableHashSet() ?? ImmutableHashSet<string>.Empty)
                                .Collect()
                                .Select(static (items, _) =>
                                            items.Length > 0 ? items[0] : ImmutableHashSet<string>.Empty);

        var symbolDeclarations = context.SyntaxProvider.CreateSyntaxProvider(
                                            static (s, _) =>
                                                s is MethodDeclarationSyntax or ConstructorDeclarationSyntax or
                                                     AccessorDeclarationSyntax or DestructorDeclarationSyntax or
                                                     BaseTypeDeclarationSyntax or PropertyDeclarationSyntax or
                                                     FieldDeclarationSyntax,
                                            static (ctx, _) => ctx)
                                        .Combine(assemblies);

        context.RegisterSourceOutput(symbolDeclarations.Collect().Combine(context.CompilationProvider),
                                     static (spc, source) => Execute(source.Left, source.Right, spc));
    }

    static SymbolMetadata? GetMetadata(GeneratorSyntaxContext context, ImmutableHashSet<string> solutionAssemblies)
    {
        var semanticModel = context.SemanticModel;
        var node = context.Node;

        var symbol = semanticModel.GetDeclaredSymbol(node);
        if (symbol == null && node is FieldDeclarationSyntax field && field.Declaration.Variables.Count > 0)
            symbol = semanticModel.GetDeclaredSymbol(field.Declaration.Variables[0]);

        if (symbol == null) return null;
        if (!IsDefinedInSolution(symbol, solutionAssemblies)) return null;

        var symbolId = symbol.GetDocumentationCommentId();
        if (symbolId == null) return null;

        var kind = symbol.Kind.ToString().ToLower();
        var modifiers = symbol.DeclaredAccessibility.ToString().ToLower();
        if (symbol.IsStatic) modifiers += " static";
        if (symbol.IsAbstract) modifiers += " abstract";
        if (symbol.IsSealed) modifiers += " sealed";

        if (node is MemberDeclarationSyntax member && member.Modifiers.Any(SyntaxKind.PartialKeyword))
            modifiers += " partial";

        HashSet<string> dependencies = new();
        HashSet<string> typeArguments = new();

        // 1. Signature-based
        if (symbol is IMethodSymbol methodSymbol)
        {
            // Return type: always add to dependencies if in solution (as it's not in ID)
            AddSolutionType(methodSymbol.ReturnType, dependencies, typeArguments, solutionAssemblies);

            // Parameters: only add to typeArguments if they are in solution. 
            // The parameter types themselves are in the symbol ID, so they are "duplicates" in dependencies.
            foreach (var p in methodSymbol.Parameters) AddSolutionType(p.Type, null, typeArguments, solutionAssemblies);

            foreach (var tp in methodSymbol.TypeParameters)
            foreach (var constraint in tp.ConstraintTypes)
                AddSolutionType(constraint, dependencies, typeArguments, solutionAssemblies);
        }
        else if (symbol is INamedTypeSymbol typeSymbol)
        {
            kind = typeSymbol.TypeKind.ToString().ToLower();
            AddSolutionType(typeSymbol.BaseType, dependencies, typeArguments, solutionAssemblies);
            foreach (var i in typeSymbol.Interfaces)
                AddSolutionType(i, dependencies, typeArguments, solutionAssemblies);
            foreach (var tp in typeSymbol.TypeParameters)
            foreach (var constraint in tp.ConstraintTypes)
                AddSolutionType(constraint, dependencies, typeArguments, solutionAssemblies);
        }
        else if (symbol is IPropertySymbol propertySymbol)
        {
            AddSolutionType(propertySymbol.Type, dependencies, typeArguments, solutionAssemblies);
        }
        else if (symbol is IFieldSymbol fieldSymbol)
        {
            AddSolutionType(fieldSymbol.Type, dependencies, typeArguments, solutionAssemblies);
        }

        // 2. Body-based (Calls are never duplicates)
        if (node is MethodDeclarationSyntax or ConstructorDeclarationSyntax or AccessorDeclarationSyntax or
                    DestructorDeclarationSyntax)
        {
            foreach (var invocation in node.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var info = semanticModel.GetSymbolInfo(invocation);
                var target = info.Symbol ?? (info.CandidateSymbols.Length > 0 ? info.CandidateSymbols[0] : null);
                AddSolutionType(target, dependencies, typeArguments, solutionAssemblies);
            }

            foreach (var creation in node.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                var info = semanticModel.GetSymbolInfo(creation);
                var target = info.Symbol ?? (info.CandidateSymbols.Length > 0 ? info.CandidateSymbols[0] : null);
                AddSolutionType(target, dependencies, typeArguments, solutionAssemblies);
            }
        }

        var lineSpan = node.GetLocation().GetLineSpan();
        return new(symbolId,
                   lineSpan.Path,
                   lineSpan.StartLinePosition.Line + 1,
                   lineSpan.EndLinePosition.Line + 1,
                   dependencies.Where(d => d != symbolId).ToList(),
                   typeArguments.Where(d => d != symbolId).ToList(),
                   kind,
                   modifiers);
    }

    static void AddSolutionType(ISymbol? symbol,
                                HashSet<string>? dependencies,
                                HashSet<string> typeArguments,
                                ImmutableHashSet<string> solutionAssemblies)
    {
        if (symbol == null) return;

        if (IsDefinedInSolution(symbol, solutionAssemblies))
        {
            var id = symbol.GetDocumentationCommentId();
            if (id != null) dependencies?.Add(id);
        }

        if (symbol is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            foreach (var arg in namedType.TypeArguments)
            {
                if (arg.TypeKind != TypeKind.TypeParameter && IsDefinedInSolution(arg, solutionAssemblies))
                {
                    var id = arg.GetDocumentationCommentId();
                    if (id != null) typeArguments.Add(id);
                }
                // No recursion here as per "rebuild recursive from script" instruction.
            }
        }
        else if (symbol is IMethodSymbol method && method.IsGenericMethod)
        {
            foreach (var arg in method.TypeArguments)
            {
                if (arg.TypeKind != TypeKind.TypeParameter && IsDefinedInSolution(arg, solutionAssemblies))
                {
                    var id = arg.GetDocumentationCommentId();
                    if (id != null) typeArguments.Add(id);
                }
            }
        }
    }

    static bool IsDefinedInSolution(ISymbol? symbol, ImmutableHashSet<string> solutionAssemblies)
    {
        if (symbol == null) return false;
        if (symbol.Locations.Any(l => l.IsInSource)) return true;

        var assemblyName = symbol.ContainingAssembly?.Name;
        if (assemblyName == null) return false;

        if (solutionAssemblies.IsEmpty) return assemblyName.StartsWith("Comptatata") || assemblyName == "Tests";

        return solutionAssemblies.Contains(assemblyName);
    }

    static void Execute(ImmutableArray<(GeneratorSyntaxContext Context, ImmutableHashSet<string> Assemblies)> items,
                        Compilation compilation,
                        SourceProductionContext context)
    {
        if (items.IsEmpty) return;

        var symbols = items.Select(i => GetMetadata(i.Context, i.Assemblies))
                           .Where(m => m != null)
                           .Select(m => m!)
                           .ToList();
        if (symbols.Count == 0) return;

        var projectRoot = FindSolutionRoot(symbols[0].FilePath);
        if (projectRoot == null) return;

        var ontologyDir = Path.Combine(projectRoot, "src", "dotnet", ".ontology", "symbols");
        if (!Directory.Exists(ontologyDir)) Directory.CreateDirectory(ontologyDir);

        var projectName = compilation.AssemblyName ?? "UnknownProject";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var filePath = Path.Combine(ontologyDir, $"{projectName}.{timestamp}.json");

        if (File.Exists(filePath)) return;

        var sb = new StringBuilder();
        foreach (var s in symbols)
        {
            var relativePath = s.FilePath;
            if (relativePath.StartsWith(projectRoot))
            {
                relativePath = relativePath.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar);
            }

            var depsJson = string.Join(",", s.Dependencies.Select(d => $"\"{Escape(d)}\""));
            var argsJson = string.Join(",", s.TypeArguments.Select(d => $"\"{Escape(d)}\""));
            sb.Append(
                $"{{\"symbol\":\"{Escape(s.Symbol)}\",\"kind\":\"{Escape(s.Kind)}\",\"modifiers\":\"{Escape(s.Modifiers)}\",\"file\":\"{Escape(relativePath)}\",\"startLine\":{s.StartLine},\"endLine\":{s.EndLine},\"dependencies\":[{depsJson}],\"typeArguments\":[{argsJson}]}}");
            sb.AppendLine();
        }

        try
        {
            File.WriteAllText(filePath, sb.ToString());
        }
        catch { }
    }

    static string? FindSolutionRoot(string filePath)
    {
        var current = Path.GetDirectoryName(filePath);
        while (!string.IsNullOrEmpty(current))
        {
            if (Directory.GetFiles(current, "Comptatata.sln").Any() ||
                Directory.GetFiles(current, "Comptatata.slnx").Any())
                return current;
            current = Path.GetDirectoryName(current);
        }

        return null;
    }

    static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    class SymbolMetadata
    {
        public SymbolMetadata(string symbol,
                              string filePath,
                              int startLine,
                              int endLine,
                              List<string> dependencies,
                              List<string> typeArguments,
                              string kind,
                              string modifiers)
        {
            Symbol = symbol;
            FilePath = filePath;
            StartLine = startLine;
            EndLine = endLine;
            Dependencies = dependencies;
            TypeArguments = typeArguments;
            Kind = kind;
            Modifiers = modifiers;
        }

        public string Symbol { get; }
        public string FilePath { get; }
        public int StartLine { get; }
        public int EndLine { get; }
        public List<string> Dependencies { get; }
        public List<string> TypeArguments { get; }
        public string Kind { get; }
        public string Modifiers { get; }
    }
}