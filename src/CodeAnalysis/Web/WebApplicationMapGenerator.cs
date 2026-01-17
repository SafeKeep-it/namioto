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
using Comptatata.CodeAnalysis.Common;

namespace Comptatata.CodeAnalysis.Web;

[Generator(LanguageNames.CSharp)]
public class WebApplicationMapGenerator : IIncrementalGenerator
{
    private class ParameterInfo
    {
        public ITypeSymbol Type { get; }
        public bool IsExplicitBody { get; }
        public bool IsCandidateBody { get; }

        public ParameterInfo(ITypeSymbol type, bool isExplicitBody, bool isCandidateBody)
        {
            Type = type;
            IsExplicitBody = isExplicitBody;
            IsCandidateBody = isCandidateBody;
        }
    }

    private class RawMapInfo
    {
        public string FilePath { get; }
        public string Namespace { get; }
        public string SerializerName { get; }
        public string Accessibility { get; }
        public ITypeSymbol? ReturnType { get; }
        public ImmutableArray<ParameterInfo> Parameters { get; }

        public RawMapInfo(string filePath, string ns, string serializerName, string accessibility, ITypeSymbol? returnType, ImmutableArray<ParameterInfo> parameters)
        {
            FilePath = filePath;
            Namespace = ns;
            SerializerName = serializerName;
            Accessibility = accessibility;
            ReturnType = returnType;
            Parameters = parameters;
        }
    }


    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var services = context.SyntaxProvider
            .CreateSyntaxProvider<ImmutableArray<ITypeSymbol>>(
                predicate: static (s, _) => IsServiceRegistration(s),
                transform: static (ctx, ct) => GetServiceTypes(ctx, ct))
            .SelectMany(static (ts, _) => ts);

