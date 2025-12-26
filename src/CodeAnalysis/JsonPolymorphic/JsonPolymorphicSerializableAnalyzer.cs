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
        isEnabledByDefault: true,
        customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(AnalyzeCompilation);
    }

    private static void AnalyzeCompilation(CompilationAnalysisContext context)
    {
        var compilation = context.Compilation;
        var jsonSerializableAttributeSymbol = compilation.GetTypeByMetadataName("System.Text.Json.Serialization.JsonSerializableAttribute");
        var jsonSerializerContextSymbol = compilation.GetTypeByMetadataName("System.Text.Json.Serialization.JsonSerializerContext");
        var jsonPolymorphicAttributeSymbol = compilation.GetTypeByMetadataName("System.Text.Json.Serialization.JsonPolymorphicAttribute");
        var jsonRootAttributeSymbol = compilation.GetTypeByMetadataName("Comptatata.Serialization.JsonRootAttribute");

        if (jsonSerializableAttributeSymbol == null || jsonSerializerContextSymbol == null || jsonPolymorphicAttributeSymbol == null || jsonRootAttributeSymbol == null)
        {
            return;
        }

        var allNamedTypes = new List<INamedTypeSymbol>();
        GetAllNamedTypes(compilation.GlobalNamespace, allNamedTypes);

        var baseTypes = allNamedTypes.Where(t => t is { IsRecord: true, IsAbstract: true } && HasAttribute(t, jsonPolymorphicAttributeSymbol) && HasAttribute(t, jsonRootAttributeSymbol)).ToList();
        var contextTypes = allNamedTypes.Where(t => IsDerivedFrom(t, jsonSerializerContextSymbol)).ToList();

        if (baseTypes.Count == 0 || contextTypes.Count == 0) return;

        foreach (var baseType in baseTypes)
        {
            var derivedTypes = allNamedTypes.Where(t => IsDerivedFrom(t, baseType)).ToList();
            if (derivedTypes.Count == 0) continue;

            foreach (var contextType in contextTypes)
            {
                var serializableTypes = GetSerializableTypes(contextType, jsonSerializableAttributeSymbol);
                if (serializableTypes.Contains(baseType))
                {
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
                            context.ReportDiagnostic(diagnostic);
                        }
                    }
                }
            }
        }
    }

    private static void GetAllNamedTypes(INamespaceOrTypeSymbol symbol, List<INamedTypeSymbol> allTypes)
    {
        if (symbol is INamedTypeSymbol type)
        {
            allTypes.Add(type);
        }

        foreach (var member in symbol.GetMembers())
        {
            if (member is INamespaceSymbol ns)
            {
                GetAllNamedTypes(ns, allTypes);
            }
            else if (member is INamedTypeSymbol nestedType)
            {
                GetAllNamedTypes(nestedType, allTypes);
            }
        }
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
