using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Comptatata.CodeAnalysis.SpoolDrop;

[Generator(LanguageNames.CSharp)]
public class SpoolDropHandlerGenerator : IIncrementalGenerator
{
    private enum RegistrationType { Handler, Client }
    private class Registration
    {
        public ITypeSymbol Type { get; }
        public RegistrationType Kind { get; }
        public Registration(ITypeSymbol type, RegistrationType kind)
        {
            Type = type;
            Kind = kind;
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
                transform: static (ctx, _) => GetHandlerType(ctx))
            .Where(static t => t is not null)
            .Select(static (t, _) => new Registration(t!, RegistrationType.Handler));

        var clientTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsCreateClientInvocation(s),
                transform: static (ctx, _) => GetClientType(ctx))
            .Where(static t => t is not null)
            .Select(static (t, _) => new Registration(t!, RegistrationType.Client));

        var allRegistrations = handlerTypes.Collect().Combine(clientTypes.Collect());

        context.RegisterSourceOutput(allRegistrations.Combine(context.CompilationProvider),
            static (spc, source) => Execute(source.Left.Left.AddRange(source.Left.Right), source.Right, spc));
    }

    private static bool IsAddHandlerInvocation(SyntaxNode node)
    {
        return node is InvocationExpressionSyntax invocation &&
               (invocation.Expression is MemberAccessExpressionSyntax memberAccess && memberAccess.Name.Identifier.Text == "AddHandler" ||
                invocation.Expression is IdentifierNameSyntax identifier && identifier.Identifier.Text == "AddHandler");
    }

    private static ITypeSymbol? GetHandlerType(GeneratorSyntaxContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var symbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;

        if (symbol is null || symbol.Name != "AddHandler") return null;

        var containingType = symbol.ContainingType?.ToDisplayString();
        if (containingType != "Comptatata.MessageDrop.SpoolDropServer") return null;

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
        var invocation = (InvocationExpressionSyntax)context.Node;
        var symbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;

        if (symbol is null || symbol.Name != "CreateClient") return null;

        var containingType = symbol.ContainingType?.ToDisplayString();
        if (containingType != "Comptatata.MessageDrop.SpoolBusClientFactory") return null;

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
        var distinctRegistrations = registrations.Distinct(RegistrationComparer.Instance);

        var groupedByFile = distinctRegistrations.GroupBy(r => r.Type.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree.FilePath)
            .Where(g => !string.IsNullOrEmpty(g.Key));

        foreach (var group in groupedByFile)
        {
            GenerateFileForGroup(group.Key!, group.ToList(), compilation, context);
        }
    }

    private static void GenerateFileForGroup(string sourcePath, List<Registration> registrations, Compilation compilation, SourceProductionContext context)
    {
        var directory = Path.GetDirectoryName(sourcePath);
        if (directory == null) return;

        var sourceFileName = Path.GetFileNameWithoutExtension(sourcePath);
        var outputPath = Path.Combine(directory, $"{sourceFileName}.generated.cs");

        // Cleanup old per-handler files
        foreach (var reg in registrations)
        {
            var oldPath = Path.Combine(directory, $"{sourceFileName}.{reg.Type.Name}.generated.cs");
            if (File.Exists(oldPath))
            {
                try { File.Delete(oldPath); } catch { }
            }
        }

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
        sb.AppendLine("using Comptatata.MessageDrop;");
        sb.AppendLine();

        // Assume same namespace for now as it's the 99% case for file-scoped namespaces
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
            var info = GetHandlerInfo(type, compilation);
            if (info.Methods.Count == 0) continue;

            foreach (var messageType in info.MessageTypes.OrderBy(t => t.ToDisplayString()))
            {
                sb.AppendLine($"[JsonSerializable(typeof({messageType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}))]");
            }
            sb.AppendLine("[EditorBrowsable(EditorBrowsableState.Never)]");

            var contextClassName = reg.Kind == RegistrationType.Handler
                ? $"{type.Name}SpoolBusServerSerializer"
                : $"{type.Name}SpoolBusClientSerializer";

            sb.AppendLine($"public partial class {contextClassName} : JsonSerializerContext");
            sb.AppendLine("{");
            sb.AppendLine("    public static JsonSerializerOptions SpoolBusOptions => field ??= ConstructPolymorphism();");
            sb.AppendLine();
            sb.AppendLine("    private static JsonSerializerOptions ConstructPolymorphism()");
            sb.AppendLine("    {");
            sb.AppendLine("        var options = new JsonSerializerOptions();");
            sb.AppendLine("        options.TypeInfoResolver = JsonTypeInfoResolver.WithAddedModifier(Default, AddPolymorphism);");
            sb.AppendLine("        return options;");
            sb.AppendLine("    }");
            sb.AppendLine();
            sb.AppendLine("    private static void AddPolymorphism(JsonTypeInfo typeInfo)");
            sb.AppendLine("    {");
            sb.AppendLine("        typeInfo.PolymorphismOptions = typeInfo switch");
            sb.AppendLine("        {");

            var abstractTypes = info.MessageTypes.Where(t => t.IsAbstract).ToList();
            foreach (var parent in abstractTypes.OrderBy(t => t.ToDisplayString()))
            {
                var concreteDescendants = info.MessageTypes
                    .Where(t => !t.IsAbstract && IsDescendantOf(t, parent))
                    .OrderBy(t => t.Name)
                    .ToList();

                if (concreteDescendants.Count > 0)
                {
                    sb.Append($"            JsonTypeInfo<{parent.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}> => SpoolBusPolymorphism.Options");
                    foreach (var descendant in concreteDescendants)
                    {
                        var discriminator = ToKebabCase(descendant.Name);
                        sb.AppendLine();
                        sb.Append($"                .AddDerived<{descendant.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>(\"{discriminator}\")");
                    }
                    sb.AppendLine(",");
                }
            }

            sb.AppendLine("            _ => typeInfo.PolymorphismOptions");
            sb.AppendLine("        };");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            sb.AppendLine();

            if (reg.Kind == RegistrationType.Handler)
            {
                // Generate Dispatcher
                sb.AppendLine("[EditorBrowsable(EditorBrowsableState.Never)]");
                sb.AppendLine($"public static class {type.Name}SpoolBusDispatcher");
                sb.AppendLine("{");
                sb.AppendLine($"    public static async global::System.Threading.Tasks.ValueTask<global::System.Collections.Generic.IReadOnlyList<global::Comptatata.MessageDrop.Messages.Message?>> DispatchAsync(");
                sb.AppendLine($"        global::System.Func<{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}> handlerFactory,");
                sb.AppendLine($"        global::System.IO.Stream stream,");
                sb.AppendLine($"        global::System.Threading.CancellationToken ct)");
                sb.AppendLine("    {");
                sb.AppendLine("        #pragma warning disable CS1998");
                sb.AppendLine($"        var request = await global::System.Text.Json.JsonSerializer.DeserializeAsync<global::Comptatata.MessageDrop.Messages.Message>(stream, {type.Name}SpoolBusServerSerializer.SpoolBusOptions, ct).ConfigureAwait(false);");
                sb.AppendLine($"        {type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}? handler = null;");
                sb.AppendLine($"        var responses = new global::System.Collections.Generic.List<global::Comptatata.MessageDrop.Messages.Message?>();");
                sb.AppendLine();

                foreach (var method in info.Methods)
                {
                    var awaitPrefix = method.IsAsync ? "await " : "";
                    var ctArg = method.HasCancellationToken ? ", ct" : "";
                    var awaitExpr = method.IsAsync ? ".ConfigureAwait(false)" : "";

                    sb.AppendLine($"        async global::System.Threading.Tasks.ValueTask<global::Comptatata.MessageDrop.Messages.Message?> Invoke_{method.Method.Name}({method.ParameterType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} m)");
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
                    sb.AppendLine("            if (response is global::Comptatata.MessageDrop.Messages.Event e)");
                    sb.AppendLine("            {");
                    sb.AppendLine("                responses.Add(e with { ReplyTo = request?.Id });");
                    sb.AppendLine("            }");
                    sb.AppendLine("            else");
                    sb.AppendLine("            {");
                    sb.AppendLine("                responses.Add(response);");
                    sb.AppendLine("            }");
                    sb.AppendLine("        }");
                }

                sb.AppendLine("        return responses;");
                sb.AppendLine("        #pragma warning restore CS1998");
                sb.AppendLine("    }");
                sb.AppendLine();
                sb.AppendLine("    public static string GetDiscriminator(global::Comptatata.MessageDrop.Messages.Message message) => message switch");
                sb.AppendLine("    {");
                foreach (var t in info.MessageTypes.Where(t => !t.IsAbstract).OrderBy(t => t.Name))
                {
                    sb.AppendLine($"        {t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} _ => \"{ToKebabCase(t.Name)}\",");
                }
                sb.AppendLine("        _ => \"unknown-message\"");
                sb.AppendLine("    };");
                sb.AppendLine("}");
                sb.AppendLine();

                // Generate Infrastructure Initializer
                sb.AppendLine("[EditorBrowsable(EditorBrowsableState.Never)]");
                sb.AppendLine($"internal static class {type.Name}SpoolBusInfrastructureInitializer");
                sb.AppendLine("{");
                sb.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializer]");
                sb.AppendLine("    public static void Initialize()");
                sb.AppendLine("    {");
                sb.AppendLine($"        global::Comptatata.MessageDrop.SpoolBusInfrastructure<{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>.Options = {type.Name}SpoolBusServerSerializer.SpoolBusOptions;");
                sb.AppendLine($"        global::Comptatata.MessageDrop.SpoolBusInfrastructure<{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>.Dispatcher = {type.Name}SpoolBusDispatcher.DispatchAsync;");
                sb.AppendLine($"        global::Comptatata.MessageDrop.SpoolBusInfrastructure<{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>.GetDiscriminator = {type.Name}SpoolBusDispatcher.GetDiscriminator;");
                sb.AppendLine("    }");
                sb.AppendLine("}");
                sb.AppendLine();
            }
            else if (reg.Kind == RegistrationType.Client)
            {
                // Generate Client Implementation
                sb.AppendLine("[EditorBrowsable(EditorBrowsableState.Never)]");
                sb.AppendLine($"public class {type.Name}SpoolBusClient : {type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}");
                sb.AppendLine("{");
                sb.AppendLine($"    private readonly global::Comptatata.MessageDrop.SpoolBusClientFactory _factory;");
                sb.AppendLine($"    public {type.Name}SpoolBusClient(global::Comptatata.MessageDrop.SpoolBusClientFactory factory) => _factory = factory;");
                sb.AppendLine();

                foreach (var method in info.Methods)
                {
                    var paramName = method.Method.Parameters[0].Name;
                    var ctParam = method.HasCancellationToken ? ", global::System.Threading.CancellationToken ct" : "";
                    var ctArg = method.HasCancellationToken ? ", ct" : "";
                    var isAsync = method.IsAsync || !method.IsOneWay;
                    var asyncKeyword = isAsync ? "async " : "";

                    sb.AppendLine($"    public {asyncKeyword}{method.Method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {method.Method.Name}({method.ParameterType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} {paramName}{ctParam})");
                    sb.AppendLine("    {");

                    if (isAsync)
                    {
                        sb.AppendLine($"        await global::Comptatata.MessageDrop.SpoolBusInfrastructure<{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>.SendAsync({paramName}, _factory.Directory{ctArg}).ConfigureAwait(false);");
                        if (!method.IsOneWay)
                        {
                            sb.AppendLine($"        return await _factory.WaitForResponseAsync<{method.MessageResultType!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>({paramName}.Id, {type.Name}SpoolBusClientSerializer.SpoolBusOptions{ctArg}).ConfigureAwait(false);");
                        }
                    }
                    else
                    {
                        sb.AppendLine($"        global::Comptatata.MessageDrop.SpoolBusInfrastructure<{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>.SendAsync({paramName}, _factory.Directory{ctArg}).GetAwaiter().GetResult();");
                        if (!method.IsOneWay)
                        {
                            sb.AppendLine($"        return _factory.WaitForResponseAsync<{method.MessageResultType!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>({paramName}.Id, {type.Name}SpoolBusClientSerializer.SpoolBusOptions{ctArg}).GetAwaiter().GetResult();");
                        }
                    }
                    sb.AppendLine("    }");
                }
                sb.AppendLine("}");
                sb.AppendLine();

                // Generate Discriminator helper for Client
                sb.AppendLine("[EditorBrowsable(EditorBrowsableState.Never)]");
                sb.AppendLine($"public static class {type.Name}SpoolBusClientHelper");
                sb.AppendLine("{");
                sb.AppendLine("    public static string GetDiscriminator(global::Comptatata.MessageDrop.Messages.Message message) => message switch");
                sb.AppendLine("    {");
                foreach (var t in info.MessageTypes.Where(t => !t.IsAbstract).OrderBy(t => t.Name))
                {
                    sb.AppendLine($"        {t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} _ => \"{ToKebabCase(t.Name)}\",");
                }
                sb.AppendLine("        _ => \"unknown-message\"");
                sb.AppendLine("    };");
                sb.AppendLine("}");
                sb.AppendLine();

                // Generate Infrastructure Initializer for Client
                sb.AppendLine("[EditorBrowsable(EditorBrowsableState.Never)]");
                sb.AppendLine($"internal static class {type.Name}SpoolBusInfrastructureInitializer");
                sb.AppendLine("{");
                sb.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializer]");
                sb.AppendLine("    public static void Initialize()");
                sb.AppendLine("    {");
                sb.AppendLine($"        global::Comptatata.MessageDrop.SpoolBusInfrastructure<{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>.Options = {type.Name}SpoolBusClientSerializer.SpoolBusOptions;");
                sb.AppendLine($"        global::Comptatata.MessageDrop.SpoolBusInfrastructure<{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>.GetDiscriminator = {type.Name}SpoolBusClientHelper.GetDiscriminator;");
                sb.AppendLine($"        global::Comptatata.MessageDrop.SpoolBusInfrastructure<{type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>.ClientFactory = factory => new {type.Name}SpoolBusClient(factory);");
                sb.AppendLine("    }");
                sb.AppendLine("}");
                sb.AppendLine();
            }
        }

        var newContent = sb.ToString();
        if (!File.Exists(outputPath) || File.ReadAllText(outputPath) != newContent)
        {
            File.WriteAllText(outputPath, newContent);
        }
    }

    private static bool IsDescendantOf(ITypeSymbol type, ITypeSymbol potentialBase)
    {
        var targetBase = potentialBase.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var current = type.BaseType;
        while (current != null)
        {
            if (current.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == targetBase) return true;
            current = current.BaseType;
        }
        return false;
    }

    private static string ToKebabCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var sb = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0) sb.Append('-');
                sb.Append(char.ToLower(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
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
        var messageTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        // Find all type references in the handler's syntax
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
                    // Handle var x = new MyEvent();
                    var variableSymbol = model.GetDeclaredSymbol(declarator) as ILocalSymbol;
                    symbol = variableSymbol?.Type;
                }

                if (symbol != null && IsOrInheritsFromMessage(symbol))
                {
                    AddWithHierarchy(messageTypes, symbol);
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
                    AddWithHierarchy(messageTypes, messageResultType);
                }
                AddWithHierarchy(messageTypes, paramType);
            }
        }

        // Search for concrete descendants in the current assembly for any identified message types
        var identifiedTypes = messageTypes.ToList();
        foreach (var type in identifiedTypes)
        {
            if (type.IsAbstract || type.TypeKind == TypeKind.Interface)
            {
                AddConcreteDescendants(messageTypes, type, compilation.Assembly.GlobalNamespace);
            }
        }

        return new HandlerInfo(messageTypes, handlerMethods);
    }

    private static void AddConcreteDescendants(HashSet<ITypeSymbol> types, ITypeSymbol baseType, INamespaceSymbol ns)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            if (!type.IsAbstract && IsDescendantOf(type, baseType))
            {
                AddWithHierarchy(types, type);
            }
            // Also check nested types
            foreach (var nested in type.GetTypeMembers())
            {
                if (!nested.IsAbstract && IsDescendantOf(nested, baseType))
                {
                    AddWithHierarchy(types, nested);
                }
            }
        }

        foreach (var childNs in ns.GetNamespaceMembers())
        {
            AddConcreteDescendants(types, baseType, childNs);
        }
    }

    private static bool IsOrInheritsFromMessage(ITypeSymbol type)
    {
        var current = type;
        while (current != null)
        {
            if (current.Name == "Message" && current.ContainingNamespace?.ToDisplayString() == "Comptatata.MessageDrop.Messages") return true;
            current = current.BaseType;
        }
        return false;
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
}