        var maps = context.SyntaxProvider
            .CreateSyntaxProvider<RawMapInfo?>(
                predicate: static (s, _) => IsMapInvocation(s),
                transform: static (ctx, ct) => GetRawMapInfo(ctx, ct))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);

        var compilationAndMaps = context.CompilationProvider
            .Combine(maps.Collect())
            .Combine(services.Collect());

        context.RegisterSourceOutput(compilationAndMaps, static (spc, source) => Execute(source.Left.Left, source.Left.Right, source.Right, spc));
    }

    private static bool IsServiceRegistration(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation) return false;
        var name = GetMethodName(invocation);
        return name is "AddSingleton" or "AddScoped" or "AddTransient" or "AddHttpClient" or "AddKeyedSingleton" or "AddKeyedScoped" or "AddKeyedTransient";
    }

    private static ImmutableArray<ITypeSymbol> GetServiceTypes(GeneratorSyntaxContext context, CancellationToken ct)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var symbol = context.SemanticModel.GetSymbolInfo(invocation, ct).Symbol as IMethodSymbol;
        if (symbol == null) return ImmutableArray<ITypeSymbol>.Empty;

        var containingType = symbol.ContainingType?.ToDisplayString();
        if (containingType != "Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions" &&
            containingType != "Microsoft.Extensions.DependencyInjection.HttpClientFactoryServiceCollectionExtensions" &&
            containingType != "Microsoft.Extensions.DependencyInjection.HealthCheckServiceCollectionExtensions")
        {
            return ImmutableArray<ITypeSymbol>.Empty;
        }

        var results = new List<ITypeSymbol>();
        if (symbol.IsGenericMethod)
        {
            foreach (var arg in symbol.TypeArguments) results.Add(arg);
        }

        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            if (arg.Expression is TypeOfExpressionSyntax typeOf)
            {
                var type = context.SemanticModel.GetTypeInfo(typeOf.Type, ct).Type;
                if (type != null) results.Add(type);
            }
            else
            {
                var typeInfo = context.SemanticModel.GetTypeInfo(arg.Expression, ct);
                if (typeInfo.Type != null && typeInfo.Type.SpecialType != SpecialType.System_Object)
                {
                    results.Add(typeInfo.Type);
                }
            }
        }
        return results.ToImmutableArray();
    }

    private static RawMapInfo? GetRawMapInfo(GeneratorSyntaxContext context, CancellationToken ct)
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

        if (invocation.ArgumentList.Arguments.Count < 2) return null;
        
        var handlerArg = invocation.ArgumentList.Arguments[1].Expression;
        var handlerSymbol = context.SemanticModel.GetSymbolInfo(handlerArg, ct).Symbol as IMethodSymbol;
        
        if (handlerSymbol == null)
        {
            var typeInfo = context.SemanticModel.GetTypeInfo(handlerArg, ct);
            if (typeInfo.ConvertedType is INamedTypeSymbol delegateType)
            {
                handlerSymbol = delegateType.DelegateInvokeMethod;
            }
        }

        if (handlerSymbol == null) return null;

        var returnType = handlerSymbol.ReturnType;
        var unwrapped = JsonSerializerContextEmitter.UnwrapEnvelope(returnType);
        if (unwrapped != null && ImplementsIResult(unwrapped))
        {
            returnType = null;
        }

        if (unwrapped is INamedTypeSymbol { IsGenericType: true } genericUnwrapped &&
            (genericUnwrapped.Name == "Func" || genericUnwrapped.Name.StartsWith("Func`")))
        {
            // If the return type is Func<IResult> or similar, exclude it
            if (genericUnwrapped.TypeArguments.Any(ImplementsIResult))
            {
                returnType = null;
            }
        }

        var parameters = handlerSymbol.Parameters.Select(p => new ParameterInfo(
            p.Type,
            IsExplicitBodyParameter(p),
            IsCandidateBodyParameter(p)
        )).ToImmutableArray();

        return new RawMapInfo(invocation.SyntaxTree.FilePath, ns, serializerName, accessibility, returnType, parameters);
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


    private static bool IsExplicitBodyParameter(IParameterSymbol param)
    {
        return param.GetAttributes().Any(attr => 
            attr.AttributeClass?.Name == "FromBodyAttribute" && 
            attr.AttributeClass?.ContainingNamespace?.ToDisplayString() == "Microsoft.AspNetCore.Mvc");
    }

    private static bool IsCandidateBodyParameter(IParameterSymbol param)
    {
        var type = param.Type;
        if (type.TypeKind == TypeKind.Interface) return false;

        var ns = type.ContainingNamespace?.ToDisplayString() ?? "";
        if (ns == "Microsoft.AspNetCore.Http" || ns == "Microsoft.AspNetCore.Mvc" || ns == "Microsoft.AspNetCore.Builder") return false;
        if (ns == "System.Threading" && type.Name == "CancellationToken") return false;
        if (ns == "System.Security.Claims" && type.Name == "ClaimsPrincipal") return false;
        if (ns == "System" && type.Name == "IServiceProvider") return false;
        if (ns == "System.Net.Http" && (type.Name == "IHttpClientFactory" || type.Name == "HttpClient")) return false;

        foreach (var attr in param.GetAttributes())
        {
            var attrName = attr.AttributeClass?.Name;
            if (attrName is "FromServicesAttribute" or "FromRouteAttribute" or "FromQueryAttribute" or "FromHeaderAttribute" or "AsParametersAttribute" or "FromKeyedServicesAttribute" or "FromFormAttribute")
                return false;
        }

        return true;
    }

    private static bool ImplementsIResult(ITypeSymbol type)
    {
        if (type == null) return false;
        if (type.Name == "IResult" && type.ContainingNamespace?.ToDisplayString() == "Microsoft.AspNetCore.Http")
            return true;
            
        return type.AllInterfaces.Any(i => i.Name == "IResult" && i.ContainingNamespace?.ToDisplayString() == "Microsoft.AspNetCore.Http");
    }



    private static bool IsRelevantType(ITypeSymbol type)
    {
        if (!JsonSerializerContextEmitter.IsRelevantType(type)) return false;
        if (ImplementsIResult(type)) return false;

        var ns = type.ContainingNamespace?.ToDisplayString() ?? "";
        if (ns.StartsWith("Microsoft.AspNetCore")) return false;

        return true;
    }

    private static void Execute(Compilation compilation, ImmutableArray<RawMapInfo> maps, ImmutableArray<ITypeSymbol> services, SourceProductionContext context)
    {
        if (maps.IsDefaultOrEmpty) return;

        var serviceTypes = new HashSet<ITypeSymbol>(services, SymbolEqualityComparer.Default);
        var groupedByFile = maps.GroupBy(m => new { m.FilePath, m.Namespace });

        var allTypesInCompilation = JsonSerializerContextEmitter.GetAllTypesInCompilation(compilation);
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
                var graph = new JsonSerializerContextEmitter.SerializationGraph();

                foreach (var raw in serializerGroup)
                {
                    if (raw.ReturnType != null && !ImplementsIResult(raw.ReturnType))
                    {
                        JsonSerializerContextEmitter.AddSerializableTypes(graph, raw.ReturnType, compilation, allTypesInCompilation,
                            context.ReportDiagnostic, IsRelevantType);
                    }

                    ITypeSymbol? bodyParam = null;
                    foreach (var p in raw.Parameters)
                    {
                        if (p.IsExplicitBody)
                        {
                            bodyParam = p.Type;
                            break;
                        }

                        if (bodyParam == null && p.IsCandidateBody && !serviceTypes.Contains(p.Type))
                        {
                            bodyParam = p.Type;
                        }
                    }

                    if (bodyParam != null)
                    {
                        JsonSerializerContextEmitter.AddSerializableTypes(graph, bodyParam, compilation, allTypesInCompilation,
                            context.ReportDiagnostic, IsRelevantType);
                    }
                }

                if (graph.GetAllTypes().Count == 0) continue;
                anySerializerAdded = true;

                var accessibility = serializerGroup.Any(m => m.Accessibility == "public") ? "public" : "internal";

                JsonSerializerContextEmitter.EmitContext(sb, serializerGroup.Key, accessibility, graph,
                    "SerializerOptions", "ConstructPolymorphism");
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
