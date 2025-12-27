using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Comptatata.CodeAnalysis.JsonPolymorphic;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class JsonPolymorphicSerializableAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "COMPTATATA001";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId,
        "Missing [JsonSerializable] attribute for derived type",
        "The type '{0}' is a derived type of '{1}' which is registered for JSON serialization in '{2}', but '{0}' is not explicitly registered with [JsonSerializable]",
        "Serialization",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(compilationContext =>
        {
            var jsonSerializerContextSymbol = compilationContext.Compilation.GetTypeByMetadataName("System.Text.Json.Serialization.JsonSerializerContext");
            var jsonSerializableAttributeSymbol = compilationContext.Compilation.GetTypeByMetadataName("System.Text.Json.Serialization.JsonSerializableAttribute");
            var jsonPolymorphicAttributeSymbol = compilationContext.Compilation.GetTypeByMetadataName("System.Text.Json.Serialization.JsonPolymorphicAttribute");

            if (jsonSerializerContextSymbol == null || jsonSerializableAttributeSymbol == null || jsonPolymorphicAttributeSymbol == null)
            {
                return;
            }

            var allNamedTypesLazy = new Lazy<IEnumerable<INamedTypeSymbol>>(() =>
            {
                return GetAllNamedTypes(compilationContext.Compilation.GlobalNamespace).ToList();
            });

            compilationContext.RegisterSymbolAction(symbolContext =>
            {
                var contextType = (INamedTypeSymbol)symbolContext.Symbol;
                if (!IsDerivedFrom(contextType, jsonSerializerContextSymbol))
                {
                    return;
                }

                var serializableTypes = GetSerializableTypes(contextType, jsonSerializableAttributeSymbol);
                var baseTypesInContext = serializableTypes.OfType<INamedTypeSymbol>()
                    .Where(t => HasAttributeByName(t, "JsonPolymorphicRootAttribute"))
                    .ToList();

                if (baseTypesInContext.Count == 0)
                {
                    return;
                }

                var allNamedTypes = allNamedTypesLazy.Value;

                foreach (var baseType in baseTypesInContext)
                {
                    var validDerivedTypes = allNamedTypes.Where(t =>
                        !SymbolEqualityComparer.Default.Equals(t, baseType) &&
                        IsDerivedFrom(t, baseType) &&
                        !t.IsAbstract &&
                        t.TypeKind != TypeKind.Interface &&
                        !HasGenericParameters(t)
                    ).ToList();

                    foreach (var derivedType in validDerivedTypes)
                    {
                        if (!serializableTypes.Contains(derivedType))
                        {
                            var location = contextType.Locations.FirstOrDefault() ?? Location.None;
                            if (contextType.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is TypeDeclarationSyntax typeDeclaration)
                            {
                                location = typeDeclaration.Identifier.GetLocation();
                            }

                            var properties = ImmutableDictionary<string, string?>.Empty.Add("MissingType", derivedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                            var diagnostic = Diagnostic.Create(Rule, location, properties, derivedType.Name, baseType.Name, contextType.Name);
                            symbolContext.ReportDiagnostic(diagnostic);
                        }
                    }
                }
            }, SymbolKind.NamedType);
        });
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

    private static bool HasGenericParameters(INamedTypeSymbol type)
    {
        if (type.TypeParameters.Length > 0) return true;
        if (type.TypeArguments.Any(t => t.Kind == SymbolKind.TypeParameter)) return true;
        if (type.ContainingType != null) return HasGenericParameters(type.ContainingType);
        return false;
    }

    private static bool HasAttributeByName(ISymbol symbol, string attributeName)
    {
        return symbol.GetAttributes().Any(ad => ad.AttributeClass?.Name == attributeName);
    }

    private static bool IsDerivedFrom(ITypeSymbol type, ITypeSymbol baseType)
    {
        if (SymbolEqualityComparer.Default.Equals(type, baseType)) return false;
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

    private static HashSet<ITypeSymbol> GetSerializableTypes(INamedTypeSymbol contextType, INamedTypeSymbol jsonSerializableAttributeSymbol)
    {
        var types = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var attribute in contextType.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, jsonSerializableAttributeSymbol))
            {
                if (attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is ITypeSymbol type)
                {
                    types.Add(type);
                }
            }
        }
        return types;
    }
}