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
        DiagnosticSeverity.Warning,
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
            var jsonRootAttributeSymbol = compilationContext.Compilation.GetTypeByMetadataName("Comptatata.Serialization.JsonRootAttribute");

            if (jsonSerializerContextSymbol == null || jsonSerializableAttributeSymbol == null || jsonPolymorphicAttributeSymbol == null || jsonRootAttributeSymbol == null)
            {
                return;
            }

            var allNamedTypesLazy = new Lazy<IEnumerable<INamedTypeSymbol>>(() =>
            {
                return compilationContext.Compilation.GetSymbolsWithName(_ => true, SymbolFilter.Type).OfType<INamedTypeSymbol>();
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
                    .Where(t => t is { IsRecord: true, IsAbstract: true } && HasAttribute(t, jsonPolymorphicAttributeSymbol) && HasAttribute(t, jsonRootAttributeSymbol))
                    .ToList();

                if (baseTypesInContext.Count == 0)
                {
                    return;
                }

                var allNamedTypes = allNamedTypesLazy.Value;

                foreach (var baseType in baseTypesInContext)
                {
                    var derivedTypes = allNamedTypes.Where(t => IsDerivedFrom(t, baseType)).ToList();
                    foreach (var derivedType in derivedTypes)
                    {
                        if (!serializableTypes.Contains(derivedType))
                        {
                            var location = contextType.Locations.FirstOrDefault() ?? Location.None;
                            if (contextType.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is TypeDeclarationSyntax typeDeclaration)
                            {
                                location = typeDeclaration.Identifier.GetLocation();
                            }

                            var diagnostic = Diagnostic.Create(Rule, location, derivedType.Name, baseType.Name, contextType.Name);
                            symbolContext.ReportDiagnostic(diagnostic);
                        }
                    }
                }
            }, SymbolKind.NamedType);
        });
    }


    private static bool HasAttribute(ISymbol symbol, INamedTypeSymbol attributeSymbol)
    {
        return symbol.GetAttributes().Any(ad => SymbolEqualityComparer.Default.Equals(ad.AttributeClass, attributeSymbol));
    }

    private static bool IsDerivedFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        if (SymbolEqualityComparer.Default.Equals(type, baseType)) return false;
        var current = type.BaseType;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType)) return true;
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
