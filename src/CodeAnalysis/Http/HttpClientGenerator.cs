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

namespace Comptatata.CodeAnalysis.Http;

[Generator(LanguageNames.CSharp)]
public class HttpClientGenerator : IIncrementalGenerator
{
    private class Registration
    {
        public ITypeSymbol InterfaceType { get; }
        public string SourceFilePath { get; }

        public Registration(ITypeSymbol interfaceType, string sourceFilePath)
        {
            InterfaceType = interfaceType;
            SourceFilePath = sourceFilePath;
        }
    }

    private class RegistrationComparer : IEqualityComparer<Registration>
    {
        public static RegistrationComparer Instance { get; } = new RegistrationComparer();
        public bool Equals(Registration x, Registration y) => SymbolEqualityComparer.Default.Equals(x?.InterfaceType, y?.InterfaceType);
        public int GetHashCode(Registration obj) => SymbolEqualityComparer.Default.GetHashCode(obj.InterfaceType);
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var interfaceTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsCreateInvocation(s),
                transform: static (ctx, _) => GetRegistration(ctx))
            .Where(static r => r is not null)
            .Select(static (r, _) => r!);

        context.RegisterSourceOutput(interfaceTypes.Collect().Combine(context.CompilationProvider),
            static (spc, source) => Execute(source.Left, source.Right, spc));
    }

    private static bool IsCreateInvocation(SyntaxNode node)
    {
        if (node is InvocationExpressionSyntax invocation)
        {
            var expression = invocation.Expression;
            if (expression is MemberAccessExpressionSyntax memberAccess)
            {
                return memberAccess.Name.Identifier.Text == "Create";
            }
            if (expression is IdentifierNameSyntax identifier)
            {
                return identifier.Identifier.Text == "Create";
            }
            if (expression is GenericNameSyntax genericName)
            {
                return genericName.Identifier.Text == "Create";
            }
        }
        return false;
    }

    private static Registration? GetRegistration(GeneratorSyntaxContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        
        // Try to get symbol info
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
        var symbol = symbolInfo.Symbol as IMethodSymbol;

        // If symbol is null, it might be because it's not yet generated or it's a pseudo-extension
        // But for our generator to work, we need the interface type.
        
        ITypeSymbol? interfaceType = null;
        if (symbol != null)
        {
            if (symbol.Name != "Create") return null;
            interfaceType = symbol.TypeArguments.FirstOrDefault();
        }
        else
        {
            // Fallback: try to get it from the syntax if it's Create<T>
            if (invocation.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax g1 })
            {
                var typeSyntax = g1.TypeArgumentList.Arguments.FirstOrDefault();
                if (typeSyntax != null)
                {
                    interfaceType = context.SemanticModel.GetTypeInfo(typeSyntax).Type;
                }
            }
            else if (invocation.Expression is GenericNameSyntax g2)
            {
                var typeSyntax = g2.TypeArgumentList.Arguments.FirstOrDefault();
                if (typeSyntax != null)
                {
                    interfaceType = context.SemanticModel.GetTypeInfo(typeSyntax).Type;
                }
            }
        }

        if (interfaceType == null || interfaceType.TypeKind != TypeKind.Interface) return null;

        return new Registration(interfaceType, invocation.SyntaxTree.FilePath);
    }

    private static void Execute(ImmutableArray<Registration> registrations, Compilation compilation, SourceProductionContext context)
    {
        var distinctRegistrations = registrations.Distinct(RegistrationComparer.Instance);

        foreach (var reg in distinctRegistrations)
        {
            try
            {
                GenerateFileForInterface(reg, compilation, context);
            }
            catch (Exception)
            {
                // In a real source generator, we'd report a diagnostic here
                // For now, we'll at least not crash the whole generator
            }
        }
    }

    private static void GenerateFileForInterface(Registration reg, Compilation compilation, SourceProductionContext context)
    {
        var interfaceType = (INamedTypeSymbol)reg.InterfaceType;
        var sourcePath = interfaceType.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree.FilePath ?? reg.SourceFilePath;
        var directory = Path.GetDirectoryName(sourcePath);
        if (directory == null) return;

        var sourceFileName = Path.GetFileNameWithoutExtension(sourcePath);
        var outputPath = Path.Combine(directory, $"{sourceFileName}.generated.cs");

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.ComponentModel;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine("using System.Text.Json.Serialization.Metadata;");
        sb.AppendLine("using System.Net.Http;");
        sb.AppendLine("using System.Net.Http.Json;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Comptatata.Http;");
        sb.AppendLine();

        var ns = interfaceType.ContainingNamespace.IsGlobalNamespace ? "" : interfaceType.ContainingNamespace.ToDisplayString();

        if (!string.IsNullOrEmpty(ns))
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        var info = GetInterfaceInfo(interfaceType, compilation);

        foreach (var messageType in info.MessageTypes.OrderBy(t => t.ToDisplayString()))
        {
            sb.AppendLine($"[JsonSerializable(typeof({messageType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}))]");
        }

        var serializerClassName = $"{interfaceType.Name}Serializer";

        sb.AppendLine("[global::System.Text.Json.Serialization.JsonSourceGenerationOptions(UseStringEnumConverter = true)]");
        sb.AppendLine($"internal partial class {serializerClassName} : JsonSerializerContext");
        sb.AppendLine("{");
        sb.AppendLine("    public static JsonSerializerOptions SerializerOptions => field ??= ConstructOptions();");
        sb.AppendLine();
        sb.AppendLine("    private static JsonSerializerOptions ConstructOptions()");
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
        
        var polymorphismTypes = info.MessageTypes
            .Where(t => t.Name != "ProblemDetails" && t.Name != "ValidationProblemDetails" && t.BaseType?.Name != "ProblemDetails" && t.BaseType?.Name != "ValidationProblemDetails")
            .ToList();
        
        JsonSerializerContextEmitter.EmitAddPolymorphism(sb, polymorphismTypes);
        sb.AppendLine();
        JsonSerializerContextEmitter.EmitGeneratedClass(sb, info.MessageTypes);
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine($"internal partial class {serializerClassName}");
        sb.AppendLine("{");
        sb.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("    public static void Initialize()");
        sb.AppendLine("    {");
        sb.AppendLine($"        global::Comptatata.Http.Factory<{interfaceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>.SetFactory(client => new {interfaceType.Name}Implementation(client));");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("[EditorBrowsable(EditorBrowsableState.Never)]");
        sb.AppendLine($"file class {interfaceType.Name}Implementation : {interfaceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}");
        sb.AppendLine("{");
        sb.AppendLine("    private readonly HttpClient _client;");
        sb.AppendLine($"    public {interfaceType.Name}Implementation(HttpClient client)");
        sb.AppendLine("    {");
        sb.AppendLine("        _client = client;");
        sb.AppendLine($"        _ = {serializerClassName}.SerializerOptions;");
        sb.AppendLine("    }");
        sb.AppendLine();

        foreach (var method in info.Methods)
        {
            var parameters = string.Join(", ", method.Method.Parameters.Select(p => $"{p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {p.Name}"));
            var asyncKeyword = method.IsAsync ? "async " : "";
            
            sb.AppendLine($"    public {asyncKeyword}{method.Method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {method.Method.Name}({parameters})");
            sb.AppendLine("    {");

            var urlPath = GetUrlPath(method.Method.Name, out var httpMethod);
            var requestMethod = CommonMethods.Contains(httpMethod) 
                ? $"global::System.Net.Http.HttpMethod.{httpMethod}" 
                : $"global::System.Net.Http.HttpMethod.Parse(\"{httpMethod.ToUpperInvariant()}\")";

            if (method.ParameterType != null)
            {
                var paramName = method.ParameterName;
                var paramTypeStr = method.ParameterType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                sb.AppendLine($"        var content = JsonContent.Create<{paramTypeStr}>({paramName}, {serializerClassName}.Generated.{GetPropertyName(method.ParameterType)});");
                sb.AppendLine($"        using var request = new HttpRequestMessage({requestMethod}, \"{urlPath}\") {{ Content = content }};");
            }
            else
            {
                sb.AppendLine($"        using var request = new HttpRequestMessage({requestMethod}, \"{urlPath}\");");
            }

            var ctPart = method.CancellationTokenParameterName != null ? $", {method.CancellationTokenParameterName}" : "";
            sb.AppendLine($"        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead{ctPart}).ConfigureAwait(false);");

            sb.AppendLine("        var statusCode = (int)response.StatusCode;");
            sb.AppendLine("        var standardStatusCode = (global::Comptatata.Http.HttpStatusCode)statusCode;");
            sb.AppendLine($"        var standardMethod = global::Comptatata.Http.HttpMethod.{httpMethod};");
            sb.AppendLine("        var hasNoBody = statusCode is >= 100 and < 200 or 204 or 205 or 304 || standardMethod == global::Comptatata.Http.HttpMethod.Head;");
            sb.AppendLine("        if (hasNoBody && standardMethod != global::Comptatata.Http.HttpMethod.Head && response.Content is not null && response.Content.Headers.ContentLength > 0)");
            sb.AppendLine("        {");
            sb.AppendLine("            await response.DrainBodyAsync().ConfigureAwait(false);");
            sb.AppendLine("            throw new global::Comptatata.Http.InvalidResponseException(request.RequestUri!, standardMethod, standardStatusCode, \"Response has a body when none was expected.\");");
            sb.AppendLine("        }");

            var unwrappedReturn = UnwrapTask(method.Method.ReturnType, compilation);
            ITypeSymbol? rawEntityType = null;
            var isRawResponse = unwrappedReturn is not null && TryGetHttpResponseEntityType(unwrappedReturn, compilation, out rawEntityType);
            var rawEntityTypeStr = rawEntityType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            var targetType = rawEntityType ?? unwrappedReturn;
            var targetTypeStr = targetType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "object";
            var targetTypeInfo = targetType != null ? $"{serializerClassName}.Generated.{GetPropertyName(targetType)}" : "null";

            sb.AppendLine("        if (statusCode is >= 400 and < 600)");
            sb.AppendLine("        {");
            sb.AppendLine($"            var problemDetails = await global::Comptatata.Http.HttpProblemDetails.ReadAsync<{targetTypeStr}>(response, {serializerClassName}.Generated.ProblemDetails, {targetTypeInfo}).ConfigureAwait(false);");
            if (isRawResponse)
            {
                sb.AppendLine($"            {rawEntityTypeStr}? parsedEntity = default;");
                sb.AppendLine($"            if (problemDetails?.Extensions?.TryGetValue(\"data\", out var data) == true && data is {rawEntityTypeStr} t) parsedEntity = t;");
                sb.AppendLine($"            return new global::Comptatata.Http.HttpResponse<{rawEntityTypeStr}>(request.RequestUri!, parsedEntity, standardStatusCode, problemDetails);");
            }
            else
            {
                sb.AppendLine("            var finalProblem = problemDetails ?? global::Comptatata.Http.ProblemDetailsDefaults.Apply(new global::Comptatata.Http.ProblemDetails(), statusCode);");
                sb.AppendLine("            throw new global::Comptatata.Http.HttpRequestDetailedException(request.RequestUri!, standardMethod, standardStatusCode, finalProblem);");
            }
            sb.AppendLine("        }");

            sb.AppendLine("        if (statusCode is >= 300 and < 400)");
            sb.AppendLine("        {");
            if (isRawResponse)
            {
                sb.AppendLine($"            return new global::Comptatata.Http.HttpResponse<{rawEntityTypeStr}>(request.RequestUri!, default, standardStatusCode);");
            }
            else
            {
                sb.AppendLine("            var location = response.Headers.Location;");
                sb.AppendLine("            throw new HttpRequestException($\"Request to {request.RequestUri} returned redirect status {statusCode} ({standardStatusCode}). Location: {location}\", null, (global::System.Net.HttpStatusCode)statusCode);");
            }
            sb.AppendLine("        }");

            sb.AppendLine("        if (!response.IsSuccessStatusCode)");
            sb.AppendLine("        {");
            if (isRawResponse)
            {
                sb.AppendLine($"            return new global::Comptatata.Http.HttpResponse<{rawEntityTypeStr}>(request.RequestUri!, default, standardStatusCode);");
            }
            else
            {
                sb.AppendLine("            var errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);");
                sb.AppendLine($"            throw new HttpRequestException($\"Request to {{request.RequestUri}} failed with status {{statusCode}} ({{standardStatusCode}}). Body: {{errorBody}}\", null, (global::System.Net.HttpStatusCode)statusCode);");
            }
            sb.AppendLine("        }");

            if (method.Method.ReturnType is INamedTypeSymbol returnType && returnType.IsGenericType && IsTask(returnType, compilation))
            {
                var resultType = returnType.TypeArguments[0];
                var resultTypeStr = resultType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

                if (isRawResponse)
                {
                    var entityPropertyName = GetPropertyName(rawEntityType!);
                    sb.AppendLine($"        {rawEntityTypeStr}? entity = default;");
                    sb.AppendLine("        if (!hasNoBody && response.Content is not null && (response.Content.Headers.ContentLength is null || response.Content.Headers.ContentLength > 0))");
                    sb.AppendLine("        {");
                    sb.AppendLine($"            entity = await response.Content.ReadFromJsonAsync<{rawEntityTypeStr}>({serializerClassName}.Generated.{entityPropertyName}).ConfigureAwait(false);");
                    sb.AppendLine("        }");
                    sb.AppendLine($"        return new global::Comptatata.Http.HttpResponse<{rawEntityTypeStr}>(request.RequestUri!, entity, standardStatusCode);");
                }
                else
                {
                    sb.AppendLine("        if (hasNoBody)");
                    sb.AppendLine("        {");
                    sb.AppendLine("            throw new global::Comptatata.Http.InvalidResponseException(request.RequestUri!, standardMethod, standardStatusCode, \"Response has no body when one was expected.\");");
                    sb.AppendLine("        }");

                    if (resultTypeStr is "global::Comptatata.Http.ProblemDetails" or "global::Comptatata.Http.ValidationProblemDetails")
                    {
                        sb.AppendLine($"        var result = (await response.Content.ReadFromJsonAsync<{resultTypeStr}>({serializerClassName}.Generated.{GetPropertyName(resultType)}).ConfigureAwait(false))!;");
                        sb.AppendLine("        return global::Comptatata.Http.ProblemDetailsDefaults.Apply(result, statusCode);");
                    }
                    else
                    {
                        sb.AppendLine($"        return (await response.Content.ReadFromJsonAsync<{resultTypeStr}>({serializerClassName}.Generated.{GetPropertyName(resultType)}).ConfigureAwait(false))!;");
                    }
                }
            }

            sb.AppendLine("    }");
        }
        sb.AppendLine("}");
        sb.AppendLine();

        var content = sb.ToString();
        try
        {
            if (!File.Exists(outputPath) || File.ReadAllText(outputPath) != content)
            {
                File.WriteAllText(outputPath, content);
            }
        }
        catch (Exception) { }
    }

    private static readonly HashSet<string> CommonMethods = new HashSet<string>(StringComparer.Ordinal)
    {
        "Delete", "Get", "Head", "Options", "Patch", "Post", "Put", "Trace"
    };

    private static readonly string[] KnownMethods = new[]
    {
        "Acl", "BaselineControl", "Bind", "Checkin", "Checkout", "Connect", "Copy", "Delete", "Get", "Head",
        "Label", "Link", "Lock", "Merge", "MkActivity", "MkCalendar", "MkCol", "MkRedirectRef", "MkWorkspace",
        "Move", "Options", "OrderPatch", "Patch", "Post", "Pri", "PropFind", "PropPatch", "Put", "Query", "Rebind",
        "Report", "Search", "Trace", "Unbind", "Uncheckout", "Unlink", "Unlock", "Update", "UpdateRedirectRef",
        "VersionControl"
    }.OrderByDescending(m => m.Length).ToArray();

    private static string GetUrlPath(string methodName, out string httpMethod)
    {
        httpMethod = "Get";
        foreach (var method in KnownMethods)
        {
            if (methodName.StartsWith(method, StringComparison.Ordinal))
            {
                httpMethod = method;
                methodName = methodName.Substring(method.Length);
                break;
            }
        }

        // Strip "Async" suffix if present
        if (methodName.EndsWith("Async", StringComparison.Ordinal)) methodName = methodName.Substring(0, methodName.Length - 5);

        if (string.IsNullOrEmpty(methodName) || methodName == "Async") return "/";

        var sb = new StringBuilder();
        if (methodName.StartsWith("WellKnown", StringComparison.Ordinal))
        {
            sb.Append("/.well-known");
            methodName = methodName.Substring(9);
            if (string.IsNullOrEmpty(methodName)) return sb.ToString();
        }

        if (methodName.Length > 0 && char.IsUpper(methodName[0]))
        {
            sb.Append('/');
            sb.Append(char.ToLower(methodName[0]));
        }
        else if (methodName.Length > 0)
        {
            sb.Append('/');
            sb.Append(methodName[0]);
        }

        for (int i = 1; i < methodName.Length; i++)
        {
            char c = methodName[i];
            if (char.IsUpper(c))
            {
                sb.Append('/');
                sb.Append(char.ToLower(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private static string GetPropertyName(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array) return GetPropertyName(array.ElementType) + "Array";
        if (type is INamedTypeSymbol named && named.IsGenericType)
        {
            return named.Name.Split('`')[0] + "_" + string.Join("_", named.TypeArguments.Select(GetPropertyName));
        }
        return type.Name;
    }

    private class InterfaceInfo
    {
        public HashSet<ITypeSymbol> MessageTypes { get; }
        public List<MethodInfo> Methods { get; }
        public InterfaceInfo(HashSet<ITypeSymbol> messageTypes, List<MethodInfo> methods)
        {
            MessageTypes = messageTypes;
            Methods = methods;
        }
    }

    private class MethodInfo
    {
        public IMethodSymbol Method { get; }
        public ITypeSymbol? ParameterType { get; }
        public string? ParameterName { get; }
        public string? CancellationTokenParameterName { get; }
        public bool IsAsync { get; }
        public MethodInfo(IMethodSymbol method, ITypeSymbol? parameterType, string? parameterName, string? cancellationTokenParameterName, bool isAsync)
        {
            Method = method;
            ParameterType = parameterType;
            ParameterName = parameterName;
            CancellationTokenParameterName = cancellationTokenParameterName;
            IsAsync = isAsync;
        }
    }

    private static bool IsTask(ITypeSymbol type, Compilation compilation)
    {
        var taskType = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task");
        var genericTaskType = compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");
        var valueTaskType = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask");
        var genericValueTaskType = compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1");

        var original = type.OriginalDefinition;
        return SymbolEqualityComparer.Default.Equals(original, taskType) ||
               SymbolEqualityComparer.Default.Equals(original, genericTaskType) ||
               SymbolEqualityComparer.Default.Equals(original, valueTaskType) ||
               SymbolEqualityComparer.Default.Equals(original, genericValueTaskType);
    }

    private static bool TryGetHttpResponseEntityType(ITypeSymbol type, Compilation compilation, out ITypeSymbol? entityType)
    {
        entityType = null;
        if (type is not INamedTypeSymbol named || !named.IsGenericType) return false;

        var httpResponseType = compilation.GetTypeByMetadataName("Comptatata.Http.HttpResponse`1");
        if (httpResponseType is null) return false;

        if (!SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, httpResponseType)) return false;

        entityType = named.TypeArguments[0];
        return true;
    }

    private static bool IsCancellationToken(ITypeSymbol type, Compilation compilation)
    {
        var ctType = compilation.GetTypeByMetadataName("System.Threading.CancellationToken");
        return SymbolEqualityComparer.Default.Equals(type, ctType);
    }

    private static InterfaceInfo GetInterfaceInfo(INamedTypeSymbol interfaceType, Compilation compilation)
    {
        var allTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var contractTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var methods = new List<MethodInfo>();

        foreach (var member in interfaceType.GetMembers().OfType<IMethodSymbol>())
        {
            if (member.MethodKind != MethodKind.Ordinary) continue;

            ITypeSymbol? paramType = null;
            string? paramName = null;
            string? ctName = null;

            foreach (var p in member.Parameters)
            {
                if (IsCancellationToken(p.Type, compilation))
                {
                    ctName = p.Name;
                }
                else if (paramType == null)
                {
                    paramType = p.Type;
                    paramName = p.Name;
                }
            }

            bool isAsync = IsTask(member.ReturnType, compilation);

            methods.Add(new MethodInfo(member, paramType, paramName, ctName, isAsync));

            if (paramType != null) AddContractTypes(contractTypes, allTypes, paramType, compilation);
            
            var unwrappedReturn = UnwrapTask(member.ReturnType, compilation);
            if (unwrappedReturn != null && IsRelevantType(unwrappedReturn))
            {
                AddContractTypes(contractTypes, allTypes, unwrappedReturn, compilation);
            }
        }

        var problemDetails = compilation.GetTypeByMetadataName("Comptatata.Http.ProblemDetails");
        if (problemDetails != null) AddContractTypes(contractTypes, allTypes, problemDetails, compilation);

        var validationProblemDetails = compilation.GetTypeByMetadataName("Comptatata.Http.ValidationProblemDetails");
        if (validationProblemDetails != null) AddContractTypes(contractTypes, allTypes, validationProblemDetails, compilation);

        if (allTypes.Count > 0)
        {
            var messageBase = compilation.GetTypeByMetadataName("Comptatata.SpoolDrop.Messages.Message");
            var eventBase = compilation.GetTypeByMetadataName("Comptatata.SpoolDrop.Messages.Event");
            if (messageBase != null) AddWithHierarchy(allTypes, messageBase);
            if (eventBase != null) AddWithHierarchy(allTypes, eventBase);
        }

        // Search for concrete descendants for all types in the hierarchy
        foreach (var type in allTypes.ToList())
        {
            if (type.IsAbstract || type.TypeKind == TypeKind.Interface || (type is INamedTypeSymbol classSymbol && !classSymbol.IsSealed && classSymbol.TypeKind == TypeKind.Class))
            {
                try
                {
                    JsonSerializerContextEmitter.AddConcreteDescendants(allTypes, type, compilation.GlobalNamespace, AddWithHierarchy);
                }
                catch (Exception) { }
            }
        }

        return new InterfaceInfo(allTypes, methods);
    }

    private static ITypeSymbol? UnwrapTask(ITypeSymbol type, Compilation compilation)
    {
        if (type is INamedTypeSymbol named && IsTask(named, compilation))
        {
            return named.IsGenericType ? named.TypeArguments[0] : null;
        }
        return type;
    }

    private static void AddContractTypes(HashSet<ITypeSymbol> contractTypes, HashSet<ITypeSymbol> allTypes, ITypeSymbol type, Compilation compilation)
    {
        if (type is IArrayTypeSymbol array) { AddContractTypes(contractTypes, allTypes, array.ElementType, compilation); return; }
        if (type is INamedTypeSymbol named && named.IsGenericType)
        {
            foreach (var arg in named.TypeArguments) AddContractTypes(contractTypes, allTypes, arg, compilation);
        }
        
        if (IsRelevantType(type))
        {
            contractTypes.Add(type);
            AddWithHierarchy(allTypes, type);
        }
    }

    private static void AddWithHierarchy(HashSet<ITypeSymbol> types, ITypeSymbol type)
    {
        var current = type;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            if (IsRelevantType(current)) types.Add(current);
            current = current.BaseType;
        }
    }

    private static bool IsRelevantType(ITypeSymbol type)
    {
        if (type == null || type.TypeKind == TypeKind.Error || type.SpecialType == SpecialType.System_Void) return false;
        if (type.SpecialType == SpecialType.System_Object) return false;

        // Exclude primitives and string
        if (type.SpecialType is >= SpecialType.System_Boolean and <= SpecialType.System_String) return false;
        if (type.SpecialType is SpecialType.System_DateTime) return false;

        // Exclude delegates
        if (type.TypeKind == TypeKind.Delegate) return false;
        if (type.BaseType?.Name == "MulticastDelegate" || type.BaseType?.Name == "Delegate") return false;

        // Exclude compiler-generated types and open generics
        if (type.Name.Contains("<") || type.Name.Contains("__")) return false;
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
        if (ns.StartsWith("Microsoft.")) return false;
        if (ns.StartsWith("System.Text.Json")) return false;
        if (ns.StartsWith("System.Threading.Tasks")) return false;
        if (ns.StartsWith("System.Security")) return false;
        if (ns.StartsWith("System.Threading")) return false;
        if (ns.StartsWith("System.Reflection")) return false;
        if (ns.StartsWith("System.Runtime")) return false;
        if (ns.StartsWith("System.Net.Http")) return false;
        if (ns.StartsWith("JetBrains.Annotations")) return false;
        if (ns.StartsWith("System.Collections")) return false;

        if (ns == "System")
        {
            if (type.Name is "IServiceProvider" or "CancellationToken" or "ValueType" or "Enum" or "Guid" or "DateTimeOffset" or "TimeSpan" or "Uri" or "Version" or "Type" or "RuntimeTypeHandle" or "Delegate" or "MulticastDelegate") return false;
            if (type.Name is "Func" or "Action" || type.Name.StartsWith("Func`") || type.Name.StartsWith("Action`")) return false;
        }

        return true;
    }
}
