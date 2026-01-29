using System.Collections.Immutable;
using System.Text;
using Comptatata.CodeAnalysis.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Comptatata.CodeAnalysis.Http;

[Generator(LanguageNames.CSharp)]
public class HttpClientGenerator : IIncrementalGenerator
{
    static readonly HashSet<string> CommonMethods = new(StringComparer.Ordinal)
                                                    {
                                                        "Delete",
                                                        "Get",
                                                        "Head",
                                                        "Options",
                                                        "Patch",
                                                        "Post",
                                                        "Put",
                                                        "Trace"
                                                    };

    static readonly string[] KnownMethods = new[]
        {
            "Acl",
            "BaselineControl",
            "Bind",
            "Checkin",
            "Checkout",
            "Connect",
            "Copy",
            "Delete",
            "Get",
            "Head",
            "Label",
            "Link",
            "Lock",
            "Merge",
            "MkActivity",
            "MkCalendar",
            "MkCol",
            "MkRedirectRef",
            "MkWorkspace",
            "Move",
            "Options",
            "OrderPatch",
            "Patch",
            "Post",
            "Pri",
            "PropFind",
            "PropPatch",
            "Put",
            "Query",
            "Rebind",
            "Report",
            "Search",
            "Trace",
            "Unbind",
            "Uncheckout",
            "Unlink",
            "Unlock",
            "Update",
            "UpdateRedirectRef",
            "VersionControl"
        }.OrderByDescending(m => m.Length)
         .ToArray();

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var interfaceTypes = context.SyntaxProvider
                                    .CreateSyntaxProvider(static (s, _) => IsCreateInvocation(s),
                                                          static (ctx, _) => GetRegistration(ctx))
                                    .Where(static r => r is not null)
                                    .Select(static (r, _) => r!);

