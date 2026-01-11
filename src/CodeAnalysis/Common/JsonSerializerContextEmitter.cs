using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Comptatata.CodeAnalysis;

public static class JsonSerializerContextEmitter
{
    public static string ToKebabCase(string name)
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

    public static bool IsDescendantOf(ITypeSymbol type, ITypeSymbol potentialBase)
    {
        var targetBase = potentialBase.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        
        // Check base classes
        var current = type.BaseType;
        while (current != null)
        {
            if (current.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == targetBase) return true;
            current = current.BaseType;
        }

        // Check interfaces
        foreach (var iface in type.AllInterfaces)
        {
            if (iface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == targetBase) return true;
        }

        return false;
    }

    public static void AddConcreteDescendants(HashSet<ITypeSymbol> types, ITypeSymbol baseType, INamespaceSymbol ns, Action<HashSet<ITypeSymbol>, ITypeSymbol> addWithHierarchy)
    {
        foreach (var type in ns.GetTypeMembers())
        {
            if (!type.IsAbstract && type.TypeKind != TypeKind.Interface && IsDescendantOf(type, baseType))
            {
                var isObsolete = type.GetAttributes().Any(a => a.AttributeClass?.Name == "ObsoleteAttribute");
                if (!isObsolete || types.Contains(type))
                {
                    addWithHierarchy(types, type);
                }
            }
            // Also check nested types
            foreach (var nested in type.GetTypeMembers())
            {
                if (!nested.IsAbstract && nested.TypeKind != TypeKind.Interface && IsDescendantOf(nested, baseType))
                {
                    var isObsolete = nested.GetAttributes().Any(a => a.AttributeClass?.Name == "ObsoleteAttribute");
                    if (!isObsolete || types.Contains(nested))
                    {
                        addWithHierarchy(types, nested);
                    }
                }
            }
        }

        foreach (var childNs in ns.GetNamespaceMembers())
        {
            AddConcreteDescendants(types, baseType, childNs, addWithHierarchy);
        }
    }

    public static void EmitGeneratedClass(StringBuilder sb, IEnumerable<ITypeSymbol> allTypes, string indent = "    ")
    {
        sb.AppendLine($"{indent}public static class Generated");
        sb.AppendLine($"{indent}{{");
        sb.AppendLine($"{indent}    public static void Initialize(JsonSerializerOptions options)");
        sb.AppendLine($"{indent}    {{");
        foreach (var type in allTypes.OrderBy(t => t.Name))
        {
            var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var propertyName = GetPropertyName(type);
            sb.AppendLine($"{indent}        {propertyName} = (JsonTypeInfo<{typeName}>)options.GetTypeInfo(typeof({typeName}));");
        }
        sb.AppendLine($"{indent}    }}");
        sb.AppendLine();
        foreach (var type in allTypes.OrderBy(t => t.Name))
        {
            var typeName = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var propertyName = GetPropertyName(type);
            sb.AppendLine($"{indent}    public static JsonTypeInfo<{typeName}> {propertyName} {{ get; private set; }} = null!;");
        }
        sb.AppendLine($"{indent}}}");
    }

    private static string GetDiscriminator(ITypeSymbol descendant, ITypeSymbol parent)
    {
        return ToKebabCase(descendant.Name);
    }

    public static void EmitAddPolymorphism(StringBuilder sb, IEnumerable<ITypeSymbol> allTypes, string indent = "    ")
    {
        sb.AppendLine($"{indent}private static void AddPolymorphism(JsonTypeInfo typeInfo) =>");
        sb.AppendLine($"{indent}    typeInfo.PolymorphismOptions = typeInfo switch");
        sb.AppendLine($"{indent}    {{");
        sb.AppendLine();
        
        // Generate polymorphism options for any type that has descendants in the set
        var potentialParents = allTypes.Where(t => !t.IsSealed || t.TypeKind == TypeKind.Interface).ToList();
        foreach (var parent in potentialParents.OrderBy(t => t.ToDisplayString()))
        {
            var concreteDescendants = allTypes
                .Where(t => !SymbolEqualityComparer.Default.Equals(t, parent) && !t.IsAbstract && t.TypeKind != TypeKind.Interface && IsDescendantOf(t, parent))
                .OrderBy(t => t.Name)
                .ToList();

            if (concreteDescendants.Count > 0)
            {
                sb.AppendLine($"{indent}        JsonTypeInfo<{parent.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}> => new JsonPolymorphismOptions");
                sb.AppendLine($"{indent}        {{");
                sb.AppendLine($"{indent}            TypeDiscriminatorPropertyName = \"type\",");
                sb.AppendLine($"{indent}            DerivedTypes =");
                sb.AppendLine($"{indent}            {{");
                foreach (var descendant in concreteDescendants)
                {
                    var discriminator = GetDiscriminator(descendant, parent);
                    sb.AppendLine($"{indent}                new JsonDerivedType(typeof({descendant.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}), \"{discriminator}\"),");
                }
                sb.AppendLine($"{indent}            }}");
                sb.AppendLine($"{indent}        }},");
            }
        }

        sb.AppendLine($"{indent}        _ => null");
        sb.AppendLine($"{indent}    }};");
    }

    private static string GetPropertyName(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array)
        {
            return GetPropertyName(array.ElementType) + "Array";
        }
        if (type is INamedTypeSymbol named && named.IsGenericType)
        {
            var baseName = named.Name.Split('`')[0];
            return baseName + "_" + string.Join("_", named.TypeArguments.Select(GetPropertyName));
        }
        var name = type.Name;
        if (string.IsNullOrEmpty(name)) name = "Unknown";
        return name;
    }
}
