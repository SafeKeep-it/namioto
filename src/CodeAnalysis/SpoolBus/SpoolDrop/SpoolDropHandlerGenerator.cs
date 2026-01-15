using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Comptatata.CodeAnalysis.Common;

namespace Comptatata.CodeAnalysis.JsonPolymorphic.SpoolDrop;

[Generator(LanguageNames.CSharp)]
public class SpoolDropHandlerGenerator : IIncrementalGenerator
{
    private enum RegistrationType { Handler, Client }
    private class Registration
    {
        public ITypeSymbol Type { get; }
        public RegistrationType Kind { get; }
        public string SourceFilePath { get; }
        public Registration(ITypeSymbol type, RegistrationType kind, string sourceFilePath)
        {
            Type = type;
            Kind = kind;
            SourceFilePath = sourceFilePath;
        }
    }

    private class RegistrationComparer : IEqualityComparer<Registration>
    {
        public static RegistrationComparer Instance { get; } = new RegistrationComparer();
        public bool Equals(Registration x, Registration y) => SymbolEqualityComparer.Default.Equals(x?.Type, y?.Type) && x?.Kind == y?.Kind;
        public int GetHashCode(Registration obj) => (SymbolEqualityComparer.Default.GetHashCode(obj.Type) * 397) ^ obj.Kind.GetHashCode();
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var handlerTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsAddHandlerInvocation(s),
                transform: static (ctx, _) => GetRegistration(ctx, RegistrationType.Handler))
            .Where(static r => r is not null)
            .Select(static (r, _) => r!);