        context.RegisterSourceOutput(interfaceTypes.Collect().Combine(context.CompilationProvider),
                                     static (spc, source) => Execute(source.Left, source.Right, spc));
    }

    static bool IsCreateInvocation(SyntaxNode node)
    {
        if (node is InvocationExpressionSyntax invocation)
        {
            var expression = invocation.Expression;
            if (expression is MemberAccessExpressionSyntax memberAccess)
                return memberAccess.Name.Identifier.Text == "Create";
            if (expression is IdentifierNameSyntax identifier) return identifier.Identifier.Text == "Create";
            if (expression is GenericNameSyntax genericName) return genericName.Identifier.Text == "Create";
        }

        return false;
    }

    static Registration? GetRegistration(GeneratorSyntaxContext context)
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
                if (typeSyntax != null) interfaceType = context.SemanticModel.GetTypeInfo(typeSyntax).Type;
            }
            else if (invocation.Expression is GenericNameSyntax g2)
            {
                var typeSyntax = g2.TypeArgumentList.Arguments.FirstOrDefault();
                if (typeSyntax != null) interfaceType = context.SemanticModel.GetTypeInfo(typeSyntax).Type;
            }
        }

        if (interfaceType == null || interfaceType.TypeKind != TypeKind.Interface) return null;

        return new(interfaceType, invocation.SyntaxTree.FilePath);
    }

    static void Execute(ImmutableArray<Registration> registrations,
                        Compilation compilation,
                        SourceProductionContext context)
    {
        var distinctRegistrations = registrations.Distinct(RegistrationComparer.Instance).ToList();
        if (distinctRegistrations.Count == 0) return;

        var firstSourcePath = distinctRegistrations.Select(r => r.SourceFilePath)
                                                   .FirstOrDefault(p => !string.IsNullOrEmpty(p));

        var projectDirectory = GeneratorManifest.FindProjectRoot(firstSourcePath);
        if (projectDirectory == null) return;

        // Normalize project directory for comparison
        projectDirectory = Path.GetFullPath(projectDirectory);

        // Track already-added hintNames to avoid duplicates
        var addedHintNames = new HashSet<string>(StringComparer.Ordinal);

        // Load manifest for tracking
        var manifest = GeneratorManifest.LoadOrCreate("Http",
                                                      projectDirectory,
                                                      "// <auto-generated by=\"Comptatata.CodeAnalysis.Http\" />",
                                                      compilation.AssemblyName);

        // Build current source file set and process registrations
        var currentSourceFiles = new HashSet<string>(StringComparer.Ordinal);

        // Group by source file path - each source file gets its own generated file
        // with its own uniquely-named serialization context.
        // NO deduplication - each usage site gets its own serializer.
        var byOutputPath = distinctRegistrations.GroupBy(r => r.SourceFilePath)
                                                .Where(g => !string.IsNullOrEmpty(g.Key));

        foreach (var group in byOutputPath)
        {
            var sourcePath = group.Key;

            var directory = Path.GetDirectoryName(sourcePath);
            if (directory == null) continue;

            // Check if output is within project directory
            // If the interface is defined in another project, we shouldn't generate it here
            // unless we can't determine the location (3rd party) in which case we fallback to local generation?
            // Actually, if it's 3rd party, DeclaringSyntaxReferences is likely empty/metadata, so sourcePath would be r.SourceFilePath (local).
            // If it's Project Reference, sourcePath is remote.

            var fullDirectory = Path.GetFullPath(directory);
            if (!fullDirectory.StartsWith(projectDirectory, StringComparison.OrdinalIgnoreCase))
                // Skip generation for external files
                continue;

            currentSourceFiles.Add(Path.GetFullPath(sourcePath));

            var sourceFileName = Path.GetFileNameWithoutExtension(sourcePath);
            var outputPath = Path.Combine(directory, $"{sourceFileName}.Http.generated.cs");

            // Check if file was renamed (old path in manifest with different output)
            var previousPath = manifest.GetPreviousGeneratedPath(sourcePath);
            if (previousPath != null && previousPath != outputPath)
                manifest.HandleFileRename(sourcePath, sourcePath, outputPath);

            // Take only the first registration for each source file
            var reg = group.First();
            try
            {
                var symbolNames =
                    GenerateFileForInterface(reg, compilation, context, manifest, sourcePath, addedHintNames);
                manifest.RecordGeneration(sourcePath, outputPath, symbolNames);
            }
            catch (Exception ex)
            {
                // Report diagnostic for generator errors
                var descriptor = new DiagnosticDescriptor("HTTPGEN001",
                                                          "HTTP Client Generator Error",
                                                          "Error generating HTTP client: {0}",
                                                          "CodeGeneration",
                                                          DiagnosticSeverity.Error,
                                                          true);
                context.ReportDiagnostic(Diagnostic.Create(descriptor, Location.None, ex.Message));
            }
        }

        // Clean up entries that no longer have registrations
        manifest.CleanupStaleEntries(currentSourceFiles);

        // Save manifest
        manifest.Save("Http", projectDirectory, compilation.AssemblyName);
    }

    static List<string> GenerateFileForInterface(Registration reg,
                                                 Compilation compilation,
                                                 SourceProductionContext context,
                                                 GeneratorManifest manifest,
                                                 string sourcePath,
                                                 HashSet<string> addedHintNames)
    {
        var symbolNames = new List<string>();
        var interfaceType = (INamedTypeSymbol)reg.InterfaceType;
        symbolNames.Add(interfaceType.Name);
        var directory = Path.GetDirectoryName(sourcePath);
        if (directory == null) return symbolNames;

        var sourceFileName = Path.GetFileNameWithoutExtension(sourcePath);
        var outputPath = Path.Combine(directory, $"{sourceFileName}.Http.generated.cs");

        var ns = interfaceType.ContainingNamespace.IsGlobalNamespace
            ? ""
            : interfaceType.ContainingNamespace.ToDisplayString();

        var syntaxTree = compilation.SyntaxTrees.FirstOrDefault(t => t.FilePath == sourcePath);
        if (syntaxTree != null)
        {
            var root = syntaxTree.GetRoot();
            var namespaceDecl = root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
            if (namespaceDecl != null)
                ns = namespaceDecl.Name.ToString();
            else
                ns = "";
        }

        var info = GetInterfaceInfo(interfaceType, compilation, context);

        // Use source file name + interface name for unique serializer class name.
        // Each source file gets its own serialization context to avoid STJ hintName conflicts.
        var serializerClassName = $"{sourceFileName}{interfaceType.Name}Serializer";

        // === DISK FILE: Attributes only for STJ source generator ===
        var diskSb = new StringBuilder();
        diskSb.AppendLine("// <auto-generated by=\"Comptatata.CodeAnalysis.Http\" />");
        diskSb.AppendLine("// This file contains only STJ attributes. Runtime code is in the .g.cs file.");
        diskSb.AppendLine("#nullable enable");
        diskSb.AppendLine();
        diskSb.AppendLine("using System.Text.Json.Serialization;");
        diskSb.AppendLine();

        if (!string.IsNullOrEmpty(ns))
        {
            diskSb.AppendLine($"namespace {ns};");
            diskSb.AppendLine();
        }

        JsonSerializerContextEmitter.EmitAttributesOnly(diskSb, serializerClassName, "internal", info.MessageGraph);

        var diskContent = diskSb.ToString();
        try
        {
            if (!File.Exists(outputPath) || File.ReadAllText(outputPath) != diskContent)
                File.WriteAllText(outputPath, diskContent);
        }
        catch (Exception) { }

        // === ADDSOURCE: Runtime members and implementation ===
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated by=\"Comptatata.CodeAnalysis.Http\" />");
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

        if (!string.IsNullOrEmpty(ns))
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        JsonSerializerContextEmitter.EmitRuntimeMembers(sb,
                                                        serializerClassName,
                                                        "internal",
                                                        info.MessageGraph,
                                                        "SerializerOptions");
        sb.AppendLine();

        sb.AppendLine($"internal partial class {serializerClassName}");
        sb.AppendLine("{");
        sb.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("    public static void Initialize()");
        sb.AppendLine("    {");
        sb.AppendLine(
            $"        global::Comptatata.Http.Factory<{interfaceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>.SetFactory(client => new {interfaceType.Name}Implementation(client));");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("[EditorBrowsable(EditorBrowsableState.Never)]");
        sb.AppendLine(
            $"file class {interfaceType.Name}Implementation : {interfaceType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}, global::System.IAsyncDisposable");
        sb.AppendLine("{");
        sb.AppendLine("    private readonly HttpClient _client;");
        sb.AppendLine($"    public {interfaceType.Name}Implementation(HttpClient client)");
        sb.AppendLine("    {");
        sb.AppendLine("        _client = client;");
        sb.AppendLine($"        _ = {serializerClassName}.SerializerOptions;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public ValueTask DisposeAsync()");
        sb.AppendLine("    {");
        sb.AppendLine("        _client.Dispose();");
        sb.AppendLine("        return ValueTask.CompletedTask;");
        sb.AppendLine("    }");
        sb.AppendLine();

        foreach (var method in info.Methods)
        {
            var parameters = string.Join(", ",
                                         method.Method.Parameters.Select(p =>
                                         {
                                             var attr =
                                                 method.IsStreaming && p.Name == method.CancellationTokenParameterName
                                                     ? "[global::System.Runtime.CompilerServices.EnumeratorCancellation] "
                                                     : "";
                                             return
                                                 $"{attr}{p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {p.Name}";
                                         }));
            var asyncKeyword = method.IsAsync ? "async " : "";

            var unwrappedReturn = JsonSerializerContextEmitter.UnwrapTask(method.Method.ReturnType);
            var isHttpResponseMessage = unwrappedReturn is not null &&
                                        IsHttpResponseMessage(unwrappedReturn, compilation);

            ITypeSymbol? rawEntityType = null;
            var isRawResponse = unwrappedReturn is not null &&
                                TryGetHttpResponseEntityType(unwrappedReturn, compilation, out rawEntityType);

            sb.AppendLine(
                $"    public {asyncKeyword}{method.Method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {method.Method.Name}({parameters})");
            sb.AppendLine("    {");

            var urlPath = GetUrlPath(method.Method.Name, out var httpMethod);
            var requestMethod = CommonMethods.Contains(httpMethod)
                ? $"global::System.Net.Http.HttpMethod.{httpMethod}"
                : $"global::System.Net.Http.HttpMethod.Parse(\"{httpMethod.ToUpperInvariant()}\")";
            var ctPart = method.CancellationTokenParameterName != null
                ? $", {method.CancellationTokenParameterName}"
                : "";

            var ctArg = method.CancellationTokenParameterName ?? "default";
            var contentFactory = "null";
            if (method.ParameterType != null)
            {
                var paramName = method.ParameterName;
                var paramTypeStr = method.ParameterType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                contentFactory =
                    $"() => global::System.Net.Http.Json.JsonContent.Create<{paramTypeStr}>({paramName}, {serializerClassName}.Generated.{JsonSerializerContextEmitter.GetPropertyName(method.ParameterType)})";
            }

            if (isHttpResponseMessage)
            {
                if (method.ParameterType != null)
                {
                    sb.AppendLine($"        var content = {contentFactory}();");
                    sb.AppendLine(
                        $"        using var request = new HttpRequestMessage({requestMethod}, \"{urlPath}\") {{ Content = content }};");
                }
                else
                {
                    sb.AppendLine(
                        $"        using var request = new HttpRequestMessage({requestMethod}, \"{urlPath}\");");
                }

                sb.AppendLine(
                    $"        return await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead{ctPart}).ConfigureAwait(false);");
                sb.AppendLine("    }");
                continue;
            }

            if (method.IsStreaming)
            {
                var streamingTypeStr = method.StreamingType!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var streamingTypePropertyName = JsonSerializerContextEmitter.GetPropertyName(method.StreamingType!);

                sb.AppendLine(
                    $"        var currentUri = new global::System.Uri(\"{urlPath}\", global::System.UriKind.RelativeOrAbsolute);");
                sb.AppendLine($"        var currentMethod = {requestMethod};");
                if (contentFactory != "null")
                {
                    sb.AppendLine($"        var contentFunc = {contentFactory};");
                    sb.AppendLine(
                        "        using var request = new HttpRequestMessage(currentMethod, currentUri) { Content = contentFunc() };");
                }
                else
                {
                    sb.AppendLine("        using var request = new HttpRequestMessage(currentMethod, currentUri);");
                }

                sb.AppendLine(
                    $"        using var response = await _client.SendAsync(request, global::System.Net.Http.HttpCompletionOption.ResponseHeadersRead, {ctArg}).ConfigureAwait(false);");
                sb.AppendLine("        if (!response.IsSuccessStatusCode)");
                sb.AppendLine("        {");
                sb.AppendLine(
                    $"            await global::Comptatata.Http.HttpClientHelper.RequestPocoVoidAsync(_client, currentUri, currentMethod, {contentFactory}, {serializerClassName}.Generated.ProblemDetails, {ctArg}).ConfigureAwait(false);");
                sb.AppendLine("        }");
                sb.AppendLine();
                sb.AppendLine(
                    $"        var stream = await response.Content.ReadAsStreamAsync({ctArg}).ConfigureAwait(false);");
                sb.AppendLine("        var contentType = response.Content.Headers.ContentType?.ToString();");
                sb.AppendLine(
                    "        if (contentType is not null && contentType.Contains(\"ndjson\", global::System.StringComparison.OrdinalIgnoreCase))");
                sb.AppendLine("        {");
                sb.AppendLine("            using var reader = new global::System.IO.StreamReader(stream);");
                sb.AppendLine(
                    $"            while (await reader.ReadLineAsync({ctArg}).ConfigureAwait(false) is string line)");
                sb.AppendLine("            {");
                sb.AppendLine("                if (string.IsNullOrWhiteSpace(line)) continue;");
                sb.AppendLine(
                    $"                var item = global::System.Text.Json.JsonSerializer.Deserialize<{streamingTypeStr}>(line, {serializerClassName}.Generated.{streamingTypePropertyName});");
                sb.AppendLine("                if (item is not null) yield return item;");
                sb.AppendLine("            }");
                sb.AppendLine("        }");
                sb.AppendLine("        else");
                sb.AppendLine("        {");
                sb.AppendLine(
                    $"            await foreach (var item in global::System.Text.Json.JsonSerializer.DeserializeAsyncEnumerable<{streamingTypeStr}>(stream, {serializerClassName}.Generated.{streamingTypePropertyName}, {ctArg}).ConfigureAwait(false))");
                sb.AppendLine("            {");
                sb.AppendLine("                if (item is not null) yield return item;");
                sb.AppendLine("            }");
                sb.AppendLine("        }");
                sb.AppendLine("    }");
                continue;
            }

            sb.AppendLine(
                $"        var currentUri = new global::System.Uri(\"{urlPath}\", global::System.UriKind.RelativeOrAbsolute);");
            sb.AppendLine($"        var currentMethod = {requestMethod};");

            if (method.Method.ReturnType is INamedTypeSymbol returnType && returnType.IsGenericType &&
                JsonSerializerContextEmitter.IsTask(returnType))
            {
                var resultType = returnType.TypeArguments[0];

                if (isRawResponse)
                {
                    sb.AppendLine("        return await global::Comptatata.Http.HttpClientHelper.RequestMessageAsync(");
                    sb.AppendLine($"            _client, currentUri, currentMethod, {contentFactory},");
                    sb.AppendLine(
                        $"            {serializerClassName}.Generated.{JsonSerializerContextEmitter.GetPropertyName(rawEntityType!)},");
                    sb.AppendLine(
                        $"            {serializerClassName}.Generated.ProblemDetails, {ctArg}).ConfigureAwait(false);");
                }
                else
                {
                    sb.AppendLine("        return await global::Comptatata.Http.HttpClientHelper.RequestPocoAsync(");
                    sb.AppendLine($"            _client, currentUri, currentMethod, {contentFactory},");
                    sb.AppendLine(
                        $"            {serializerClassName}.Generated.{JsonSerializerContextEmitter.GetPropertyName(resultType)},");
                    sb.AppendLine(
                        $"            {serializerClassName}.Generated.ProblemDetails, {ctArg}).ConfigureAwait(false);");
                }
            }
            else
            {
                sb.AppendLine("        await global::Comptatata.Http.HttpClientHelper.RequestPocoVoidAsync(");
                sb.AppendLine($"            _client, currentUri, currentMethod, {contentFactory},");
                sb.AppendLine(
                    $"            {serializerClassName}.Generated.ProblemDetails, {ctArg}).ConfigureAwait(false);");
            }

            sb.AppendLine("    }");
        }

        sb.AppendLine("}");
        sb.AppendLine();

        // Add runtime code via AddSource for IDE incremental updates
        // Use source file name + interface name to ensure uniqueness across projects
        var runtimeContent = sb.ToString();
        var hintName = $"{sourceFileName}.{interfaceType.Name}.g.cs";

        // Only add if not already added (prevents duplicates when same interface used multiple times)
        if (addedHintNames.Add(hintName)) context.AddSource(hintName, runtimeContent);

        return symbolNames;
    }

    static string GetUrlPath(string methodName, out string httpMethod)
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
        if (methodName.EndsWith("Async", StringComparison.Ordinal))
            methodName = methodName.Substring(0, methodName.Length - 5);

        if (methodName.EndsWith("Message", StringComparison.Ordinal))
            methodName = methodName.Substring(0, methodName.Length - 7);

        if (methodName.EndsWith("Poco", StringComparison.Ordinal))
            methodName = methodName.Substring(0, methodName.Length - 4);

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

        for (var i = 1; i < methodName.Length; i++)
        {
            var c = methodName[i];
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

    static bool TryGetHttpResponseEntityType(ITypeSymbol type, Compilation compilation, out ITypeSymbol? entityType)
    {
        entityType = null;
        if (type is not INamedTypeSymbol named || !named.IsGenericType) return false;

        var httpResponseType = compilation.GetTypeByMetadataName("Comptatata.Http.HttpResponse`1");
        if (httpResponseType is null) return false;

        if (!SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, httpResponseType)) return false;

        entityType = named.TypeArguments[0];
        return true;
    }

    static bool IsCancellationToken(ITypeSymbol type, Compilation compilation)
    {
        var ctType = compilation.GetTypeByMetadataName("System.Threading.CancellationToken");
        return SymbolEqualityComparer.Default.Equals(type, ctType);
    }

    static InterfaceInfo GetInterfaceInfo(INamedTypeSymbol interfaceType,
                                          Compilation compilation,
                                          SourceProductionContext context)
    {
        var graph = new JsonSerializerContextEmitter.SerializationGraph();
        var methods = new List<MethodInfo>();
        var seenMethods = new HashSet<string>(StringComparer.Ordinal);
        var allTypesInCompilation = JsonSerializerContextEmitter.GetAllTypesInCompilation(compilation);

        foreach (var member in interfaceType.GetMembers().OfType<IMethodSymbol>())
        {
            if (member.MethodKind != MethodKind.Ordinary) continue;

            // Deduplicate by method signature to handle potential Roslyn symbol duplication
            var methodKey = member.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            if (!seenMethods.Add(methodKey)) continue;

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

            // Check for IAsyncEnumerable<T> directly first (not wrapped in Task)
            var isStreaming = JsonSerializerContextEmitter.IsAsyncEnumerable(member.ReturnType);
            var streamingReturnType = member.ReturnType;

            // If not streaming, check if it's Task<IAsyncEnumerable<T>>
            if (!isStreaming)
            {
                var unwrapped = JsonSerializerContextEmitter.UnwrapTask(member.ReturnType);
                if (unwrapped != null && JsonSerializerContextEmitter.IsAsyncEnumerable(unwrapped))
                {
                    isStreaming = true;
                    streamingReturnType = unwrapped;
                }
            }

            var isAsync = JsonSerializerContextEmitter.IsTask(member.ReturnType) || isStreaming;
            var streamingType = isStreaming ? ((INamedTypeSymbol)streamingReturnType).TypeArguments[0] : null;

            methods.Add(new(member, paramType, paramName, ctName, isAsync || isStreaming, isStreaming, streamingType));

            if (paramType != null)
                JsonSerializerContextEmitter.AddSerializableTypes(graph,
                                                                  paramType,
                                                                  compilation,
                                                                  allTypesInCompilation,
                                                                  context.ReportDiagnostic);

            var unwrappedReturn = JsonSerializerContextEmitter.UnwrapTask(member.ReturnType);
            if (unwrappedReturn != null)
            {
                var serializableType = unwrappedReturn;
                if (JsonSerializerContextEmitter.IsAsyncEnumerable(serializableType))
                    serializableType = ((INamedTypeSymbol)serializableType).TypeArguments[0];
                JsonSerializerContextEmitter.AddSerializableTypes(graph,
                                                                  serializableType,
                                                                  compilation,
                                                                  allTypesInCompilation,
                                                                  context.ReportDiagnostic);
            }
            else if (isStreaming && streamingType != null)
            {
                JsonSerializerContextEmitter.AddSerializableTypes(graph,
                                                                  streamingType,
                                                                  compilation,
                                                                  allTypesInCompilation,
                                                                  context.ReportDiagnostic);
            }
        }

        var problemDetails = compilation.GetTypeByMetadataName("Comptatata.Http.ProblemDetails");
        if (problemDetails != null)
            JsonSerializerContextEmitter.AddSerializableTypes(graph,
                                                              problemDetails,
                                                              compilation,
                                                              allTypesInCompilation,
                                                              context.ReportDiagnostic);

        var validationProblemDetails = compilation.GetTypeByMetadataName("Comptatata.Http.ValidationProblemDetails");
        if (validationProblemDetails != null)
            JsonSerializerContextEmitter.AddSerializableTypes(graph,
                                                              validationProblemDetails,
                                                              compilation,
                                                              allTypesInCompilation,
                                                              context.ReportDiagnostic);

        return new(graph, methods);
    }

    static bool IsHttpResponseMessage(ITypeSymbol type, Compilation compilation)
    {
        var httpResponseMessageType = compilation.GetTypeByMetadataName("System.Net.Http.HttpResponseMessage");
        return SymbolEqualityComparer.Default.Equals(type, httpResponseMessageType);
    }

    class Registration
    {
        public Registration(ITypeSymbol interfaceType, string sourceFilePath)
        {
            InterfaceType = interfaceType;
            SourceFilePath = sourceFilePath;
        }

        public ITypeSymbol InterfaceType { get; }
        public string SourceFilePath { get; }
    }

    class RegistrationComparer : IEqualityComparer<Registration>
    {
        public static RegistrationComparer Instance { get; } = new();

        public bool Equals(Registration x, Registration y) =>
            SymbolEqualityComparer.Default.Equals(x?.InterfaceType, y?.InterfaceType);

        public int GetHashCode(Registration obj) => SymbolEqualityComparer.Default.GetHashCode(obj.InterfaceType);
    }

    class InterfaceInfo
    {
        public InterfaceInfo(JsonSerializerContextEmitter.SerializationGraph messageGraph, List<MethodInfo> methods)
        {
            MessageGraph = messageGraph;
            Methods = methods;
        }

        public JsonSerializerContextEmitter.SerializationGraph MessageGraph { get; }
        public List<MethodInfo> Methods { get; }
    }

    class MethodInfo
    {
        public MethodInfo(IMethodSymbol method,
                          ITypeSymbol? parameterType,
                          string? parameterName,
                          string? cancellationTokenParameterName,
                          bool isAsync,
                          bool isStreaming,
                          ITypeSymbol? streamingType)
        {
            Method = method;
            ParameterType = parameterType;
            ParameterName = parameterName;
            CancellationTokenParameterName = cancellationTokenParameterName;
            IsAsync = isAsync;
            IsStreaming = isStreaming;
            StreamingType = streamingType;
        }

        public IMethodSymbol Method { get; }
        public ITypeSymbol? ParameterType { get; }
        public string? ParameterName { get; }
        public string? CancellationTokenParameterName { get; }
        public bool IsAsync { get; }
        public bool IsStreaming { get; }
        public ITypeSymbol? StreamingType { get; }
    }
}