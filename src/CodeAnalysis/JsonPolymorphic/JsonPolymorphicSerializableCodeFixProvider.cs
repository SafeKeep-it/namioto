using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Simplification;

namespace Comptatata.CodeAnalysis.JsonPolymorphic;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(JsonPolymorphicSerializableCodeFixProvider)), Shared]
public class JsonPolymorphicSerializableCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(JsonPolymorphicSerializableAnalyzer.DiagnosticId);

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root == null) return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        // Find the type declaration identified by the diagnostic.
        var declaration = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (declaration == null) return;

        if (diagnostic.Properties.TryGetValue("MissingType", out var missingType) && missingType != null)
        {
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Add [JsonSerializable(typeof({GetShortName(missingType)}))]",
                    createChangedDocument: c => AddJsonSerializableAttributeAsync(context.Document, declaration, missingType, c),
                    equivalenceKey: $"AddJsonSerializable_{missingType}"),
                diagnostic);
        }
    }

    private string GetShortName(string fullyQualifiedName)
    {
        if (fullyQualifiedName.StartsWith("global::"))
        {
            fullyQualifiedName = fullyQualifiedName.Substring(8);
        }
        var lastDot = fullyQualifiedName.LastIndexOf('.');
        return lastDot == -1 ? fullyQualifiedName : fullyQualifiedName.Substring(lastDot + 1);
    }

    private async Task<Document> AddJsonSerializableAttributeAsync(Document document, TypeDeclarationSyntax typeDeclaration, string missingType, CancellationToken cancellationToken)
    {
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        var typeName = SyntaxFactory.ParseTypeName(missingType)
            .WithAdditionalAnnotations(Simplifier.Annotation);

        var attributeArgument = SyntaxFactory.AttributeArgument(
            SyntaxFactory.TypeOfExpression(typeName));

        var attribute = SyntaxFactory.Attribute(
            SyntaxFactory.ParseName("JsonSerializable"),
            SyntaxFactory.AttributeArgumentList(SyntaxFactory.SingletonSeparatedList(attributeArgument)));

        // Try to find where to insert. We'd like to insert it after other [JsonSerializable] attributes.
        var lastJsonSerializable = typeDeclaration.AttributeLists
            .SelectMany(al => al.Attributes)
            .LastOrDefault(a => IsJsonSerializable(a));

        if (lastJsonSerializable != null)
        {
            var attributeList = (AttributeListSyntax)lastJsonSerializable.Parent!;
            // If it's the only attribute in the list, we can just insert a new list after it.
            // But if there are multiple attributes in the list [A, B], it's more complex.
            // STJ usually has one attribute per list for these.

            var newAttributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute))
                .WithLeadingTrivia(attributeList.GetLeadingTrivia())
                .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);

            editor.InsertAfter(attributeList, newAttributeList);
        }
        else
        {
            editor.AddAttribute(typeDeclaration, attribute);
        }

        return editor.GetChangedDocument();
    }

    private bool IsJsonSerializable(AttributeSyntax attribute)
    {
        var name = attribute.Name.ToString();
        return name == "JsonSerializable" || name == "JsonSerializableAttribute" || name.EndsWith(".JsonSerializable") || name.EndsWith(".JsonSerializableAttribute");
    }
}