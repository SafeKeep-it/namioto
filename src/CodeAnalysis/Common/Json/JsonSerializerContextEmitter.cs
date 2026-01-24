using System.Text;
using Microsoft.CodeAnalysis;

namespace Comptatata.CodeAnalysis.Common;

public static class JsonSerializerContextEmitter
{
    public static readonly DiagnosticDescriptor InvalidEnvelopeDescriptor = new(
        "COMP0002",
        "Invalid Envelope",
        "The type {0} is marked as an [Envelope] but must have exactly one generic type parameter",
        "Usage",
        DiagnosticSeverity.Error,
        true);

    public static string ToKebabCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var sb = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
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

    private static string GetToken(string discriminator)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in Encoding.UTF8.GetBytes(discriminator))
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
                crc = (crc >> 1) ^ (0xEDB88320 & (uint)-(int)(crc & 1));
        }

        crc = ~crc;

        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var char1 = alphabet[(int)((crc >> 27) & 0x1F)];
        var char2 = alphabet[(int)((crc >> 22) & 0x1F)];
        return $"{char1}{char2}";
    }

    private static string GetTokenChain(ITypeSymbol type, SerializationGraph graph, ITypeSymbol root)
    {
        var chain = new StringBuilder();
        var current = type;
        while (current != null)
        {
            chain.Append(GetToken(ToKebabCase(current.Name)));
            if (SymbolEqualityComparer.Default.Equals(current, root)) break;

            // Follow the hierarchy defined in the graph
            ITypeSymbol? parent = null;
            foreach (var family in graph.Families.Values)
            {
                foreach (var entry in family)
                {
                    if (entry.Value.Contains(current))
                    {
                        parent = entry.Key;
                        break;
                    }
                }
                if (parent != null) break;
            }

            current = parent;
        }

        return chain.ToString();
    }

    public static bool IsDescendantOf(ITypeSymbol type, ITypeSymbol potentialBase)
    {
        if (SymbolEqualityComparer.Default.Equals(type, potentialBase)) return false;

        var targetBase = potentialBase.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // Check base classes
        var current = type.BaseType;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, potentialBase)) return true;
            if (current.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == targetBase) return true;
            current = current.BaseType;
        }

        // Check interfaces
        foreach (var iface in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface, potentialBase)) return true;
            if (iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == targetBase) return true;
        }

        return false;
    }

    public static ITypeSymbol? GetNearestPolymorphicRoot(ITypeSymbol type)
    {
        var current = type;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            if (current.GetAttributes()
                .Any(a => a.AttributeClass?.Name is "JsonPolymorphicAttribute" or "JsonPolymorphic")) return current;
            current = current.BaseType;
        }

        foreach (var iface in type.AllInterfaces)
            if (iface.GetAttributes()
                .Any(a => a.AttributeClass?.Name is "JsonPolymorphicAttribute" or "JsonPolymorphic"))
                return iface;

        return null;
    }

    public static void DiscoverHierarchy(SerializationGraph graph, ITypeSymbol candidate,
        IEnumerable<ITypeSymbol> allTypesInCompilation, Func<ITypeSymbol, bool>? filter = null)
    {
        var root = GetNearestPolymorphicRoot(candidate);
        if (root == null)
        {
            if (filter == null || filter(candidate))
                graph.AddNonPolymorphic(candidate);
            return;
        }

        // 1. Upward walk: Strictly register parents in the direct line from starting type to root
        var current = candidate;
        while (current != null && !SymbolEqualityComparer.Default.Equals(current, root))
        {
            ITypeSymbol? parent = current.BaseType;
            if (parent == null || parent.SpecialType == SpecialType.System_Object)
            {
                // Check if root is an interface implemented by current
                if (current.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, root)))
                    parent = root;
                else break;
            }

            if (filter == null || filter(current))
                graph.AddPolymorphic(root, parent, current);

            current = parent;
        }

        // Ensure root is initialized in its family
        if (filter == null || filter(root))
            if (!graph.Families.ContainsKey(root))
                graph.Families[root] =
                    new Dictionary<ITypeSymbol, HashSet<ITypeSymbol>>(SymbolEqualityComparer.Default);

        // 2. Downward fan-out: Only descendants of the starting type are registered and explored
        if (!candidate.IsSealed || candidate.TypeKind == TypeKind.Interface)
            FanOutChildren(graph, root, candidate, allTypesInCompilation, filter);
    }

    static void FanOutChildren(SerializationGraph graph, ITypeSymbol root, ITypeSymbol parent,
        IEnumerable<ITypeSymbol> allTypesInCompilation, Func<ITypeSymbol, bool>? filter)
    {
        // Find immediate children of 'parent' in the compilation
        var immediateChildren = allTypesInCompilation
            .Where(t => !SymbolEqualityComparer.Default.Equals(t, parent))
            .Where(t => SymbolEqualityComparer.Default.Equals(t.BaseType, parent) ||
                        (parent.TypeKind == TypeKind.Interface &&
                         t.Interfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, parent))))
            .ToList();

        foreach (var child in immediateChildren)
        {
            if (filter != null && !filter(child)) continue;

            // Stop at first root downward too (Rule 3)
            var childRoot = GetNearestPolymorphicRoot(child);
            if (childRoot != null && !SymbolEqualityComparer.Default.Equals(childRoot, root))
            {
                // This child starts its own family, so it's a leaf in our current family walk
                graph.AddPolymorphic(root, parent, child);
                continue;
            }

            graph.AddPolymorphic(root, parent, child);

            // Recursively fan out unless it's sealed
            if (!child.IsSealed || child.TypeKind == TypeKind.Interface)
                FanOutChildren(graph, root, child, allTypesInCompilation, filter);
        }
    }

    public static List<ITypeSymbol> GetAllTypesInCompilation(Compilation compilation)
    {
        var types = new List<ITypeSymbol>();
        CollectTypes(compilation.GlobalNamespace, types);
        return types;
    }

    static void CollectTypes(INamespaceOrTypeSymbol symbol, List<ITypeSymbol> types)
    {
        if (symbol is INamespaceSymbol ns)
        {
            var nsName = ns.Name;
            if (nsName is "System" or "Microsoft" or "JetBrains" or "OpenAI" or "Azure" or "Google")
                // Only skip if it's a top-level namespace of these names, or if we want to be more specific:
                if (ns.ContainingNamespace == null || ns.ContainingNamespace.IsGlobalNamespace)
                    // Exceptions for things we might care about
                    if (nsName != "System")
                        return;

            // Further pruning for System sub-namespaces
            var fullNs = ns.ToDisplayString();
            if (fullNs.StartsWith("System."))
                if (fullNs is "System.Text.Json" or "System.Threading.Tasks" or "System.Security" or "System.Reflection"
                    or "System.Runtime" or "System.Net.Http" or "System.Collections")
                    return;

            foreach (var member in ns.GetMembers())
                if (member is INamespaceOrTypeSymbol nested)
                    CollectTypes(nested, types);
        }
        else if (symbol is INamedTypeSymbol type)
        {
            if (IsRelevantType(type))
            {
                types.Add(type);
                foreach (var nested in type.GetTypeMembers())
                    CollectTypes(nested, types);
            }
        }
    }

    public static void AddSerializableTypes(
        SerializationGraph graph,
        ITypeSymbol? type,
        Compilation compilation,
        IEnumerable<ITypeSymbol> allTypesInCompilation,
        Action<Diagnostic>? reportError = null,
        Func<ITypeSymbol, bool>? filter = null)
    {
        if (type == null || type.SpecialType == SpecialType.System_Object) return;

        type = UnwrapEnvelope(type, reportError);
        if (type == null) return;

        if (type is IArrayTypeSymbol array)
        {
            AddSerializableTypes(graph, array.ElementType, compilation, allTypesInCompilation, reportError, filter);
            return;
        }

        if (type is INamedTypeSymbol named && named.IsGenericType)
            foreach (var arg in named.TypeArguments)
                AddSerializableTypes(graph, arg, compilation, allTypesInCompilation, reportError, filter);

        if (filter == null || filter(type))
            if (IsRelevantType(type))
                DiscoverHierarchy(graph, type, allTypesInCompilation, filter);
    }

    public static bool IsEnvelope(ITypeSymbol type)
    {
        return type is INamedTypeSymbol named &&
               named.GetAttributes().Any(a => a.AttributeClass?.Name == "Envelope");
    }

    public static bool IsTask(ITypeSymbol type)
    {
        return type is INamedTypeSymbol named &&
               (named.Name == "Task" || named.Name == "ValueTask") &&
               named.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks";
    }

    public static ITypeSymbol? UnwrapTask(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol named && IsTask(named)) return named.IsGenericType ? named.TypeArguments[0] : null;

        return type;
    }

    public static ITypeSymbol? UnwrapEnvelope(ITypeSymbol? type, Action<Diagnostic>? reportError = null)
    {
        if (type == null) return null;
        var current = type;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            if (current is INamedTypeSymbol named && (IsEnvelope(named) || IsTask(named)))
            {
                if (IsEnvelope(named) && named.TypeParameters.Length != 1)
                    reportError?.Invoke(Diagnostic.Create(InvalidEnvelopeDescriptor, named.Locations.FirstOrDefault(),
                        named.ToDisplayString()));

                if (named.TypeArguments.Length == 1)
                    // Found an envelope. Extract payload and restart whole unwrapping from there.
                    return UnwrapEnvelope(named.TypeArguments[0], reportError);

                if (IsTask(named)) return null;
            }

            current = current.BaseType;
        }

        return type;
    }

    public static bool IsRelevantType(ITypeSymbol type)
    {
        if (type == null || type.TypeKind == TypeKind.Error || type.SpecialType == SpecialType.System_Void)
            return false;
        if (type.SpecialType == SpecialType.System_Object) return false;

        // Exclude primitives and string
        if (type.SpecialType is >= SpecialType.System_Boolean and <= SpecialType.System_String) return false;
        if (type.SpecialType is SpecialType.System_DateTime) return false;

        // Exclude delegates
        if (type.TypeKind == TypeKind.Delegate) return false;
        if (type.BaseType?.Name == "MulticastDelegate" || type.BaseType?.Name == "Delegate") return false;

        // Exclude compiler-generated types and open generics
        if (type.Name.Contains("<") || type.Name.Contains("__")) return false;
        if (type is INamedTypeSymbol named && named.IsGenericType &&
            named.TypeArguments.Any(t => t.TypeKind == TypeKind.TypeParameter)) return false;

        // Don't register serializer contexts themselves
        var current = type;
        while (current != null)
        {
            if (current.Name == "JsonSerializerContext" &&
                current.ContainingNamespace?.ToDisplayString() == "System.Text.Json.Serialization")
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
        if (ns.StartsWith("OpenAI")) return false;
        if (ns.StartsWith("System.ClientModel")) return false;
        if (ns.StartsWith("System.Collections")) return false;

        if (ns == "System")
        {
            if (type.Name is "IServiceProvider" or "CancellationToken" or "ValueType" or "Enum" or "Guid"
                or "DateTimeOffset" or "TimeSpan" or "Uri" or "Version" or "Type" or "RuntimeTypeHandle" or "Delegate"
                or "MulticastDelegate") return false;
            if (type.Name is "Func" or "Action" || type.Name.StartsWith("Func`") ||
                type.Name.StartsWith("Action`")) return false;
        }

        return true;
    }

    public static void EmitGeneratedClass(StringBuilder sb, IEnumerable<ITypeSymbol> allTypes,
        string optionsPropertyName, string indent = "    ")
    {
        sb.AppendLine(
            $"{indent}[global::System.CodeDom.Compiler.GeneratedCode(\"Comptatata.CodeAnalysis\", \"1.0.0\")]");
        sb.AppendLine($"{indent}public static class Generated");
        sb.AppendLine($"{indent}{{");
        foreach (var type in allTypes.OrderBy(t => t.ToDisplayString()))
        {
            var propertyName = GetPropertyName(type);
            var typeFullName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            sb.AppendLine(
                $"{indent}    public static JsonTypeInfo<{typeFullName}> {propertyName} => (JsonTypeInfo<{typeFullName}>){optionsPropertyName}.GetTypeInfo(typeof({typeFullName}))!;");
        }

        sb.AppendLine($"{indent}    public static void Initialize(JsonSerializerOptions options) {{ }}");
        sb.AppendLine($"{indent}}}");
    }

    public static string GetDiscriminatorMethodName(ITypeSymbol? root)
    {
        if (root == null) return "GetDiscriminator";
        var parts = root.ToDisplayString().Split('.');
        var name = string.Join("_", parts.Select(p => p.Replace("global::", "")));
        return $"GetDiscriminator_{name}";
    }

    public static string GetTokenChainMethodName(ITypeSymbol? root)
    {
        if (root == null) return "GetTokenChain";
        var parts = root.ToDisplayString().Split('.');
        var name = string.Join("_", parts.Select(p => p.Replace("global::", "")));
        return $"GetTokenChain_{name}";
    }

    public static void EmitDiscriminatorMethods(StringBuilder sb, SerializationGraph graph,
        string indent = "    ")
    {
        foreach (var family in graph.Families.OrderBy(f => f.Key.ToDisplayString()))
        {
            var root = family.Key;
            var hierarchy = family.Value;
            var methodName = GetDiscriminatorMethodName(root);
            var rootType = root.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            sb.AppendLine($"{indent}public static string {methodName}({rootType} value) => value switch");
            sb.AppendLine($"{indent}{{");

            var allFamilyTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var parent in hierarchy.Keys) allFamilyTypes.Add(parent);
            foreach (var children in hierarchy.Values)
            foreach (var child in children)
                allFamilyTypes.Add(child);

            foreach (var t in allFamilyTypes.Where(t => !t.IsAbstract && t.TypeKind != TypeKind.Interface)
                         .OrderBy(t => t.Name))
                sb.AppendLine(
                    $"{indent}    {t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} _ => \"{ToKebabCase(t.Name)}\",");
            sb.AppendLine($"{indent}    _ => \"unknown\"");
            sb.AppendLine($"{indent}}};");
        }

        if (graph.Families.Count == 0)
            sb.AppendLine($"{indent}public static string GetDiscriminator(object value) => \"unknown\";");
    }

    public static void EmitTokenChainMethods(StringBuilder sb, SerializationGraph graph,
        string indent = "    ")
    {
        foreach (var family in graph.Families.OrderBy(f => f.Key.ToDisplayString()))
        {
            var root = family.Key;
            var hierarchy = family.Value;
            var methodName = GetTokenChainMethodName(root);
            var rootType = root.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            sb.AppendLine($"{indent}public static string {methodName}({rootType} value) => value switch");
            sb.AppendLine($"{indent}{{");

            var allFamilyTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var parent in hierarchy.Keys) allFamilyTypes.Add(parent);
            foreach (var children in hierarchy.Values)
            foreach (var child in children)
                allFamilyTypes.Add(child);

            foreach (var t in allFamilyTypes.Where(t => !t.IsAbstract && t.TypeKind != TypeKind.Interface)
                         .OrderBy(t => t.Name))
                sb.AppendLine(
                    $"{indent}    {t.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} _ => \"{GetTokenChain(t, graph, root)}\",");
            sb.AppendLine($"{indent}    _ => \"\"");
            sb.AppendLine($"{indent}}};");
        }

        if (graph.Families.Count == 0)
            sb.AppendLine($"{indent}public static string GetTokenChain(object value) => \"\";");
    }

    public static void EmitJsonSerializerOptions(StringBuilder sb, string propertyName, string methodName,
        string indent = "    ", string className = "")
    {
        sb.AppendLine($"{indent}public static JsonSerializerOptions {propertyName} => field ??= {methodName}();");
        sb.AppendLine();
        sb.AppendLine($"{indent}private static JsonSerializerOptions {methodName}()");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    var options = new JsonSerializerOptions");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine($"{indent}        WriteIndented = false,");
        sb.AppendLine($"{indent}        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,");
        sb.AppendLine($"{indent}        RespectNullableAnnotations = true,");
        sb.AppendLine($"{indent}        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,");
        sb.AppendLine($"{indent}    }};");
        // Use fully qualified access to Default to allow resolution at runtime when STJ has generated it
        if (!string.IsNullOrEmpty(className))
            sb.AppendLine(
                $"{indent}    options.TypeInfoResolverChain.Add({className}.Default.WithAddedModifier(AddPolymorphism));");
        else
            sb.AppendLine(
                $"{indent}    options.TypeInfoResolverChain.Add(Default.WithAddedModifier(AddPolymorphism));");
        sb.AppendLine($"{indent}    Generated.Initialize(options);");
        sb.AppendLine($"{indent}    return options;");
        sb.AppendLine($"{indent}}}");
    }

    public static void EmitAddPolymorphism(StringBuilder sb, SerializationGraph graph, string indent = "    ")
    {
        sb.AppendLine($"{indent}private static void AddPolymorphism(JsonTypeInfo typeInfo)");
        sb.AppendLine($"{indent}{{");

        foreach (var familyEntry in graph.Families.OrderBy(f => f.Key.ToDisplayString()))
        {
            var root = familyEntry.Key;
            var hierarchy = familyEntry.Value;

            var polyAttr = root.GetAttributes().FirstOrDefault(a =>
                a.AttributeClass?.Name is "JsonPolymorphicAttribute" or "JsonPolymorphic");
            var discriminatorName = "type";
            if (polyAttr != null)
            {
                var namedArg = polyAttr.NamedArguments.FirstOrDefault(kv => kv.Key == "TypeDiscriminatorPropertyName");
                if (namedArg.Value.Value is string s) discriminatorName = s;
            }

            foreach (var parentEntry in hierarchy.OrderBy(p => p.Key.ToDisplayString()))
            {
                var parent = parentEntry.Key;

                // For this parent, we need ALL concrete descendants in this family
                var concreteDescendants = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
                CollectConcreteDescendants(hierarchy, parent, concreteDescendants);

                if (concreteDescendants.Count > 0)
                {
                    sb.AppendLine(
                        $"{indent}    if (typeInfo.Type == typeof({parent.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}))");
                    sb.AppendLine($"{indent}    {{");
                    sb.AppendLine(
                        $"{indent}        var options = typeInfo.PolymorphismOptions ??= new JsonPolymorphismOptions();");
                    sb.AppendLine($"{indent}        options.TypeDiscriminatorPropertyName = \"{discriminatorName}\";");
                    foreach (var descendant in concreteDescendants.OrderBy(t => t.Name))
                        sb.AppendLine(
                            $"{indent}        options.DerivedTypes.Add(new JsonDerivedType(typeof({descendant.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}), \"{ToKebabCase(descendant.Name)}\"));");
                    sb.AppendLine($"{indent}        return;");
                    sb.AppendLine($"{indent}    }}");
                }
            }
        }

        sb.AppendLine($"{indent}}}");
    }

    static void CollectConcreteDescendants(Dictionary<ITypeSymbol, HashSet<ITypeSymbol>> hierarchy, ITypeSymbol parent,
        HashSet<ITypeSymbol> result)
    {
        if (hierarchy.TryGetValue(parent, out var children))
            foreach (var child in children)
            {
                if (!child.IsAbstract && child.TypeKind != TypeKind.Interface)
                    result.Add(child);
                CollectConcreteDescendants(hierarchy, child, result);
            }
    }

    public static void EmitContext(
        StringBuilder sb,
        string className,
        string accessibility,
        SerializationGraph graph,
        string optionsPropertyName = "Options",
        string optionsMethodName = "ConstructOptions",
        Action<StringBuilder>? additionalMembers = null)
    {
        var typeList = graph.GetAllTypes().OrderBy(t => t.ToDisplayString()).ToList();

        foreach (var type in typeList)
            sb.AppendLine(
                $"[JsonSerializable(typeof({type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}))]");

        sb.AppendLine(
            "[global::System.Text.Json.Serialization.JsonSourceGenerationOptions(UseStringEnumConverter = true)]");
        sb.AppendLine(
            $"{accessibility} partial class {className} : global::System.Text.Json.Serialization.JsonSerializerContext");
        sb.AppendLine("{");

        if (additionalMembers != null)
        {
            additionalMembers(sb);
            sb.AppendLine();
        }

        EmitJsonSerializerOptions(sb, optionsPropertyName, optionsMethodName);
        sb.AppendLine();

        EmitDiscriminatorMethods(sb, graph);
        sb.AppendLine();

        EmitTokenChainMethods(sb, graph);
        sb.AppendLine();

        EmitAddPolymorphism(sb, graph);
        sb.AppendLine();

        EmitGeneratedClass(sb, typeList, optionsPropertyName);

        sb.AppendLine("}");
    }

    public static void EmitAttributesOnly(
        StringBuilder sb,
        string className,
        string accessibility,
        SerializationGraph graph)
    {
        var typeList = graph.GetAllTypes().OrderBy(t => t.ToDisplayString()).ToList();

        foreach (var type in typeList)
            sb.AppendLine(
                $"[JsonSerializable(typeof({type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}))]");

        sb.AppendLine(
            "[global::System.Text.Json.Serialization.JsonSourceGenerationOptions(UseStringEnumConverter = true)]");
        sb.AppendLine(
            $"{accessibility} partial class {className} : global::System.Text.Json.Serialization.JsonSerializerContext");
        sb.AppendLine("{");
        sb.AppendLine("}");
    }

    public static void EmitRuntimeMembers(
        StringBuilder sb,
        string className,
        string accessibility,
        SerializationGraph graph,
        string optionsPropertyName = "Options",
        string optionsMethodName = "ConstructOptions",
        Action<StringBuilder>? additionalMembers = null)
    {
        var typeList = graph.GetAllTypes().OrderBy(t => t.ToDisplayString()).ToList();

        sb.AppendLine($"{accessibility} partial class {className}");
        sb.AppendLine("{");

        if (additionalMembers != null)
        {
            additionalMembers(sb);
            sb.AppendLine();
        }

        EmitJsonSerializerOptions(sb, optionsPropertyName, optionsMethodName, "    ", className);
        sb.AppendLine();

        EmitDiscriminatorMethods(sb, graph);
        sb.AppendLine();

        EmitTokenChainMethods(sb, graph);
        sb.AppendLine();

        EmitAddPolymorphism(sb, graph);
        sb.AppendLine();

        EmitGeneratedClass(sb, typeList, optionsPropertyName);

        sb.AppendLine("}");
    }

    public static string GetPropertyName(ITypeSymbol type)
    {
        var unwrapped = UnwrapEnvelope(type);
        if (unwrapped != null) type = unwrapped;

        if (type is IArrayTypeSymbol array) return GetPropertyName(array.ElementType) + "Array";
        if (type is INamedTypeSymbol named && named.IsGenericType)
        {
            var baseName = named.Name.Split('`')[0];
            return baseName + "_" + string.Join("_", named.TypeArguments.Select(GetPropertyName));
        }

        var name = type.Name;
        if (string.IsNullOrEmpty(name)) name = "Unknown";
        return name;
    }

    public class SerializationGraph
    {
        public HashSet<ITypeSymbol> NonPolymorphicTypes { get; } = new(SymbolEqualityComparer.Default);

        // Root -> (Parent -> Children)
        public Dictionary<ITypeSymbol, Dictionary<ITypeSymbol, HashSet<ITypeSymbol>>> Families { get; } =
            new(SymbolEqualityComparer.Default);

        public void AddNonPolymorphic(ITypeSymbol type) => NonPolymorphicTypes.Add(type);

        public void AddPolymorphic(ITypeSymbol root, ITypeSymbol parent, ITypeSymbol child)
        {
            if (!Families.TryGetValue(root, out var hierarchy))
            {
                hierarchy = new Dictionary<ITypeSymbol, HashSet<ITypeSymbol>>(SymbolEqualityComparer.Default);
                Families[root] = hierarchy;
            }

            if (!hierarchy.TryGetValue(parent, out var children))
            {
                children = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
                hierarchy[parent] = children;
            }

            children.Add(child);

            if (!hierarchy.ContainsKey(child))
                hierarchy[child] = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        }

        public HashSet<ITypeSymbol> GetAllTypes()
        {
            var all = new HashSet<ITypeSymbol>(NonPolymorphicTypes, SymbolEqualityComparer.Default);
            foreach (var family in Families.Values)
            {
                foreach (var parent in family.Keys) all.Add(parent);
                foreach (var children in family.Values)
                foreach (var child in children)
                    all.Add(child);
            }

            return all;
        }
    }
}