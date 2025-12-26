using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace Comptatata.CodeAnalysis.JsonPolymorphic;

[Generator(LanguageNames.CSharp)]
public class JsonPolymorphicGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Filter for classes/records that have both [JsonPolymorphic] and [JsonRoot] attributes
        var polymorphicTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsTargetSyntax(s),
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
            .Where(static m => m.HasValue)
            .Select(static (m, _) => m!.Value);

        // 2. Register the source output
        context.RegisterSourceOutput(polymorphicTypes.Collect().Combine(context.CompilationProvider), 
            static (spc, source) => Execute(source.Right, source.Left, spc));
    }

    private static bool IsTargetSyntax(SyntaxNode node)
    {
        return node is TypeDeclarationSyntax { AttributeLists.Count: > 0 };
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
                    // Try to extract DiscriminatorNamingPolicy
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
            if (symbol is INamedTypeSymbol { IsAbstract: true } &&
                typeDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
            {
                return (typeDeclaration, namingPolicy);
            }
        }

        return null;
    }

    private static void Execute(Compilation compilation, ImmutableArray<(TypeDeclarationSyntax Target, string? NamingPolicy)> polymorphicTypes, SourceProductionContext context)
    {
        if (polymorphicTypes.IsDefaultOrEmpty) return;

        foreach (var tuple in polymorphicTypes.Distinct())
        {
            var typeDeclaration = tuple.Target;
            var namingPolicy = tuple.NamingPolicy;

            var semanticModel = compilation.GetSemanticModel(typeDeclaration.SyntaxTree);
            if (semanticModel.GetDeclaredSymbol(typeDeclaration) is not INamedTypeSymbol typeSymbol)
                continue;

            var derivedTypes = FindDerivedTypes(compilation, typeSymbol);
            
            var source = GeneratePolymorphicPartial(typeSymbol, derivedTypes, namingPolicy);
            if (source != null)
            {
                context.AddSource($"{typeSymbol.Name}.g.cs", source);
            }
        }
    }

    private static List<INamedTypeSymbol> FindDerivedTypes(Compilation compilation, INamedTypeSymbol baseType)
    {
        var derivedTypes = new List<INamedTypeSymbol>();
        var stack = new Stack<INamespaceSymbol>();
        stack.Push(compilation.GlobalNamespace);

        while (stack.Count > 0)
        {
            var ns = stack.Pop();
            foreach (var member in ns.GetMembers())
            {
                if (member is INamespaceSymbol childNs)
                {
                    stack.Push(childNs);
                }
                else if (member is INamedTypeSymbol type)
                {
                    if (IsDerivedFrom(type, baseType))
                    {
                        derivedTypes.Add(type);
                    }
                }
            }
        }
        return derivedTypes;
    }

    private static bool IsDerivedFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        if (SymbolEqualityComparer.Default.Equals(type, baseType)) return false;
        
        var current = type.BaseType;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;
            current = current.BaseType;
        }
        return false;
    }

    private static string? GeneratePolymorphicPartial(INamedTypeSymbol typeSymbol, List<INamedTypeSymbol> derivedTypes, string? namingPolicy)
    {
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

        foreach (var derivedType in derivedTypes)
        {
            var discriminator = GetDiscriminator(derivedType.Name, namingPolicy);
            var typeName = derivedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            sb.AppendLine($"[JsonDerivedType(typeof({typeName}), \"{discriminator}\")]");
        }
        
        var typeKind = typeSymbol.IsRecord ? "record" : "class";
        sb.AppendLine($"partial {typeKind} {typeSymbol.Name};");

        return sb.ToString();
    }

    private static string GetDiscriminator(string typeName, string? namingPolicy)
    {
        if (string.IsNullOrEmpty(typeName)) return typeName;

        // 0: Unspecified, 1: CamelCase
        if (string.IsNullOrEmpty(namingPolicy) || namingPolicy == "0" || namingPolicy == "1" || namingPolicy == "CamelCase")
        {
            return char.ToLowerInvariant(typeName[0]) + typeName.Substring(1);
        }

        // 4: KebabCaseLower
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

        // 2: SnakeCaseLower
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
        
        // Default to original if unknown policy for now
        return typeName;
    }
}
