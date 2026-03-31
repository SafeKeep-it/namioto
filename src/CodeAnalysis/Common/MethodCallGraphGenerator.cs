// using System.Collections.Immutable;
// using System.Text;
// using Microsoft.CodeAnalysis;
// using Microsoft.CodeAnalysis.CSharp;
// using Microsoft.CodeAnalysis.CSharp.Syntax;
//
// namespace Comptatata.CodeAnalysis.Common;
//
// [Generator(LanguageNames.CSharp)]
// public class MethodCallGraphGenerator : IIncrementalGenerator
// {
//     public void Initialize(IncrementalGeneratorInitializationContext context)
//     {
//         var assemblies = context.AdditionalTextsProvider
//                                 .Where(static f => f.Path.EndsWith("assemblies.txt"))
//                                 .Select(static (f, ct) =>
//                                             f.GetText(ct)
//                                              ?.ToString()
//                                              .Split(new[] { '\r', '\n' },
//                                                     StringSplitOptions.RemoveEmptyEntries)
//                                              .ToImmutableHashSet() ??
//                                             ImmutableHashSet<string>.Empty)
//                                 .Collect()
//                                 .Select(static (items, _) =>
//                                             items.Length > 0
//                                                 ? items[0]
//                                                 : ImmutableHashSet<string>.Empty);
//
//         var symbolDeclarations = context.SyntaxProvider.CreateSyntaxProvider(
//                                             static (s, _) => s is MethodDeclarationSyntax or
//                                                 ConstructorDeclarationSyntax or
//                                                 AccessorDeclarationSyntax or
//                                                 DestructorDeclarationSyntax or
//                                                 BaseTypeDeclarationSyntax or
//                                                 PropertyDeclarationSyntax or
//                                                 FieldDeclarationSyntax or EventDeclarationSyntax or
//                                                 EventFieldDeclarationSyntax or
//                                                 DelegateDeclarationSyntax or
//                                                 IndexerDeclarationSyntax,
//                                             static (ctx, _) => ctx)
//                                         .Combine(assemblies);
//
//         context.RegisterSourceOutput(
//             symbolDeclarations.Collect().Combine(context.CompilationProvider),
//             static (spc, source) => Execute(source.Left, source.Right, spc));
//     }
//
//     static SymbolMetadata? GetMetadata(GeneratorSyntaxContext context,
//                                        ImmutableHashSet<string> solutionAssemblies)
//     {
//         var semanticModel = context.SemanticModel;
//         var node = context.Node;
//
//         var symbol = semanticModel.GetDeclaredSymbol(node);
//         if (symbol == null && node is FieldDeclarationSyntax field &&
//             field.Declaration.Variables.Count > 0)
//             symbol = semanticModel.GetDeclaredSymbol(field.Declaration.Variables[0]);
//         if (symbol == null && node is EventFieldDeclarationSyntax eventField &&
//             eventField.Declaration.Variables.Count > 0)
//             symbol = semanticModel.GetDeclaredSymbol(eventField.Declaration.Variables[0]);
//
//         if (symbol == null) return null;
//         if (!IsDefinedInSolution(symbol, solutionAssemblies)) return null;
//
//         var symbolId = symbol.GetDocumentationCommentId();
//         if (symbolId == null) return null;
//
//         var kind = symbol.Kind.ToString().ToLower();
//         var modifiers = symbol.DeclaredAccessibility.ToString().ToLower();
//         if (symbol.IsStatic) modifiers += " static";
//         if (symbol.IsAbstract) modifiers += " abstract";
//         if (symbol.IsSealed) modifiers += " sealed";
//
//         if (node is MemberDeclarationSyntax member &&
//             member.Modifiers.Any(SyntaxKind.PartialKeyword))
//             modifiers += " partial";
//
//         HashSet<string> dependencies = new();
//         HashSet<string> typeArguments = new();
//
//         // New fields for enhanced metadata
//         string? signature = null;
//         string? returnType = null;
//         List<(string Name, string Type)> parameters = new();
//         var containingType = symbol.ContainingType?.GetDocumentationCommentId();
//
//         // Additional metadata fields
//         string? baseType = null;
//         List<string> interfaces = new();
//         var isExtension = false;
//         string? extendedType = null;
//         var isGenerated = false;
//         string? generatorName = null;
//         string? constructorKind = null;
//         string? nullable = null;
//         List<string> usings = new();
//         List<string> operators = new();
//
//         // Synthesized member detection
//         var isSynthesized = false;
//         string? synthesizedKind = null;
//         string? synthesizedFrom = null;
//
//         // Detect compiler-synthesized members (record equality, backing fields, etc.)
//         if (symbol.IsImplicitlyDeclared)
//         {
//             isSynthesized = true;
//             synthesizedFrom = symbol.ContainingType?.GetDocumentationCommentId();
//
//             // Classify synthesized kind
//             if (symbol is IMethodSymbol synthMethod)
//             {
//                 var containingIsRecord = symbol.ContainingType?.IsRecord == true;
//                 synthesizedKind = synthMethod.Name switch
//                                   {
//                                       "GetHashCode" when containingIsRecord => "record-gethashcode",
//                                       "Equals" when containingIsRecord => "record-equals",
//                                       "ToString" when containingIsRecord => "record-tostring",
//                                       "PrintMembers" when containingIsRecord =>
//                                           "record-printmembers",
//                                       "Deconstruct" when containingIsRecord => "record-deconstruct",
//                                       "<Clone>$" when containingIsRecord => "record-clone",
//                                       ".ctor" when containingIsRecord &&
//                                                    synthMethod.Parameters.Length == 1 &&
//                                                    SymbolEqualityComparer.Default.Equals(
//                                                        synthMethod.Parameters[0].Type,
//                                                        symbol.ContainingType) => "record-copy-ctor",
//                                       "op_Equality" or "op_Inequality" when containingIsRecord =>
//                                           "record-equality-op",
//                                       "get_EqualityContract" when containingIsRecord =>
//                                           "record-equality-contract",
//                                       var _ when synthMethod.MethodKind == MethodKind.PropertyGet =>
//                                           "auto-property-getter",
//                                       var _ when synthMethod.MethodKind == MethodKind.PropertySet =>
//                                           "auto-property-setter",
//                                       var _ when synthMethod.MethodKind == MethodKind.EventAdd =>
//                                           "event-add",
//                                       var _ when synthMethod.MethodKind == MethodKind.EventRemove =>
//                                           "event-remove",
//                                       var _ => "compiler-generated"
//                                   };
//             }
//             else if (symbol is IFieldSymbol synthField)
//             {
//                 synthesizedKind = synthField.AssociatedSymbol switch
//                                   {
//                                       IPropertySymbol => "backing-field-property",
//                                       IEventSymbol => "backing-field-event",
//                                       var _ => "compiler-generated-field"
//                                   };
//                 // Link backing field to its associated property/event
//                 if (synthField.AssociatedSymbol != null)
//                     synthesizedFrom = synthField.AssociatedSymbol.GetDocumentationCommentId();
//             }
//             else if (symbol is INamedTypeSymbol synthType)
//             {
//                 synthesizedKind = synthType.IsAnonymousType ? "anonymous-type" :
//                     synthType.IsTupleType ? "tuple-type" : "compiler-generated-type";
//             }
//         }
//
//         // Check if generated (from attribute or filename)
//         var generatedAttr = symbol.GetAttributes()
//                                   .FirstOrDefault(a => a.AttributeClass?.Name ==
//                                                        "GeneratedCodeAttribute" ||
//                                                        a.AttributeClass?.Name ==
//                                                        "CompilerGeneratedAttribute");
//         if (generatedAttr != null)
//         {
//             isGenerated = true;
//             if (generatedAttr.ConstructorArguments.Length > 0)
//                 generatorName = generatedAttr.ConstructorArguments[0].Value?.ToString();
//         }
//         else if (node.SyntaxTree.FilePath.EndsWith(".g.cs") ||
//                  node.SyntaxTree.FilePath.EndsWith(".generated.cs"))
//         {
//             isGenerated = true;
//             generatorName = "file";
//         }
//
//         // Get nullable context
//         var nullableContext = semanticModel.GetNullableContext(node.SpanStart);
//         if (nullableContext.HasFlag(NullableContext.Enabled)) nullable = "enable";
//
//         // Get using directives from the file
//         if (node.SyntaxTree.GetRoot() is CompilationUnitSyntax root)
//             foreach (var u in root.Usings)
//             {
//                 var usingText = u.Name?.ToString();
//                 if (usingText != null) usings.Add(usingText);
//             }
//
//         // 1. Signature-based
//         if (symbol is IMethodSymbol methodSymbol)
//         {
//             // Return type: always add to dependencies if in solution (as it's not in ID)
//             returnType =
//                 methodSymbol.ReturnType.ToDisplayString(
//                     SymbolDisplayFormat.MinimallyQualifiedFormat);
//             AddSolutionType(methodSymbol.ReturnType,
//                             dependencies,
//                             typeArguments,
//                             solutionAssemblies);
//
//             // Parameters: capture name and type for enhanced metadata
//             foreach (var p in methodSymbol.Parameters)
//             {
//                 parameters.Add(
//                     (p.Name, p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
//                 AddSolutionType(p.Type, null, typeArguments, solutionAssemblies);
//             }
//
//             // Build signature string
//             var paramStr = string.Join(", ",
//                                        methodSymbol.Parameters.Select(p =>
//                                            $"{p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {p.Name}"));
//             signature = $"{returnType} {methodSymbol.Name}({paramStr})";
//
//             foreach (var tp in methodSymbol.TypeParameters)
//             foreach (var constraint in tp.ConstraintTypes)
//                 AddSolutionType(constraint, dependencies, typeArguments, solutionAssemblies);
//
//             // Extension method detection
//             if (methodSymbol.IsExtensionMethod && methodSymbol.Parameters.Length > 0)
//             {
//                 isExtension = true;
//                 extendedType = methodSymbol.Parameters[0].Type.GetDocumentationCommentId() ??
//                                methodSymbol.Parameters[0]
//                                            .Type.ToDisplayString(
//                                                SymbolDisplayFormat.MinimallyQualifiedFormat);
//             }
//
//             // Constructor kind detection
//             if (methodSymbol.MethodKind == MethodKind.Constructor)
//             {
//                 // Check if it's a primary constructor by looking at the syntax
//                 if (node is ConstructorDeclarationSyntax ctorSyntax)
//                 {
//                     constructorKind = "explicit";
//                 }
//                 else if (symbol.ContainingType is INamedTypeSymbol containingType2 &&
//                          containingType2.DeclaringSyntaxReferences.Length > 0)
//                 {
//                     var typeSyntax = containingType2.DeclaringSyntaxReferences[0].GetSyntax();
//                     if (typeSyntax is TypeDeclarationSyntax typeDecl &&
//                         typeDecl.ParameterList != null)
//                         constructorKind = "primary";
//                 }
//             }
//         }
//         else if (symbol is INamedTypeSymbol typeSymbol)
//         {
//             kind = typeSymbol.TypeKind.ToString().ToLower();
//
//             // Capture base type explicitly
//             if (typeSymbol.BaseType != null &&
//                 typeSymbol.BaseType.SpecialType != SpecialType.System_Object)
//                 baseType = typeSymbol.BaseType.GetDocumentationCommentId() ??
//                            typeSymbol.BaseType.ToDisplayString(
//                                SymbolDisplayFormat.MinimallyQualifiedFormat);
//
//             // Capture interfaces explicitly
//             foreach (var i in typeSymbol.Interfaces)
//             {
//                 var ifaceId = i.GetDocumentationCommentId() ??
//                               i.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
//                 interfaces.Add(ifaceId);
//             }
//
//             // Check if this is a static class with extension methods (extension class)
//             if (typeSymbol.IsStatic && typeSymbol.GetMembers()
//                                                  .OfType<IMethodSymbol>()
//                                                  .Any(m => m.IsExtensionMethod))
//                 isExtension = true;
//
//             // Extract operator overloads
//             foreach (var op in typeSymbol.GetMembers()
//                                          .OfType<IMethodSymbol>()
//                                          .Where(m =>
//                                                     m.MethodKind ==
//                                                     MethodKind.UserDefinedOperator ||
//                                                     m.MethodKind == MethodKind.Conversion))
//             {
//                 var opName = op.Name;
//                 var opParams = string.Join(",",
//                                            op.Parameters.Select(p => p.Type.ToDisplayString(
//                                                                     SymbolDisplayFormat
//                                                                         .MinimallyQualifiedFormat)));
//                 var opReturn =
//                     op.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
//                 operators.Add($"{opName}({opParams}):{opReturn}");
//             }
//
//             AddSolutionType(typeSymbol.BaseType, dependencies, typeArguments, solutionAssemblies);
//             foreach (var i in typeSymbol.Interfaces)
//                 AddSolutionType(i, dependencies, typeArguments, solutionAssemblies);
//             foreach (var tp in typeSymbol.TypeParameters)
//             foreach (var constraint in tp.ConstraintTypes)
//                 AddSolutionType(constraint, dependencies, typeArguments, solutionAssemblies);
//
//             // For delegates, capture the invoke method signature
//             if (typeSymbol.TypeKind == TypeKind.Delegate && typeSymbol.DelegateInvokeMethod != null)
//             {
//                 var invoke = typeSymbol.DelegateInvokeMethod;
//                 returnType =
//                     invoke.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
//                 foreach (var p in invoke.Parameters)
//                 {
//                     parameters.Add(
//                         (p.Name,
//                          p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
//                     AddSolutionType(p.Type, null, typeArguments, solutionAssemblies);
//                 }
//
//                 AddSolutionType(invoke.ReturnType, dependencies, typeArguments, solutionAssemblies);
//                 var paramStr = string.Join(", ",
//                                            invoke.Parameters.Select(p =>
//                                                $"{p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {p.Name}"));
//                 signature = $"delegate {returnType} {typeSymbol.Name}({paramStr})";
//             }
//         }
//         else if (symbol is IPropertySymbol propertySymbol)
//         {
//             returnType =
//                 propertySymbol.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
//             var accessors = new List<string>();
//             if (propertySymbol.GetMethod != null) accessors.Add("get");
//             if (propertySymbol.SetMethod != null)
//                 accessors.Add(propertySymbol.SetMethod.IsInitOnly ? "init" : "set");
//
//             if (propertySymbol.IsIndexer)
//             {
//                 kind = "indexer";
//                 foreach (var p in propertySymbol.Parameters)
//                 {
//                     parameters.Add(
//                         (p.Name,
//                          p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
//                     AddSolutionType(p.Type, null, typeArguments, solutionAssemblies);
//                 }
//
//                 var paramStr = string.Join(", ",
//                                            propertySymbol.Parameters.Select(p =>
//                                                $"{p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {p.Name}"));
//                 signature = $"{returnType} this[{paramStr}] {{ {string.Join("; ", accessors)}; }}";
//             }
//             else
//             {
//                 signature =
//                     $"{returnType} {propertySymbol.Name} {{ {string.Join("; ", accessors)}; }}";
//             }
//
//             AddSolutionType(propertySymbol.Type, dependencies, typeArguments, solutionAssemblies);
//         }
//         else if (symbol is IFieldSymbol fieldSymbol)
//         {
//             returnType =
//                 fieldSymbol.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
//             signature = $"{returnType} {fieldSymbol.Name}";
//             AddSolutionType(fieldSymbol.Type, dependencies, typeArguments, solutionAssemblies);
//         }
//         else if (symbol is IEventSymbol eventSymbol)
//         {
//             kind = "event";
//             returnType =
//                 eventSymbol.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
//             signature = $"event {returnType} {eventSymbol.Name}";
//             AddSolutionType(eventSymbol.Type, dependencies, typeArguments, solutionAssemblies);
//         }
//
//         // 2. Body-based (Calls are never duplicates)
//         if (node is MethodDeclarationSyntax or ConstructorDeclarationSyntax or
//                     AccessorDeclarationSyntax or DestructorDeclarationSyntax)
//         {
//             foreach (var invocation in node.DescendantNodes().OfType<InvocationExpressionSyntax>())
//             {
//                 var info = semanticModel.GetSymbolInfo(invocation);
//                 var target = info.Symbol ??
//                              (info.CandidateSymbols.Length > 0 ? info.CandidateSymbols[0] : null);
//                 AddSolutionType(target, dependencies, typeArguments, solutionAssemblies);
//             }
//
//             foreach (var creation in node.DescendantNodes()
//                                          .OfType<ObjectCreationExpressionSyntax>())
//             {
//                 var info = semanticModel.GetSymbolInfo(creation);
//                 var target = info.Symbol ??
//                              (info.CandidateSymbols.Length > 0 ? info.CandidateSymbols[0] : null);
//                 AddSolutionType(target, dependencies, typeArguments, solutionAssemblies);
//             }
//         }
//
//         // 3. Extract attributes
//         var attributes = new List<AttributeMetadata>();
//         foreach (var attr in symbol.GetAttributes())
//         {
//             if (attr.AttributeClass == null) continue;
//             var attrName =
//                 attr.AttributeClass.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
//
//             var ctorArgs = attr.ConstructorArguments.Select(a => a.ToCSharpString()).ToList();
//
//             var namedArgs = attr.NamedArguments.Select(a => (a.Key, a.Value.ToCSharpString()))
//                                 .ToList();
//
//             attributes.Add(new(attrName, ctorArgs, namedArgs));
//         }
//
//         var lineSpan = node.GetLocation().GetLineSpan();
//
//         // Extract Roslyn checksum for change detection
//         var checksum = node.SyntaxTree.GetText().GetChecksum();
//         var checksumHex = BitConverter.ToString(checksum.ToArray())
//                                       .Replace("-", "")
//                                       .ToLowerInvariant();
//
//         return new(symbolId,
//                    lineSpan.Path,
//                    lineSpan.StartLinePosition.Line + 1,
//                    lineSpan.EndLinePosition.Line + 1,
//                    dependencies.Where(d => d != symbolId).ToList(),
//                    typeArguments.Where(d => d != symbolId).ToList(),
//                    kind,
//                    modifiers,
//                    signature,
//                    returnType,
//                    parameters,
//                    containingType,
//                    attributes,
//                    baseType,
//                    interfaces,
//                    isExtension,
//                    extendedType,
//                    isGenerated,
//                    generatorName,
//                    constructorKind,
//                    nullable,
//                    usings,
//                    operators,
//                    isSynthesized,
//                    synthesizedKind,
//                    synthesizedFrom,
//                    checksumHex);
//     }
//
//     static void AddSolutionType(ISymbol? symbol,
//                                 HashSet<string>? dependencies,
//                                 HashSet<string> typeArguments,
//                                 ImmutableHashSet<string> solutionAssemblies)
//     {
//         if (symbol == null) return;
//
//         if (IsDefinedInSolution(symbol, solutionAssemblies))
//         {
//             var id = symbol.GetDocumentationCommentId();
//             if (id != null) dependencies?.Add(id);
//         }
//
//         if (symbol is INamedTypeSymbol namedType && namedType.IsGenericType)
//         {
//             foreach (var arg in namedType.TypeArguments)
//             {
//                 if (arg.TypeKind != TypeKind.TypeParameter &&
//                     IsDefinedInSolution(arg, solutionAssemblies))
//                 {
//                     var id = arg.GetDocumentationCommentId();
//                     if (id != null) typeArguments.Add(id);
//                 }
//                 // No recursion here as per "rebuild recursive from script" instruction.
//             }
//         }
//         else if (symbol is IMethodSymbol method && method.IsGenericMethod)
//         {
//             foreach (var arg in method.TypeArguments)
//             {
//                 if (arg.TypeKind != TypeKind.TypeParameter &&
//                     IsDefinedInSolution(arg, solutionAssemblies))
//                 {
//                     var id = arg.GetDocumentationCommentId();
//                     if (id != null) typeArguments.Add(id);
//                 }
//             }
//         }
//     }
//
//     static bool IsDefinedInSolution(ISymbol? symbol, ImmutableHashSet<string> solutionAssemblies)
//     {
//         if (symbol == null) return false;
//         if (symbol.Locations.Any(l => l.IsInSource)) return true;
//
//         var assemblyName = symbol.ContainingAssembly?.Name;
//         if (assemblyName == null) return false;
//
//         if (solutionAssemblies.IsEmpty)
//             return assemblyName.StartsWith("Comptatata") || assemblyName == "Tests";
//
//         return solutionAssemblies.Contains(assemblyName);
//     }
//
//     static void Execute(
//         ImmutableArray<(GeneratorSyntaxContext Context, ImmutableHashSet<string> Assemblies)> items,
//         Compilation compilation,
//         SourceProductionContext context)
//     {
//         if (items.IsEmpty) return;
//
//         var symbols = items.Select(i => GetMetadata(i.Context, i.Assemblies))
//                            .Where(m => m != null)
//                            .Select(m => m!)
//                            .ToList();
//         if (symbols.Count == 0) return;
//
//         var projectRoot = FindSolutionRoot(symbols[0].FilePath);
//         if (projectRoot == null) return;
//
//         var ontologyDir = Path.Combine(projectRoot, "src", "dotnet", ".ontology");
//         var docsDir = Path.Combine(ontologyDir, "docs");
//         var tmpDir = Path.Combine(docsDir, ".tmp");
//
//         if (!Directory.Exists(tmpDir)) Directory.CreateDirectory(tmpDir);
//
//         var projectName = compilation.AssemblyName ?? "UnknownProject";
//         var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
//
//         // Build all symbol JSON lines
//         var allLines = new StringBuilder();
//         var symbolIds = new List<string>();
//
//         foreach (var s in symbols)
//         {
//             var relativePath = s.FilePath;
//             if (relativePath.StartsWith(projectRoot))
//                 relativePath = relativePath.Substring(projectRoot.Length)
//                                            .TrimStart(Path.DirectorySeparatorChar);
//
//             var json = BuildSymbolJson(s, relativePath, timestamp);
//             allLines.AppendLine(json);
//             symbolIds.Add(s.Symbol);
//         }
//
//         // Write symbols to timestamped shape file in docs/.tmp
//         // Format: <projectName>.<timestamp>.shape.ndjson (compacted to <projectName>.doc.ndjson)
//         var filePath = Path.Combine(tmpDir, $"{projectName}.{timestamp}.shape.ndjson");
//
//         try
//         {
//             File.WriteAllText(filePath, allLines.ToString());
//         }
//         catch
//         {
//             return;
//         }
//
//         // Try to notify daemon; if unavailable, leave file for later pickup.
//         // IMPORTANT: daemon expects the timestamp to match the shape file timestamp.
//         // Generator must stay fast: single best-effort touch only.
//         BalorClient.TryTouch($"PROJECT:{projectName}", projectName, timestamp);
//         // No separate inbox file needed - .tmp shape files serve as inbox
//     }
//
//     static string BuildSymbolJson(SymbolMetadata s, string relativePath, long timestamp)
//     {
//         var depsJson = string.Join(",", s.Dependencies.Select(d => $"\"{Escape(d)}\""));
//         var argsJson = string.Join(",", s.TypeArguments.Select(d => $"\"{Escape(d)}\""));
//         var paramsJson = string.Join(",",
//                                      s.Parameters.Select(p =>
//                                                              $"{{\"name\":\"{Escape(p.Name)}\",\"type\":\"{Escape(p.Type)}\"}}"));
//         var attrsJson = string.Join(",",
//                                     s.Attributes.Select(a =>
//                                     {
//                                         var ctorArgsJson = string.Join(
//                                             ",",
//                                             a.ConstructorArgs.Select(c => $"\"{Escape(c)}\""));
//                                         var namedArgsJson = string.Join(
//                                             ",",
//                                             a.NamedArgs.Select(n =>
//                                                                    $"{{\"name\":\"{Escape(n.Name)}\",\"value\":\"{Escape(n.Value)}\"}}"));
//                                         return
//                                             $"{{\"name\":\"{Escape(a.Name)}\",\"constructorArgs\":[{ctorArgsJson}],\"namedArgs\":[{namedArgsJson}]}}";
//                                     }));
//
//         var signatureField = s.Signature != null ? $",\"signature\":\"{Escape(s.Signature)}\"" : "";
//         var returnTypeField =
//             s.ReturnType != null ? $",\"returnType\":\"{Escape(s.ReturnType)}\"" : "";
//         var paramsField = s.Parameters.Count > 0 ? $",\"parameters\":[{paramsJson}]" : "";
//         var containingField = s.ContainingType != null
//             ? $",\"containingType\":\"{Escape(s.ContainingType)}\""
//             : "";
//         var attrsField = s.Attributes.Count > 0 ? $",\"attributes\":[{attrsJson}]" : "";
//
//         // New fields
//         var baseTypeField = s.BaseType != null ? $",\"baseType\":\"{Escape(s.BaseType)}\"" : "";
//         var interfacesJson = string.Join(",", s.Interfaces.Select(i => $"\"{Escape(i)}\""));
//         var interfacesField = s.Interfaces.Count > 0 ? $",\"interfaces\":[{interfacesJson}]" : "";
//         var extensionField = s.IsExtension ? ",\"isExtension\":true" : "";
//         var extendedTypeField = s.ExtendedType != null
//             ? $",\"extendedType\":\"{Escape(s.ExtendedType)}\""
//             : "";
//         var generatedField = s.IsGenerated ? ",\"isGenerated\":true" : "";
//         var generatorField = s.GeneratorName != null
//             ? $",\"generatorName\":\"{Escape(s.GeneratorName)}\""
//             : "";
//         var ctorKindField = s.ConstructorKind != null
//             ? $",\"constructorKind\":\"{Escape(s.ConstructorKind)}\""
//             : "";
//         var nullableField = s.Nullable != null ? $",\"nullable\":\"{Escape(s.Nullable)}\"" : "";
//         var usingsJson = string.Join(",", s.Usings.Select(u => $"\"{Escape(u)}\""));
//         var usingsField = s.Usings.Count > 0 ? $",\"usings\":[{usingsJson}]" : "";
//         var operatorsJson = string.Join(",", s.Operators.Select(o => $"\"{Escape(o)}\""));
//         var operatorsField = s.Operators.Count > 0 ? $",\"operators\":[{operatorsJson}]" : "";
//
//         // Synthesized fields
//         var synthesizedField = s.IsSynthesized ? ",\"isSynthesized\":true" : "";
//         var synthesizedKindField = s.SynthesizedKind != null
//             ? $",\"synthesizedKind\":\"{Escape(s.SynthesizedKind)}\""
//             : "";
//         var synthesizedFromField = s.SynthesizedFrom != null
//             ? $",\"synthesizedFrom\":\"{Escape(s.SynthesizedFrom)}\""
//             : "";
//
//         // Checksum field
//         var checksumField = $",\"checksum\":\"{Escape(s.Checksum)}\"";
//
//         // "change":"modified" indicates this symbol was (re)compiled - delta detection determines actual status
//         return
//             $"{{\"symbol\":\"{Escape(s.Symbol)}\",\"timestamp\":{timestamp},\"change\":\"modified\",\"kind\":\"{Escape(s.Kind)}\",\"modifiers\":\"{Escape(s.Modifiers)}\",\"file\":\"{Escape(relativePath)}\",\"startLine\":{s.StartLine},\"endLine\":{s.EndLine}{signatureField}{returnTypeField}{paramsField}{containingField}{attrsField}{baseTypeField}{interfacesField}{extensionField}{extendedTypeField}{generatedField}{generatorField}{ctorKindField}{nullableField}{usingsField}{operatorsField}{synthesizedField}{synthesizedKindField}{synthesizedFromField}{checksumField},\"dependencies\":[{depsJson}],\"typeArguments\":[{argsJson}]}}";
//     }
//
//     static string? FindSolutionRoot(string filePath)
//     {
//         var current = Path.GetDirectoryName(filePath);
//         while (!string.IsNullOrEmpty(current))
//         {
//             if (Directory.GetFiles(current, "Comptatata.sln").Any() ||
//                 Directory.GetFiles(current, "Comptatata.slnx").Any())
//                 return current;
//             current = Path.GetDirectoryName(current);
//         }
//
//         return null;
//     }
//
//     static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
//
//     class SymbolMetadata
//     {
//         public SymbolMetadata(string symbol,
//                               string filePath,
//                               int startLine,
//                               int endLine,
//                               List<string> dependencies,
//                               List<string> typeArguments,
//                               string kind,
//                               string modifiers,
//                               string? signature,
//                               string? returnType,
//                               List<(string Name, string Type)> parameters,
//                               string? containingType,
//                               List<AttributeMetadata> attributes,
//                               string? baseType,
//                               List<string> interfaces,
//                               bool isExtension,
//                               string? extendedType,
//                               bool isGenerated,
//                               string? generatorName,
//                               string? constructorKind,
//                               string? nullable,
//                               List<string> usings,
//                               List<string> operators,
//                               bool isSynthesized,
//                               string? synthesizedKind,
//                               string? synthesizedFrom,
//                               string checksum)
//         {
//             Symbol = symbol;
//             FilePath = filePath;
//             StartLine = startLine;
//             EndLine = endLine;
//             Dependencies = dependencies;
//             TypeArguments = typeArguments;
//             Kind = kind;
//             Modifiers = modifiers;
//             Signature = signature;
//             ReturnType = returnType;
//             Parameters = parameters;
//             ContainingType = containingType;
//             Attributes = attributes;
//             BaseType = baseType;
//             Interfaces = interfaces;
//             IsExtension = isExtension;
//             ExtendedType = extendedType;
//             IsGenerated = isGenerated;
//             GeneratorName = generatorName;
//             ConstructorKind = constructorKind;
//             Nullable = nullable;
//             Usings = usings;
//             Operators = operators;
//             IsSynthesized = isSynthesized;
//             SynthesizedKind = synthesizedKind;
//             SynthesizedFrom = synthesizedFrom;
//             Checksum = checksum;
//         }
//
//         public string Symbol { get; }
//         public string FilePath { get; }
//         public int StartLine { get; }
//         public int EndLine { get; }
//         public List<string> Dependencies { get; }
//         public List<string> TypeArguments { get; }
//         public string Kind { get; }
//         public string Modifiers { get; }
//         public string? Signature { get; }
//         public string? ReturnType { get; }
//         public List<(string Name, string Type)> Parameters { get; }
//         public string? ContainingType { get; }
//         public List<AttributeMetadata> Attributes { get; }
//         public string? BaseType { get; }
//         public List<string> Interfaces { get; }
//         public bool IsExtension { get; }
//         public string? ExtendedType { get; }
//         public bool IsGenerated { get; }
//         public string? GeneratorName { get; }
//         public string? ConstructorKind { get; }
//         public string? Nullable { get; }
//         public List<string> Usings { get; }
//         public List<string> Operators { get; }
//         public bool IsSynthesized { get; }
//         public string? SynthesizedKind { get; }
//         public string? SynthesizedFrom { get; }
//         public string Checksum { get; }
//     }
//
//     class AttributeMetadata
//     {
//         public AttributeMetadata(string name,
//                                  List<string> constructorArgs,
//                                  List<(string Name, string Value)> namedArgs)
//         {
//             Name = name;
//             ConstructorArgs = constructorArgs;
//             NamedArgs = namedArgs;
//         }
//
//         public string Name { get; }
//         public List<string> ConstructorArgs { get; }
//         public List<(string Name, string Value)> NamedArgs { get; }
//     }
// }