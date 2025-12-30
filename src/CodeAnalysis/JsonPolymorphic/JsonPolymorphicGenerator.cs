using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Immutable;
using System.Text;

namespace Comptatata.CodeAnalysis.JsonPolymorphic;

[Generator(LanguageNames.CSharp)]
public class JsonPolymorphicGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor DuplicateDiscriminatorRule = new DiagnosticDescriptor(
        "COMPTATATA002",
        "Duplicate JSON discriminator",
        "The type '{0}' has the same JSON discriminator '{1}' as type '{2}' for base type '{3}'",
        "Serialization",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor OpenGenericDerivedTypeRule = new DiagnosticDescriptor(
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

    private static (TypeDeclarationSyntax Target, string? NamingPolicy)? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
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

    private static void Execute(Compilation compilation, ImmutableArray<(TypeDeclarationSyntax Target, string? NamingPolicy)> polymorphicTypes, SourceProductionContext context)
    {
        if (polymorphicTypes.IsDefaultOrEmpty) return;

        var allNamedTypes = GetAllNamedTypes(compilation.GlobalNamespace).ToList();
        var hierarchies = new List<(INamedTypeSymbol Root, List<INamedTypeSymbol> Derived)>();

        foreach (var tuple in polymorphicTypes.Distinct())
        {
            var typeDeclaration = tuple.Target;
            var namingPolicy = tuple.NamingPolicy;

            var semanticModel = compilation.GetSemanticModel(typeDeclaration.SyntaxTree);
            if (semanticModel.GetDeclaredSymbol(typeDeclaration) is not INamedTypeSymbol typeSymbol)
                continue;

            // Only process roots that are physically defined in the project being compiled
            // to ensure MessageDiscriminatorNames is generated alongside the roots.
            if (!typeSymbol.DeclaringSyntaxReferences.Any(r => compilation.ContainsSyntaxTree(r.SyntaxTree)))
                continue;

            var validDerivedTypes = new List<INamedTypeSymbol>();
            var discriminators = new Dictionary<string, INamedTypeSymbol>();
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

                    var discriminator = GetDiscriminator(type.Name, namingPolicy);

                    if (discriminators.TryGetValue(discriminator, out var existingType))
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            DuplicateDiscriminatorRule,
                            type.Locations.FirstOrDefault() ?? Location.None,
                            type.Name, discriminator, existingType.Name, typeSymbol.Name));
                        continue;
                    }

                    discriminators.Add(discriminator, type);
                    validDerivedTypes.Add(type);
                }
            }

            if (validDerivedTypes.Count > 0)
            {
                hierarchies.Add((typeSymbol, validDerivedTypes));
                
                var source = GeneratePolymorphicPartial(typeSymbol, validDerivedTypes, namingPolicy);
                if (source != null)
                {
                    var fileName = $"{typeSymbol.ContainingNamespace.ToDisplayString()}.{typeSymbol.Name}.g.cs";
                    context.AddSource(fileName, source);
                }
            }
        }

        if (hierarchies.Count > 0)
        {
            var metadataSource = GenerateMetadataClass(hierarchies);
            context.AddSource("MessageDiscriminatorNames.g.cs", metadataSource);
        }
    }

    private static string? GeneratePolymorphicPartial(INamedTypeSymbol typeSymbol, List<INamedTypeSymbol> derivedTypes, string? namingPolicy)
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

    private static IEnumerable<INamedTypeSymbol> GetAllNamedTypes(INamespaceSymbol ns)
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

    private static IEnumerable<INamedTypeSymbol> GetAllNestedTypes(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (var moreNested in GetAllNestedTypes(nested)) yield return moreNested;
        }
    }

    private static string GenerateMetadataClass(List<(INamedTypeSymbol Root, List<INamedTypeSymbol> Derived)> hierarchies)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine();
        
        var ns = hierarchies[0].Root.ContainingNamespace.ToDisplayString();
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();
        sb.AppendLine("public static class MessageDiscriminatorNames");
        sb.AppendLine("{");
        
        // GetDiscriminator
        sb.AppendLine("    public static string GetDiscriminator(object message) => message switch");
        sb.AppendLine("    {");
        var allDerived = hierarchies.SelectMany(h => h.Derived).Distinct(SymbolEqualityComparer.Default).Cast<INamedTypeSymbol>().ToList();
        foreach (var derivedType in allDerived.OrderBy(t => t.Name))
        {
            var root = hierarchies.First(h => h.Derived.Contains(derivedType, SymbolEqualityComparer.Default)).Root;
            var namingPolicyAttr = root.GetAttributes().FirstOrDefault(ad => ad.AttributeClass?.Name == "JsonPolymorphicRootAttribute");
            var namingPolicyValue = namingPolicyAttr?.NamedArguments.FirstOrDefault(kv => kv.Key == "DiscriminatorNamingPolicy").Value.Value?.ToString() ?? "0";
            
            var discriminator = GetDiscriminator(derivedType.Name, namingPolicyValue);
            var typeName = derivedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            sb.AppendLine($"        {typeName} _ => \"{discriminator}\",");
        }
        sb.AppendLine("        _ => \"\"");
        sb.AppendLine("    };");
        sb.AppendLine();

        // GetTypeName
        sb.AppendLine("    public static string? GetTypeName(string discriminator) => discriminator switch");
        sb.AppendLine("    {");
        foreach (var derivedType in allDerived.OrderBy(t => t.Name))
        {
             var root = hierarchies.First(h => h.Derived.Contains(derivedType, SymbolEqualityComparer.Default)).Root;
             var namingPolicyAttr = root.GetAttributes().FirstOrDefault(ad => ad.AttributeClass?.Name == "JsonPolymorphicRootAttribute");
             var namingPolicyValue = namingPolicyAttr?.NamedArguments.FirstOrDefault(kv => kv.Key == "DiscriminatorNamingPolicy").Value.Value?.ToString() ?? "0";
             
             var discriminator = GetDiscriminator(derivedType.Name, namingPolicyValue);
             sb.AppendLine($"        \"{discriminator}\" => \"{derivedType.Name}\",");
        }
        sb.AppendLine("        _ => null");
        sb.AppendLine("    };");

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string? GetNamingPolicy(INamedTypeSymbol root)
    {
        var attr = root.GetAttributes().FirstOrDefault(ad => ad.AttributeClass?.Name == "JsonPolymorphicRootAttribute");
        if (attr == null) return null;
        var policy = attr.NamedArguments.FirstOrDefault(kv => kv.Key == "DiscriminatorNamingPolicy").Value.Value?.ToString();
        return policy;
    }

    private static string GetDiscriminator(string typeName, string? namingPolicy)
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

    private static bool IsAssignableTo(ITypeSymbol type, ITypeSymbol baseType)
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

    private static bool HasGenericParameters(INamedTypeSymbol type)
    {
        if (type.TypeParameters.Length > 0) return true;
        if (type.TypeArguments.Any(t => t.Kind == SymbolKind.TypeParameter)) return true;
        if (type.ContainingType != null) return HasGenericParameters(type.ContainingType);
        return false;
    }
}