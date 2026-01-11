using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Comptatata.CodeAnalysis;

namespace Comptatata.CodeAnalysis.Web;

[Generator(LanguageNames.CSharp)]
public class WebApplicationMapGenerator : IIncrementalGenerator
{
    private class MapInfo
    {
        public string FilePath { get; }
        public string Namespace { get; }
        public string SerializerName { get; }
        public ImmutableHashSet<ITypeSymbol> Types { get; }
        public string Accessibility { get; }

        public MapInfo(string filePath, string ns, string serializerName, ImmutableHashSet<ITypeSymbol> types, string accessibility)
        {
            FilePath = filePath;
            Namespace = ns;
            SerializerName = serializerName;
            Types = types;
            Accessibility = accessibility;
        }
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var maps = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsMapInvocation(s),
                transform: static (ctx, ct) => GetMapInfo(ctx, ct))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);

        var compilationAndMaps = context.CompilationProvider.Combine(maps.Collect());

        context.RegisterSourceOutput(compilationAndMaps, static (spc, source) => Execute(source.Left, source.Right, spc));
    }

    private static bool IsMapInvocation(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        var name = GetMethodName(invocation);
        return name is "MapGet" or "MapPost" or "MapPut" or "MapDelete" or "MapPatch";
    }

    private static string? GetMethodName(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            return memberAccess.Name.Identifier.Text;
        if (invocation.Expression is IdentifierNameSyntax identifier)
            return identifier.Identifier.Text;
        return null;
    }

    private static MapInfo? GetMapInfo(GeneratorSyntaxContext context, CancellationToken ct)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var symbol = context.SemanticModel.GetSymbolInfo(invocation, ct).Symbol as IMethodSymbol;
        if (symbol == null || !IsMapMethod(symbol)) return null;

        var containingMethod = invocation.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        var containingClass = invocation.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        var isTopLevel = invocation.Ancestors().OfType<GlobalStatementSyntax>().Any();

        if (!isTopLevel && containingMethod == null) return null;

        string serializerName;
        string ns = "";

        if (isTopLevel)
        {
            serializerName = "RootSerializer";
            ns = GetNamespace(invocation);
        }
        else
        {
            var className = containingClass?.Identifier.Text ?? "Global";
            serializerName = $"{className}{containingMethod!.Identifier.Text}Serializer";
            ns = GetNamespace(containingClass ?? (SyntaxNode)containingMethod);
        }

        string accessibility = "internal";
        if (!isTopLevel && containingClass != null)
        {
            var classSymbol = context.SemanticModel.GetDeclaredSymbol(containingClass, ct);
            if (classSymbol?.DeclaredAccessibility == Accessibility.Public)
                accessibility = "public";
        }

        var types = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        ExtractTypes(invocation, context.SemanticModel, types, ct);

        return new MapInfo(invocation.SyntaxTree.FilePath, ns, serializerName, types.ToImmutableHashSet<ITypeSymbol>(SymbolEqualityComparer.Default), accessibility);
    }

    private static string GetNamespace(SyntaxNode node)
    {
        var ns = node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        if (ns == null) return "";
        return ns.Name.ToString();
    }

    private static bool IsMapMethod(IMethodSymbol symbol)
    {
        if (symbol.Name is not ("MapGet" or "MapPost" or "MapPut" or "MapDelete" or "MapPatch")) return false;
        var containingType = symbol.ContainingType?.ToDisplayString();
        return containingType == "Microsoft.AspNetCore.Builder.EndpointRouteBuilderExtensions";
    }

    private static void ExtractTypes(InvocationExpressionSyntax invocation, SemanticModel model, HashSet<ITypeSymbol> types, CancellationToken ct)
    {
        if (invocation.ArgumentList.Arguments.Count < 2) return;
        
        var handlerArg = invocation.ArgumentList.Arguments.Count >= 2 ? invocation.ArgumentList.Arguments[1].Expression : null;
        if (handlerArg == null) return;

        var handlerSymbol = model.GetSymbolInfo(handlerArg, ct).Symbol as IMethodSymbol;
        if (handlerSymbol == null)
        {
            var typeInfo = model.GetTypeInfo(handlerArg, ct);
            if (typeInfo.ConvertedType is INamedTypeSymbol delegateType)
            {
                handlerSymbol = delegateType.DelegateInvokeMethod;
            }
        }

        if (handlerSymbol != null)
        {
            foreach (var param in handlerSymbol.Parameters)
            {
                AddWithHierarchy(types, param.Type);
            }
            AddWithHierarchy(types, handlerSymbol.ReturnType);
        }

        var walker = new BodyTypeWalker(model, types, ct);
        walker.Visit(handlerArg);
    }

    private static bool IsRelevantType(ITypeSymbol type)
    {
        if (type == null || type.TypeKind == TypeKind.Error || type.SpecialType == SpecialType.System_Void) return false;
        if (type.SpecialType == SpecialType.System_Object) return false;

        // Exclude primitives and string
        if (type.SpecialType is >= SpecialType.System_Boolean and <= SpecialType.System_String) return false;
        if (type.SpecialType is SpecialType.System_DateTime) return false;

        // Exclude compiler-generated types and open generics
        if (type.Name.Contains("<")) return false;
        if (type is INamedTypeSymbol named && named.IsGenericType && named.TypeArguments.Any(t => t.TypeKind == TypeKind.TypeParameter)) return false;

        // Don't register serializer contexts themselves
        var current = type;
        while (current != null)
        {
            if (current.Name == "JsonSerializerContext" && current.ContainingNamespace?.ToDisplayString() == "System.Text.Json.Serialization")
                return false;
            current = current.BaseType;
        }
        
        var ns = type.ContainingNamespace?.ToDisplayString() ?? "";
        if (ns.StartsWith("Microsoft.AspNetCore")) return false;
        if (ns.StartsWith("Microsoft.Extensions")) return false;
        if (ns.StartsWith("System.Text.Json")) return false;
        if (ns.StartsWith("System.Threading.Tasks")) return false;
        if (ns.StartsWith("System.Security")) return false;
        if (ns.StartsWith("System.Threading")) return false;
        if (ns.StartsWith("System.Reflection")) return false;
        if (ns.StartsWith("System.Runtime")) return false;
        if (ns.StartsWith("JetBrains.Annotations")) return false;

        if (ns == "System")
        {
            if (type.Name is "IServiceProvider" or "CancellationToken" or "ValueType" or "Enum" or "Guid" or "DateTimeOffset" or "TimeSpan" or "Uri" or "Version" or "Type" or "RuntimeTypeHandle") return false;
        }

        return true;
    }

    private static void AddWithHierarchy(HashSet<ITypeSymbol> types, ITypeSymbol? type)
    {
        if (type == null || type.SpecialType == SpecialType.System_Object) return;

        if (type is IArrayTypeSymbol array)
        {
            AddWithHierarchy(types, array.ElementType);
        }

        if (type is INamedTypeSymbol named && named.IsGenericType)
        {
            foreach (var arg in named.TypeArguments) AddWithHierarchy(types, arg);
        }

        if (IsRelevantType(type))
        {
            if (types.Add(type))
            {
                AddWithHierarchy(types, type.BaseType);

                if (type is INamedTypeSymbol namedType)
                {
                    foreach (var member in namedType.GetMembers())
                    {
                        if (member is IPropertySymbol property)
                        {
                            AddWithHierarchy(types, property.Type);
                        }
                        else if (member is IFieldSymbol field && field.CanBeReferencedByName)
                        {
                            AddWithHierarchy(types, field.Type);
                        }
                    }
                }

                foreach (var attr in type.GetAttributes())
                {
                    if (attr.AttributeClass?.ToDisplayString() == "System.Text.Json.Serialization.JsonDerivedTypeAttribute")
                    {
                        if (attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is ITypeSymbol derivedType)
                        {
                            AddWithHierarchy(types, derivedType);
                        }
                    }
                }
            }
        }
    }

    private class BodyTypeWalker : CSharpSyntaxWalker
    {
        private readonly SemanticModel _model;
        private readonly HashSet<ITypeSymbol> _types;
        private readonly CancellationToken _ct;

        public BodyTypeWalker(SemanticModel model, HashSet<ITypeSymbol> types, CancellationToken ct)
        {
            _model = model;
            _types = types;
            _ct = ct;
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var symbol = _model.GetSymbolInfo(node, _ct).Symbol as IMethodSymbol;
            if (symbol != null && (symbol.ContainingType?.ToDisplayString() is "Microsoft.AspNetCore.Http.TypedResults" or "Microsoft.AspNetCore.Http.Results"))
            {
                if (symbol.IsGenericMethod)
                {
                    foreach (var arg in symbol.TypeArguments) AddWithHierarchy(_types, arg);
                }
                else if (node.ArgumentList.Arguments.Count > 0)
                {
                    var argType = _model.GetTypeInfo(node.ArgumentList.Arguments[0].Expression, _ct).Type;
                    AddWithHierarchy(_types, argType);
                }
            }
            base.VisitInvocationExpression(node);
        }

        public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            var typeInfo = _model.GetTypeInfo(node, _ct).Type;
            AddWithHierarchy(_types, typeInfo);
            base.VisitObjectCreationExpression(node);
        }

        public override void VisitDeclarationPattern(DeclarationPatternSyntax node)
        {
            var typeInfo = _model.GetTypeInfo(node.Type, _ct).Type;
            AddWithHierarchy(_types, typeInfo);
            base.VisitDeclarationPattern(node);
        }

        public override void VisitTypePattern(TypePatternSyntax node)
        {
            var typeInfo = _model.GetTypeInfo(node.Type, _ct).Type;
            AddWithHierarchy(_types, typeInfo);
            base.VisitTypePattern(node);
        }

        public override void VisitRecursivePattern(RecursivePatternSyntax node)
        {
            if (node.Type != null)
            {
                var typeInfo = _model.GetTypeInfo(node.Type, _ct).Type;
                AddWithHierarchy(_types, typeInfo);
            }
            base.VisitRecursivePattern(node);
        }
        
        public override void VisitCastExpression(CastExpressionSyntax node)
        {
            var typeInfo = _model.GetTypeInfo(node.Type, _ct).Type;
            AddWithHierarchy(_types, typeInfo);
            base.VisitCastExpression(node);
        }
    }

    private static void Execute(Compilation compilation, ImmutableArray<MapInfo> maps, SourceProductionContext context)
    {
        if (maps.IsDefaultOrEmpty) return;

        var groupedByFile = maps.GroupBy(m => new { m.FilePath, m.Namespace });

        foreach (var fileGroup in groupedByFile)
        {
            var sb = new StringBuilder();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("#nullable enable");
            sb.AppendLine();
            sb.AppendLine("using System.Text.Json;");
            sb.AppendLine("using System.Text.Json.Serialization;");
            sb.AppendLine("using System.Text.Json.Serialization.Metadata;");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(fileGroup.Key.Namespace))
            {
                sb.AppendLine($"namespace {fileGroup.Key.Namespace};");
                sb.AppendLine();
            }

            var serializersInFile = fileGroup.GroupBy(m => m.SerializerName);
            bool anySerializerAdded = false;

            foreach (var serializerGroup in serializersInFile)
            {
                var allTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
                foreach (var m in serializerGroup)
                {
                    foreach (var t in m.Types) allTypes.Add(t);
                }

                if (allTypes.Count == 0) continue;
                anySerializerAdded = true;

                // Search for concrete descendants in the current assembly for any identified message types
                var identifiedTypes = allTypes.ToList();
                foreach (var type in identifiedTypes)
                {
                    if (type.IsAbstract || type.TypeKind == TypeKind.Interface)
                    {
                        JsonSerializerContextEmitter.AddConcreteDescendants(allTypes, type, compilation.GlobalNamespace, AddWithHierarchy);
                    }
                }

                foreach (var type in allTypes.OrderBy(t => t.ToDisplayString()))
                {
                    sb.AppendLine($"[JsonSerializable(typeof({type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}))]");
                }

                var accessibility = serializerGroup.Any(m => m.Accessibility == "public") ? "public" : "internal";

                sb.AppendLine("[global::System.Text.Json.Serialization.JsonSourceGenerationOptions(UseStringEnumConverter = true)]");
                sb.AppendLine($"{accessibility} partial class {serializerGroup.Key} : JsonSerializerContext");
                sb.AppendLine("{");
                sb.AppendLine("    public static JsonSerializerOptions SerializerOptions => field ??= ConstructPolymorphism();");
                sb.AppendLine();
                sb.AppendLine("    private static JsonSerializerOptions ConstructPolymorphism()");
                sb.AppendLine("    {");
                sb.AppendLine("        var options = new JsonSerializerOptions");
                sb.AppendLine("        {");
                sb.AppendLine("            WriteIndented = false,");
                sb.AppendLine("            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,");
                sb.AppendLine("            RespectNullableAnnotations = true,");
                sb.AppendLine("            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,");
                sb.AppendLine("        };");
                sb.AppendLine("        options.TypeInfoResolver = JsonTypeInfoResolver.WithAddedModifier(Default, AddPolymorphism);");
                sb.AppendLine("        Generated.Initialize(options);");
                sb.AppendLine("        return options;");
                sb.AppendLine("    }");
                sb.AppendLine();
                
                JsonSerializerContextEmitter.EmitAddPolymorphism(sb, allTypes);
                
                sb.AppendLine();
                
                JsonSerializerContextEmitter.EmitGeneratedClass(sb, allTypes);
                
                sb.AppendLine("}");
                sb.AppendLine();
            }

            if (!anySerializerAdded) continue;

            var content = sb.ToString();
            var directory = Path.GetDirectoryName(fileGroup.Key.FilePath);
            if (directory == null) continue;
            
            var sourceFileName = Path.GetFileNameWithoutExtension(fileGroup.Key.FilePath);
            var outputPath = Path.Combine(directory, $"{sourceFileName}.generated.cs");

            try
            {
                if (!File.Exists(outputPath) || File.ReadAllText(outputPath) != content)
                {
                    File.WriteAllText(outputPath, content);
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