        var clientTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsCreateClientInvocation(s),
                transform: static (ctx, _) => GetRegistration(ctx, RegistrationType.Client))
            .Where(static r => r is not null)
            .Select(static (r, _) => r!);

        var allRegistrations = handlerTypes.Collect()
            .Combine(clientTypes.Collect());

        context.RegisterSourceOutput(allRegistrations.Combine(context.CompilationProvider),
            static (spc, source) => Execute(
                source.Left.Left.AddRange(source.Left.Right), 
                source.Right, spc));
    }

    private static Registration? GetRegistration(GeneratorSyntaxContext context, RegistrationType kind)
    {
        var type = kind == RegistrationType.Handler ? GetHandlerType(context) : GetClientType(context);
        if (type == null) return null;
        return new Registration(type, kind, context.Node.SyntaxTree.FilePath);
    }

    private static bool IsAddHandlerInvocation(SyntaxNode node)
    {
        return node is InvocationExpressionSyntax invocation &&
               (invocation.Expression is MemberAccessExpressionSyntax memberAccess && memberAccess.Name.Identifier.Text == "AddHandler" ||
                invocation.Expression is IdentifierNameSyntax identifier && identifier.Identifier.Text == "AddHandler");
    }

    private static ITypeSymbol? GetHandlerType(GeneratorSyntaxContext context)
    {
        if (context.Node.SyntaxTree.FilePath.EndsWith(".generated.cs")) return null;
        var invocation = (InvocationExpressionSyntax)context.Node;
        var symbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;

        if (symbol is null || symbol.Name != "AddHandler") return null;

        var containingType = symbol.ContainingType?.ToDisplayString();
        if (containingType != "Comptatata.SpoolBus.SpoolDropServer") return null;

        return symbol.TypeArguments.FirstOrDefault();
    }

    private static bool IsCreateClientInvocation(SyntaxNode node)
    {
        return node is InvocationExpressionSyntax invocation &&
               (invocation.Expression is MemberAccessExpressionSyntax memberAccess && memberAccess.Name.Identifier.Text == "CreateClient" ||
                invocation.Expression is IdentifierNameSyntax identifier && identifier.Identifier.Text == "CreateClient");
    }

    private static ITypeSymbol? GetClientType(GeneratorSyntaxContext context)
    {
        if (context.Node.SyntaxTree.FilePath.EndsWith(".generated.cs")) return null;
        var invocation = (InvocationExpressionSyntax)context.Node;
        var symbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;

        if (symbol is null || symbol.Name != "CreateClient") return null;

        var containingType = symbol.ContainingType?.ToDisplayString();
        if (containingType != "Comptatata.SpoolBus.SpoolBusClientFactory") return null;

        return symbol.TypeArguments.FirstOrDefault();
    }

    private class HandlerMethodInfo
    {
        public HandlerMethodInfo(IMethodSymbol method, ITypeSymbol parameterType, ITypeSymbol? messageResultType, bool isAsync, bool isOneWay, bool hasCancellationToken)
        {
            Method = method;
            ParameterType = parameterType;
            MessageResultType = messageResultType;
            IsAsync = isAsync;
            IsOneWay = isOneWay;
            HasCancellationToken = hasCancellationToken;
        }

        public IMethodSymbol Method { get; }
        public ITypeSymbol ParameterType { get; }
        public ITypeSymbol? MessageResultType { get; }
        public bool IsAsync { get; }
        public bool IsOneWay { get; }
        public bool HasCancellationToken { get; }
    }

    private static void Execute(ImmutableArray<Registration> registrations, Compilation compilation, SourceProductionContext context)
    {
        var distinctRegistrations = registrations.Distinct(RegistrationComparer.Instance).ToList();
        if (distinctRegistrations.Count == 0) return;

        var groups = distinctRegistrations
            .GroupBy(r => r.Type.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree.FilePath ?? r.SourceFilePath)
            .ToList();

        // Determine project directory from first registration's source file (call site)
        // We must use SourceFilePath because it guarantees to be in the current project
        var firstSourcePath = distinctRegistrations
            .Select(r => r.SourceFilePath)
            .FirstOrDefault(p => !string.IsNullOrEmpty(p));
        
        var projectDirectory = firstSourcePath != null ? Path.GetDirectoryName(firstSourcePath) : null;
        if (projectDirectory == null) return;
        
        // Normalize project directory
        projectDirectory = Path.GetFullPath(projectDirectory);

        // Load manifest for tracking
        var manifest = GeneratorManifest.LoadOrCreate("SpoolBus", projectDirectory, "// <auto-generated by=\"Comptatata.CodeAnalysis.SpoolBus\" />");

        // Build current source file set
        var currentSourceFiles = new HashSet<string>(StringComparer.Ordinal);

        foreach (var group in groups)
        {
            var sourcePath = group.Key;
            
            var directory = Path.GetDirectoryName(sourcePath);
            if (directory == null) continue;
            
            // Check if output is within project directory
            var fullDirectory = Path.GetFullPath(directory);
            if (!fullDirectory.StartsWith(projectDirectory, StringComparison.OrdinalIgnoreCase))
            {
                // Skip generation for external files
                continue;
            }

            currentSourceFiles.Add(sourcePath);

            var sourceFileName = Path.GetFileNameWithoutExtension(sourcePath);
            var outputPath = Path.Combine(directory, $"{sourceFileName}.generated.cs");

            // Check if file was renamed (old path in manifest with different output)
            var previousPath = manifest.GetPreviousGeneratedPath(sourcePath);
            if (previousPath != null && previousPath != outputPath)
            {
                manifest.HandleFileRename(sourcePath, sourcePath, outputPath);
            }

            var symbolNames = GenerateFileForGroup(sourcePath, group.ToList(), compilation, context);
            manifest.RecordGeneration(sourcePath, outputPath, symbolNames);
        }

        // Clean up entries that no longer have registrations
        manifest.CleanupStaleEntries(currentSourceFiles);

        // Save manifest
        manifest.Save("SpoolBus", projectDirectory);
    }

    private static List<string> GenerateFileForGroup(string sourcePath, List<Registration> registrations, Compilation compilation, SourceProductionContext context)
    {
        var symbolNames = new List<string>();
        var directory = Path.GetDirectoryName(sourcePath);
        if (directory == null) return symbolNames;

        var sourceFileName = Path.GetFileNameWithoutExtension(sourcePath);
        var outputPath = Path.Combine(directory, $"{sourceFileName}.generated.cs");
        if (sourceFileName.StartsWith("SpoolBus.Remote."))
        {
            foreach (var reg in registrations)
            {
                var oldPath = Path.Combine(directory, $"{sourceFileName}.{reg.Type.Name}.generated.cs");
                if (File.Exists(oldPath))
                {
                    try { File.Delete(oldPath); } catch { }
                }
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated by=\"Comptatata.CodeAnalysis.SpoolBus\" />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.ComponentModel;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine("using System.Text.Json.Serialization.Metadata;");
        sb.AppendLine("using Comptatata.SpoolBus;");
        sb.AppendLine("using Comptatata.SpoolDrop.Messages;");
        sb.AppendLine();

        var firstReg = registrations.First();
        var ns = firstReg.Type.ContainingNamespace.IsGlobalNamespace ? "" : firstReg.Type.ContainingNamespace.ToDisplayString();

        if (!string.IsNullOrEmpty(ns))
        {
            sb.AppendLine($"namespace {ns};");
            sb.AppendLine();
        }

        foreach (var reg in registrations)
        {
            var type = (INamedTypeSymbol)reg.Type;
            symbolNames.Add(type.Name);
            var info = GetHandlerInfo(type, compilation);
            if (info.Methods.Count == 0) continue;

            foreach (var messageType in info.MessageTypes.OrderBy(t => t.ToDisplayString()))
            {
                sb.AppendLine($"[JsonSerializable(typeof({messageType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}))]");
            }

            var contextClassName = reg.Kind == RegistrationType.Handler
                ? $"{type.Name}SpoolBusServerSerializer"
                : $"{type.Name}SpoolBusClientSerializer";

            sb.AppendLine("[global::System.Text.Json.Serialization.JsonSourceGenerationOptions(UseStringEnumConverter = true)]");
            sb.AppendLine($"internal partial class {contextClassName} : JsonSerializerContext");
            sb.AppendLine("{");
            sb.AppendLine("    public static global::System.Threading.Tasks.ValueTask<global::Comptatata.SpoolDrop.Messages.Message?> DeserializeAsync(global::System.IO.Stream stream, global::System.Threading.CancellationToken ct = default) =>");
            sb.AppendLine("        global::System.Text.Json.JsonSerializer.DeserializeAsync(stream, Generated.Message, ct);");
            sb.AppendLine();
            sb.AppendLine("    public static global::Comptatata.SpoolDrop.Messages.Message? Deserialize(global::System.IO.Stream stream) =>");
            sb.AppendLine("        global::System.Text.Json.JsonSerializer.Deserialize(stream, Generated.Message);");
            sb.AppendLine();
            sb.AppendLine("    public static global::System.Threading.Tasks.Task SerializeAsync(global::System.IO.Stream stream, global::Comptatata.SpoolDrop.Messages.Message message, global::System.Threading.CancellationToken ct = default) =>");
            sb.AppendLine("        global::System.Text.Json.JsonSerializer.SerializeAsync(stream, message, Generated.Message, ct);");
            sb.AppendLine();
            sb.AppendLine("    public static void Serialize(global::System.IO.Stream stream, global::Comptatata.SpoolDrop.Messages.Message message) =>");
            sb.AppendLine("        global::System.Text.Json.JsonSerializer.Serialize(stream, message, Generated.Message);");
            sb.AppendLine();
            sb.AppendLine("    public static JsonSerializerOptions SpoolBusOptions => field ??= ConstructPolymorphism();");
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
            
            JsonSerializerContextEmitter.EmitAddPolymorphism(sb, info.MessageTypes);
            
            sb.AppendLine();
            
            JsonSerializerContextEmitter.EmitGeneratedClass(sb, info.MessageTypes);
            
            sb.AppendLine("}");
            sb.AppendLine();

            sb.AppendLine($"internal partial class {contextClassName}");
            sb.AppendLine("{");
            sb.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializer]");
            sb.AppendLine("    public static void InitializeSpoolBus()");
            sb.AppendLine("    {");
            sb.AppendLine("        _ = SpoolBusOptions;");
            if (reg.Kind == RegistrationType.Handler)
            {
                sb.AppendLine($"        global::Comptatata.SpoolBus.SpoolBusInfrastructure<{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>.Default = new {type.Name}SpoolBusHandlerInfrastructure();");
            }
            else if (reg.Kind == RegistrationType.Client)
            {
                sb.AppendLine($"        global::Comptatata.SpoolBus.SpoolBusInfrastructure<{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>.Default = new {type.Name}SpoolBusClient(null!);");
            }
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();

            if (reg.Kind == RegistrationType.Handler)
            {
                sb.AppendLine("[EditorBrowsable(EditorBrowsableState.Never)]");
                sb.AppendLine($"file class {type.Name}SpoolBusHandlerInfrastructure : global::Comptatata.SpoolBus.ISpoolBusInfrastructure<{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>");
                sb.AppendLine("{");
                sb.AppendLine($"    public global::System.Threading.Tasks.ValueTask<global::Comptatata.SpoolDrop.Messages.Message?> DeserializeAsync(global::System.IO.Stream stream, global::System.Threading.CancellationToken ct = default) => {contextClassName}.DeserializeAsync(stream, ct);");
                sb.AppendLine($"    public global::Comptatata.SpoolDrop.Messages.Message? Deserialize(global::System.IO.Stream stream) => {contextClassName}.Deserialize(stream);");
                sb.AppendLine($"    public global::System.Threading.Tasks.Task SerializeAsync(global::System.IO.Stream stream, global::Comptatata.SpoolDrop.Messages.Message message, global::System.Threading.CancellationToken ct = default) => {contextClassName}.SerializeAsync(stream, message, ct);");
                sb.AppendLine($"    public void Serialize(global::System.IO.Stream stream, global::Comptatata.SpoolDrop.Messages.Message message) => {contextClassName}.Serialize(stream, message);");
                sb.AppendLine($"    public string GetDiscriminator(global::Comptatata.SpoolDrop.Messages.Message message) => {type.Name}SpoolBusDispatcher.GetDiscriminator(message);");
                sb.AppendLine();
                sb.AppendLine($"    public {type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} CreateClient(global::Comptatata.SpoolBus.SpoolBusClientFactory factory) => throw new global::System.NotSupportedException();");
                sb.AppendLine();
                sb.AppendLine($"    public global::System.Threading.Tasks.ValueTask<bool> DispatchAsync(");
                sb.AppendLine($"        global::System.Func<{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}> handlerFactory,");
                sb.AppendLine($"        global::System.IO.Stream stream,");
                sb.AppendLine($"        string directory,");
                sb.AppendLine($"        global::System.Threading.CancellationToken ct) => {type.Name}SpoolBusDispatcher.DispatchAsync(handlerFactory, stream, directory, ct);");
                sb.AppendLine("}");
                sb.AppendLine();

                sb.AppendLine("[EditorBrowsable(EditorBrowsableState.Never)]");
                sb.AppendLine($"file static class {type.Name}SpoolBusDispatcher");
                sb.AppendLine("{");
                sb.AppendLine($"    public static async global::System.Threading.Tasks.ValueTask<bool> DispatchAsync(");
                sb.AppendLine($"        global::System.Func<{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}> handlerFactory,");
                sb.AppendLine($"        global::System.IO.Stream stream,");
                sb.AppendLine($"        string directory,");
                sb.AppendLine($"        global::System.Threading.CancellationToken ct)");
                sb.AppendLine("    {");
                sb.AppendLine("        #pragma warning disable CS1998");
                sb.AppendLine($"        var request = await {contextClassName}.DeserializeAsync(stream, ct).ConfigureAwait(false);");
                sb.AppendLine($"        {type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}? handler = null;");
                sb.AppendLine("        var handled = false;");
                sb.AppendLine();

                foreach (var method in info.Methods)
                {
                    var awaitPrefix = method.IsAsync ? "await " : "";
                    var ctArg = method.HasCancellationToken ? ", ct" : "";
                    var awaitExpr = method.IsAsync ? ".ConfigureAwait(false)" : "";

                    sb.AppendLine($"        async global::System.Threading.Tasks.ValueTask<global::Comptatata.SpoolDrop.Messages.Message?> Invoke_{method.Method.Name}({method.ParameterType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} m)");
                    sb.AppendLine("        {");
                    if (method.IsOneWay)
                    {
                        sb.AppendLine($"            {awaitPrefix}(handler ??= handlerFactory()).{method.Method.Name}(m{ctArg}){awaitExpr};");
                        sb.AppendLine("            return null;");
                    }
                    else
                    {
                        sb.AppendLine($"            var result = {awaitPrefix}(handler ??= handlerFactory()).{method.Method.Name}(m{ctArg}){awaitExpr};");
                        sb.AppendLine($"            return result ?? throw new global::System.InvalidOperationException($\"Handler method '{method.Method.Name}' in '{type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}' returned null, which is not allowed.\");");
                    }
                    sb.AppendLine("        }");
                    sb.AppendLine();
                }

                foreach (var method in info.Methods)
                {
                    sb.AppendLine($"        if (request is {method.ParameterType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} m_{method.Method.Name})");
                    sb.AppendLine("        {");
                    sb.AppendLine($"            var response = await Invoke_{method.Method.Name}(m_{method.Method.Name}).ConfigureAwait(false);");
                    sb.AppendLine("            if (response != null)");
                    sb.AppendLine("            {");
                    sb.AppendLine("                if (response is global::Comptatata.SpoolDrop.Messages.Event e)");
                    sb.AppendLine("                {");
                    sb.AppendLine("                    response = e with { ReplyTo = request?.Id };");
                    sb.AppendLine("                }");
                    sb.AppendLine($"                await global::Comptatata.SpoolBus.SpoolBusInfrastructure.SendAsync(response, {contextClassName}.SerializeAsync, GetDiscriminator, directory, ct).ConfigureAwait(false);");
                    sb.AppendLine("            }");
                    sb.AppendLine("            handled = true;");
                    sb.AppendLine("        }");
                }

                sb.AppendLine("        return handled;");
                sb.AppendLine("        #pragma warning restore CS1998");
                sb.AppendLine("    }");
                sb.AppendLine();
                sb.AppendLine("    public static string GetDiscriminator(global::Comptatata.SpoolDrop.Messages.Message message) => message switch");
                sb.AppendLine("    {");
                foreach (var t in info.MessageTypes.Where(t => !t.IsAbstract).OrderBy(t => t.Name))
                {
                    sb.AppendLine($"        {t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} _ => \"{JsonSerializerContextEmitter.ToKebabCase(t.Name)}\",");
                }
                sb.AppendLine("        _ => \"unknown-message\"");
                sb.AppendLine("    };");
                sb.AppendLine("}");
                sb.AppendLine();
            }
            else if (reg.Kind == RegistrationType.Client)
            {
                sb.AppendLine("[EditorBrowsable(EditorBrowsableState.Never)]");
                sb.AppendLine($"file class {type.Name}SpoolBusClient : {type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}, global::Comptatata.SpoolBus.ISpoolBusInfrastructure<{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>");
                sb.AppendLine("{");
                sb.AppendLine("    private readonly global::Comptatata.SpoolBus.SpoolBusClientFactory _factory;");
                sb.AppendLine($"    public {type.Name}SpoolBusClient(global::Comptatata.SpoolBus.SpoolBusClientFactory factory) => _factory = factory;");
                sb.AppendLine();
                sb.AppendLine($"    public global::System.Threading.Tasks.ValueTask<global::Comptatata.SpoolDrop.Messages.Message?> DeserializeAsync(global::System.IO.Stream stream, global::System.Threading.CancellationToken ct = default) => {contextClassName}.DeserializeAsync(stream, ct);");
                sb.AppendLine($"    public global::Comptatata.SpoolDrop.Messages.Message? Deserialize(global::System.IO.Stream stream) => {contextClassName}.Deserialize(stream);");
                sb.AppendLine($"    public global::System.Threading.Tasks.Task SerializeAsync(global::System.IO.Stream stream, global::Comptatata.SpoolDrop.Messages.Message message, global::System.Threading.CancellationToken ct = default) => {contextClassName}.SerializeAsync(stream, message, ct);");
                sb.AppendLine($"    public void Serialize(global::System.IO.Stream stream, global::Comptatata.SpoolDrop.Messages.Message message) => {contextClassName}.Serialize(stream, message);");
                sb.AppendLine($"    public string GetDiscriminator(global::Comptatata.SpoolDrop.Messages.Message message) => {type.Name}SpoolBusClientHelper.GetDiscriminator(message);");
                sb.AppendLine();
                sb.AppendLine($"    public {type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} CreateClient(global::Comptatata.SpoolBus.SpoolBusClientFactory factory) => new {type.Name}SpoolBusClient(factory);");
                sb.AppendLine();
                sb.AppendLine($"    public global::System.Threading.Tasks.ValueTask<bool> DispatchAsync(global::System.Func<{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}> handlerFactory, global::System.IO.Stream stream, string directory, global::System.Threading.CancellationToken ct) => throw new global::System.NotSupportedException();");
                sb.AppendLine();

                var implementedMethods = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
                foreach (var method in info.Methods)
                {
                    implementedMethods.Add(method.Method);
                    var paramName = method.Method.Parameters[0].Name;
                    var ctParam = method.HasCancellationToken ? ", global::System.Threading.CancellationToken ct" : "";
                    var ctArg = method.HasCancellationToken ? ", ct" : "";
                    var isAsync = method.IsAsync || !method.IsOneWay;
                    var asyncKeyword = isAsync ? "async " : "";

                    sb.AppendLine($"    public {asyncKeyword}{method.Method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {method.Method.Name}({method.ParameterType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {paramName}{ctParam})");
                    sb.AppendLine("    {");

                    if (isAsync)
                    {
                        if (!method.IsOneWay)
                        {
                            sb.AppendLine($"        var responseTask = _factory.WaitForResponseAsync<{method.MessageResultType!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>({paramName}.Id, this{ctArg});");
                            sb.AppendLine($"        await global::Comptatata.SpoolBus.SpoolBusInfrastructure.SendAsync({paramName}, SerializeAsync, GetDiscriminator, _factory.Directory{ctArg}).ConfigureAwait(false);");
                            sb.AppendLine($"        return await responseTask.ConfigureAwait(false);");
                        }
                        else
                        {
                            sb.AppendLine($"        await global::Comptatata.SpoolBus.SpoolBusInfrastructure.SendAsync({paramName}, SerializeAsync, GetDiscriminator, _factory.Directory{ctArg}).ConfigureAwait(false);");
                            var returnType = method.Method.ReturnType.ToDisplayString();
                            if (returnType == "global::System.Threading.Tasks.Task")
                            {
                                sb.AppendLine("        return global::System.Threading.Tasks.Task.CompletedTask;");
                            }
                            else if (returnType == "global::System.Threading.Tasks.ValueTask")
                            {
                                sb.AppendLine("        return global::System.Threading.Tasks.ValueTask.CompletedTask;");
                            }
                            else if (method.Method.ReturnType is INamedTypeSymbol { IsGenericType: true } genericReturn && 
                                     (genericReturn.Name == "Task" || genericReturn.Name == "ValueTask") &&
                                     genericReturn.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks")
                            {
                                var resultType = genericReturn.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                                if (genericReturn.Name == "Task")
                                    sb.AppendLine($"        return global::System.Threading.Tasks.Task.FromResult<{resultType}>(default!);");
                                else
                                    sb.AppendLine($"        return new global::System.Threading.Tasks.ValueTask<{resultType}>(default!);");
                            }
                        }
                    }
                    else
                    {
                        if (!method.IsOneWay)
                        {
                            sb.AppendLine($"        var responseTask = _factory.WaitForResponseAsync<{method.MessageResultType!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>({paramName}.Id, this{ctArg});");
                            sb.AppendLine($"        global::Comptatata.SpoolBus.SpoolBusInfrastructure.SendAsync({paramName}, SerializeAsync, GetDiscriminator, _factory.Directory{ctArg}).GetAwaiter().GetResult();");
                            sb.AppendLine($"        return responseTask.GetAwaiter().GetResult();");
                        }
                        else
                        {
                            sb.AppendLine($"        global::Comptatata.SpoolBus.SpoolBusInfrastructure.SendAsync({paramName}, SerializeAsync, GetDiscriminator, _factory.Directory{ctArg}).GetAwaiter().GetResult();");
                        }
                    }
                    sb.AppendLine("    }");
                }

                foreach (var member in GetAllInterfaceMembers(type))
                {
                    if (member is IMethodSymbol m && !implementedMethods.Contains(m) && m.MethodKind == MethodKind.Ordinary)
                    {
                        var parameters = string.Join(", ", m.Parameters.Select(p => $"{p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {p.Name}"));
                        sb.AppendLine($"    public {m.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {m.Name}({parameters}) => throw new global::System.NotSupportedException();");
                    }
                    else if (member is IPropertySymbol p)
                    {
                        var accessors = "";
                        if (p.GetMethod != null) accessors += "get => throw new global::System.NotSupportedException(); ";
                        if (p.SetMethod != null) accessors += "set => throw new global::System.NotSupportedException(); ";
                        sb.AppendLine($"    public {p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {p.Name} {{ {accessors} }}");
                    }
                }

                sb.AppendLine("}");
                sb.AppendLine();

                sb.AppendLine("[EditorBrowsable(EditorBrowsableState.Never)]");
                sb.AppendLine($"file static class {type.Name}SpoolBusClientHelper");
                sb.AppendLine("{");
                sb.AppendLine("    public static string GetDiscriminator(global::Comptatata.SpoolDrop.Messages.Message message) => message switch");
                sb.AppendLine("    {");
                foreach (var t in info.MessageTypes.Where(t => !t.IsAbstract).OrderBy(t => t.Name))
                {
                    sb.AppendLine($"        {t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} _ => \"{JsonSerializerContextEmitter.ToKebabCase(t.Name)}\",");
                }
                sb.AppendLine("        _ => \"unknown-message\"");
                sb.AppendLine("    };");
                sb.AppendLine("}");
                sb.AppendLine();
            }
        }

        var newContent = sb.ToString();
        if (!File.Exists(outputPath) || File.ReadAllText(outputPath) != newContent)
        {
            File.WriteAllText(outputPath, newContent);
        }
        
        return symbolNames;
    }

    private class HandlerInfo
    {
        public HandlerInfo(HashSet<ITypeSymbol> messageTypes, List<HandlerMethodInfo> methods)
        {
            MessageTypes = messageTypes;
            Methods = methods;
        }

        public HashSet<ITypeSymbol> MessageTypes { get; }
        public List<HandlerMethodInfo> Methods { get; }
    }

    private static HandlerInfo GetHandlerInfo(INamedTypeSymbol handler, Compilation compilation)
    {
        var contractTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var allTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var syntaxRef in handler.DeclaringSyntaxReferences)
        {
            var model = compilation.GetSemanticModel(syntaxRef.SyntaxTree);
            var root = syntaxRef.GetSyntax();

            var nodes = root.DescendantNodesAndSelf();
            foreach (var node in nodes)
            {
                ITypeSymbol? symbol = null;
                if (node is TypeSyntax typeSyntax)
                {
                    symbol = model.GetSymbolInfo(typeSyntax).Symbol as ITypeSymbol;
                }
                else if (node is ObjectCreationExpressionSyntax creation)
                {
                    symbol = model.GetSymbolInfo(creation).Symbol?.ContainingType as ITypeSymbol ?? model.GetTypeInfo(creation).Type;
                }
                else if (node is VariableDeclaratorSyntax declarator)
                {
                    var variableSymbol = model.GetDeclaredSymbol(declarator) as ILocalSymbol;
                    symbol = variableSymbol?.Type;
                }

                if (symbol != null && IsOrInheritsFromMessage(symbol))
                {
                    AddContractTypes(contractTypes, symbol);
                    AddWithHierarchy(allTypes, symbol);
                }
            }
        }

        var methods = handler.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary && m.DeclaredAccessibility == Accessibility.Public && !m.IsStatic)
            .ToList();

        var handlerMethods = new List<HandlerMethodInfo>();

        foreach (var method in methods)
        {
            if (method.Parameters.Length < 1 || method.Parameters.Length > 2) continue;

            var paramType = method.Parameters[0].Type;
            if (!IsOrInheritsFromMessage(paramType)) continue;

            bool hasCancellationToken = false;
            if (method.Parameters.Length == 2)
            {
                var secondParamType = method.Parameters[1].Type;
                if (secondParamType.Name == "CancellationToken" && secondParamType.ContainingNamespace?.ToDisplayString() == "System.Threading")
                {
                    hasCancellationToken = true;
                }
                else
                {
                    continue;
                }
            }

            ITypeSymbol? messageResultType = null;
            bool isAsync = false;
            bool isOneWay = false;

            var returnType = method.ReturnType;
            if (returnType.SpecialType == SpecialType.System_Void)
            {
                isOneWay = true;
                isAsync = false;
            }
            else if (returnType is INamedTypeSymbol namedReturnType)
            {
                var returnNamespace = namedReturnType.ContainingNamespace?.ToDisplayString();
                var returnName = namedReturnType.Name;

                if (returnNamespace == "System.Threading.Tasks" && (returnName == "Task" || returnName == "ValueTask"))
                {
                    if (namedReturnType.IsGenericType)
                    {
                        messageResultType = namedReturnType.TypeArguments[0];
                        isAsync = true;
                        if (!IsOrInheritsFromMessage(messageResultType))
                        {
                            messageResultType = null;
                            isOneWay = true;
                        }
                    }
                    else
                    {
                        isOneWay = true;
                        isAsync = true;
                    }
                }
                else if (IsOrInheritsFromMessage(returnType))
                {
                    messageResultType = returnType;
                    isAsync = false;
                }
                else
                {
                    isOneWay = true;
                    isAsync = false;
                }
            }

            if (isOneWay || messageResultType != null)
            {
                handlerMethods.Add(new HandlerMethodInfo(method, paramType, messageResultType, isAsync, isOneWay, hasCancellationToken));
                if (messageResultType != null)
                {
                    AddContractTypes(contractTypes, messageResultType);
                    AddWithHierarchy(allTypes, messageResultType);
                }
                AddContractTypes(contractTypes, paramType);
                AddWithHierarchy(allTypes, paramType);
            }
        }

        var identifiedTypes = contractTypes.ToList();
        foreach (var type in identifiedTypes)
        {
            if (!type.IsSealed || type.TypeKind == TypeKind.Interface)
            {
                JsonSerializerContextEmitter.AddConcreteDescendants(allTypes, type, compilation.GlobalNamespace, AddWithHierarchy);
            }
        }

        if (allTypes.Count > 0)
        {
            var messageBase = compilation.GetTypeByMetadataName("Comptatata.SpoolDrop.Messages.Message");
            var eventBase = compilation.GetTypeByMetadataName("Comptatata.SpoolDrop.Messages.Event");
            if (messageBase != null) allTypes.Add(messageBase);
            if (eventBase != null) allTypes.Add(eventBase);
        }

        return new HandlerInfo(allTypes, handlerMethods);
    }

    private static bool IsOrInheritsFromMessage(ITypeSymbol type)
    {
        var current = type;
        while (current != null)
        {
            if (current.Name == "Message" && current.ContainingNamespace?.ToDisplayString() == "Comptatata.SpoolDrop.Messages") return true;
            current = current.BaseType;
        }
        return false;
    }

    private static void AddContractTypes(HashSet<ITypeSymbol> types, ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array)
        {
            AddContractTypes(types, array.ElementType);
            return;
        }

        if (type is INamedTypeSymbol named && named.IsGenericType)
        {
            foreach (var arg in named.TypeArguments) AddContractTypes(types, arg);
            // Don't return, we also want the generic type itself if it's a message
        }

        types.Add(type);
    }

    private static void AddWithHierarchy(HashSet<ITypeSymbol> types, ITypeSymbol type)
    {
        var current = type;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            types.Add(current);
            current = current.BaseType;
        }
    }

    private static IEnumerable<ISymbol> GetAllInterfaceMembers(ITypeSymbol type)
    {
        foreach (var member in type.GetMembers()) yield return member;
        foreach (var iface in type.AllInterfaces)
        {
            foreach (var member in iface.GetMembers()) yield return member;
        }
    }
}
