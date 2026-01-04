using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Text;

namespace Comptatata.CodeAnalysis.JsonPolymorphic;

[Generator(LanguageNames.CSharp)]
public class JsonPolymorphicGenerator : IIncrementalGenerator
{
    static readonly DiagnosticDescriptor OpenGenericDerivedTypeRule = new DiagnosticDescriptor(
        "COMPTATATA003",
        "Open generic derived type",
        "The type '{0}' is a derived type of '{1}' but it is an open generic or contained in one, which is not supported for JSON polymorphism",
        "Serialization",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var polymorphicTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => s is TypeDeclarationSyntax { AttributeLists.Count: > 0 },
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
            .Where(static m => m.HasValue)
            .Select(static (m, _) => m!.Value);

        context.RegisterSourceOutput(polymorphicTypes.Collect().Combine(context.CompilationProvider),
            static (spc, source) => Execute(source.Right, source.Left, spc));
    }

    static (TypeDeclarationSyntax Target, string? NamingPolicy)? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        var typeDeclaration = (TypeDeclarationSyntax)context.Node;
        string? namingPolicy = null;
        bool hasJsonRoot = false;

        foreach (var attributeList in typeDeclaration.AttributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var name = attribute.Name.ToString();
                if (name == "JsonPolymorphicRoot" || name == "JsonPolymorphicRootAttribute" || name.EndsWith(".JsonPolymorphicRoot") || name.EndsWith(".JsonPolymorphicRootAttribute"))
                {
                    hasJsonRoot = true;
                    if (attribute.ArgumentList != null)
                    {
                        var namingPolicyArg = attribute.ArgumentList.Arguments
                            .FirstOrDefault(a => a.NameEquals?.Name.Identifier.Text == "DiscriminatorNamingPolicy");

                        if (namingPolicyArg != null)
                        {
                            var optionalValue = context.SemanticModel.GetConstantValue(namingPolicyArg.Expression);
                            if (optionalValue.HasValue)
                            {
                                namingPolicy = optionalValue.Value?.ToString();
                            }
                        }
                    }
                }
            }
        }

        if (hasJsonRoot)
        {
            var symbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration);
            if (symbol is INamedTypeSymbol { IsAbstract: true })
            {
                return (typeDeclaration, namingPolicy);
            }
        }

        return null;
    }

    static void Execute(Compilation compilation, ImmutableArray<(TypeDeclarationSyntax Target, string? NamingPolicy)> polymorphicTypes, SourceProductionContext context)
    {
        if (polymorphicTypes.IsDefaultOrEmpty) return;

        var allNamedTypes = GetAllNamedTypes(compilation.GlobalNamespace).ToList();

        foreach (var tuple in polymorphicTypes.Distinct())
        {
            var typeDeclaration = tuple.Target;
            var namingPolicy = tuple.NamingPolicy;

            var semanticModel = compilation.GetSemanticModel(typeDeclaration.SyntaxTree);
            if (semanticModel.GetDeclaredSymbol(typeDeclaration) is not INamedTypeSymbol typeSymbol)
                continue;

            // Only process roots that are physically defined in the project being compiled
            if (!typeSymbol.DeclaringSyntaxReferences.Any(r => compilation.ContainsSyntaxTree(r.SyntaxTree)))
                continue;

            var validDerivedTypes = new List<INamedTypeSymbol>();
            var seenTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            foreach (var type in allNamedTypes)
            {
                if (SymbolEqualityComparer.Default.Equals(type, typeSymbol)) continue;

                if (IsAssignableTo(type, typeSymbol))
                {
                    if (type.IsAbstract || type.TypeKind == TypeKind.Interface) continue;

                    if (HasGenericParameters(type))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            OpenGenericDerivedTypeRule,
                            type.Locations.FirstOrDefault() ?? Location.None,
                            type.Name, typeSymbol.Name));
                        continue;
                    }

                    if (!seenTypes.Add(type)) continue;

                    validDerivedTypes.Add(type);
                }
            }

            if (validDerivedTypes.Count > 0)
            {
                var source = GeneratePolymorphicPartial(typeSymbol, validDerivedTypes, namingPolicy);
                if (source != null)
                {
                    var fileName = $"{typeSymbol.ContainingNamespace.ToDisplayString()}.{typeSymbol.Name}.g.cs";
                    context.AddSource(fileName, source);
                }
            }
        }
    }

    static string? GeneratePolymorphicPartial(INamedTypeSymbol typeSymbol, List<INamedTypeSymbol> derivedTypes, string? namingPolicy)
    {
        if (derivedTypes.Count == 0) return null;

        var ns = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : typeSymbol.ContainingNamespace.ToDisplayString();

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();

        if (ns != null)
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        foreach (var derivedType in derivedTypes.OrderBy(t => t.Name))
        {
            var discriminator = GetDiscriminator(derivedType.Name, namingPolicy);
            var typeName = derivedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            sb.AppendLine($"[JsonDerivedType(typeof({typeName}), \"{discriminator}\")]");
        }

        var typeKind = typeSymbol.IsRecord ? "record" : "class";
        sb.AppendLine($"partial {typeKind} {typeSymbol.Name};");

        return sb.ToString();
    }

    static IEnumerable<INamedTypeSymbol> GetAllNamedTypes(INamespaceSymbol ns)
    {
        foreach (var member in ns.GetMembers())
        {
            if (member is INamespaceSymbol childNs)
            {
                foreach (var type in GetAllNamedTypes(childNs)) yield return type;
            }
            else if (member is INamedTypeSymbol type)
            {
                yield return type;
                foreach (var nested in GetAllNestedTypes(type)) yield return nested;
            }
        }
    }

    static IEnumerable<INamedTypeSymbol> GetAllNestedTypes(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (var moreNested in GetAllNestedTypes(nested)) yield return moreNested;
        }
    }

    static string GetDiscriminator(string typeName, string? namingPolicy)
    {
        if (string.IsNullOrEmpty(typeName)) return typeName;

        if (string.IsNullOrEmpty(namingPolicy) || namingPolicy == "0" || namingPolicy == "1" || namingPolicy == "CamelCase")
        {
            return char.ToLowerInvariant(typeName[0]) + typeName.Substring(1);
        }

        if (namingPolicy == "4" || namingPolicy == "KebabCaseLower")
        {
            var result = new StringBuilder();
            for (int i = 0; i < typeName.Length; i++)
            {
                var c = typeName[i];
                if (char.IsUpper(c))
                {
                    if (i > 0) result.Append('-');
                    result.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    result.Append(c);
                }
            }
            return result.ToString();
        }

        if (namingPolicy == "2" || namingPolicy == "SnakeCaseLower")
        {
            var result = new StringBuilder();
            for (int i = 0; i < typeName.Length; i++)
            {
                var c = typeName[i];
                if (char.IsUpper(c))
                {
                    if (i > 0) result.Append('_');
                    result.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    result.Append(c);
                }
            }
            return result.ToString();
        }

        return typeName;
    }

    static bool IsAssignableTo(ITypeSymbol type, ITypeSymbol baseType)
    {
        var current = type;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType)) return true;
            if (baseType.TypeKind == TypeKind.Interface && current.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, baseType)))
                return true;
            current = current.BaseType;
        }
        return false;
    }

    static bool HasGenericParameters(INamedTypeSymbol type)
    {
        if (type.TypeParameters.Length > 0) return true;
        if (type.TypeArguments.Any(t => t.Kind == SymbolKind.TypeParameter)) return true;
        if (type.ContainingType != null) return HasGenericParameters(type.ContainingType);
        return false;
    }
}