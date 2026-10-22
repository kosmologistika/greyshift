using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Rs13.Packs.Generators;

/// <summary>
/// Writes <c>ModEntry</c> from the attributes on the module's types and its
/// <c>EntitySystem</c> subclasses, so nothing is listed by hand: the own types
/// with their paths, each system with its <c>[Dependency]</c> fields filled,
/// and the markers that make the grants constraints. The JSON contexts it
/// names are written before compile by the props, and a registered type
/// missing from them is an error here rather than a value that ships as <c>{}</c>
/// </summary>
[Generator]
public sealed class RegistrationsGenerator : IIncrementalGenerator
{
    const string Sdk = "Rs13.Packs.";

    static readonly DiagnosticDescriptor SystemShape = new(
        "RS13001",
        "A system is not partial or takes constructor parameters",
        "{0} {1}: a system is a partial class the generator completes, built with no arguments on every setup",
        "Rs13.Packs",
        DiagnosticSeverity.Error,
        true);

    static readonly DiagnosticDescriptor DependencyShape = new(
        "RS13002",
        "A [Dependency] cannot be filled",
        "{0}.{1} {2}: a dependency is a plain field holding another of the module's systems",
        "Rs13.Packs",
        DiagnosticSeverity.Error,
        true);

    static readonly DiagnosticDescriptor SystemState = new(
        "RS13003",
        "A system holds state",
        "{0}.{1} is not a [Dependency]: a system is rebuilt on every setup, so its state lives in own components and resources",
        "Rs13.Packs",
        DiagnosticSeverity.Error,
        true);

    static readonly DiagnosticDescriptor NotStored = new(
        "RS13004",
        "An own type is not shaped for storage",
        "{0}{1} {2}: every instance member of an own type is public, settable and marked [DataField], and a component derives from Component and is registered",
        "Rs13.Packs",
        DiagnosticSeverity.Error,
        true);

    static readonly DiagnosticDescriptor MissingFromContext = new(
        "RS13005",
        "Registered type missing from the JSON context",
        "{0} is registered but {1} does not list it: the pre-compile scan in Rs13.Packs.props missed its attribute, check the declaration is `[{2}] class {3}` at namespace level",
        "Rs13.Packs",
        DiagnosticSeverity.Error,
        true);

    static readonly DiagnosticDescriptor Nested = new(
        "RS13006",
        "A nested type cannot be an own type",
        "{0} is nested in {1}: an own type is declared at namespace level",
        "Rs13.Packs",
        DiagnosticSeverity.Error,
        true);

    static readonly DiagnosticDescriptor DuplicatePath = new(
        "RS13007",
        "Two own types share a path",
        "{0} and {1} both register as {2}",
        "Rs13.Packs",
        DiagnosticSeverity.Error,
        true);

    static readonly DiagnosticDescriptor NotPartial = new(
        "RS13009",
        "An own type is partial",
        "{0} is not partial: the generator marks an own type with what the module may do with it",
        "Rs13.Packs",
        DiagnosticSeverity.Error,
        true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var registrations = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax or RecordDeclarationSyntax,
                static (ctx, _) => Registration.Of(ctx))
            .Where(static r => r is not null)
            .Select(static (r, _) => r!)
            .Collect();
        var derived = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax,
                static (ctx, _) => Derived.Of(ctx))
            .Where(static d => d is not null)
            .Select(static (d, _) => d!)
            .Collect();
        context.RegisterSourceOutput(
            registrations.Combine(derived).Combine(context.CompilationProvider),
            static (spc, pair) => Emit(spc, pair.Left.Left, pair.Left.Right, pair.Right));
    }

    static void Emit(SourceProductionContext spc, ImmutableArray<Registration> found, ImmutableArray<Derived> derived, Compilation compilation)
    {
        var sorted = found.Sort(static (a, b) => string.CompareOrdinal(a.Path, b.Path));
        var systems = derived.Where(static d => d.System is not null).Select(static d => d.System!).OrderBy(static s => s.Type, StringComparer.Ordinal).ToList();
        foreach (var problem in derived.SelectMany(static d => d.Problems))
        {
            spc.ReportDiagnostic(problem);
        }
        var own = compilation.GetTypeByMetadataName(Sdk + "ModJson");
        var mirrors = compilation.GetTypeByMetadataName(Sdk + "MirrorJson");
        var seen = new Dictionary<string, Registration>();
        foreach (var r in sorted)
        {
            if (seen.TryGetValue(r.Path, out var first))
            {
                spc.ReportDiagnostic(Diagnostic.Create(DuplicatePath, r.Location, first.Metadata, r.Metadata, r.Path));
            }
            seen[r.Path] = r;
            foreach (var d in r.Problems)
            {
                spc.ReportDiagnostic(d);
            }
            var (context, name) = r.Kind == Kind.Mirror ? (mirrors, "MirrorJson") : (own, "ModJson");
            if (!Lists(context, r.Metadata))
            {
                spc.ReportDiagnostic(Diagnostic.Create(MissingFromContext, r.Location, r.Metadata, name, r.Attribute, r.Name));
            }
            if (!r.Partial)
            {
                spc.ReportDiagnostic(Diagnostic.Create(NotPartial, r.Location, r.Name));
            }
            if (r.Kind == Kind.Component && !r.IsComponent)
            {
                spc.ReportDiagnostic(Diagnostic.Create(NotStored, r.Location, r.Name, "", "does not derive from Component"));
            }
        }

        var source = new StringBuilder();
        source.AppendLine("// <auto-generated/> Rs13.Packs.Generators");
        source.AppendLine("namespace Rs13.Packs");
        source.AppendLine("{");
        source.AppendLine("    static partial class ModEntry");
        source.AppendLine("    {");
        source.AppendLine("        internal static partial void Build(global::Rs13.Packs.ModBuilder b)");
        source.AppendLine("        {");
        Contexts(source, sorted);
        foreach (var r in sorted)
        {
            source.AppendLine(r.Kind switch
            {
                Kind.Component => $"            b.Component<{r.Type}>(\"{r.Path}\", networked: {(r.Networked ? "true" : "false")});",
                Kind.Resource => $"            b.Resource<{r.Type}>(\"{r.Path}\");",
                Kind.Message => $"            b.Message<{r.Type}>(\"{r.Path}\");",
                _ => $"            b.Mirror<{r.Type}>(\"{r.Path}\");",
            });
        }
        foreach (var s in systems)
        {
            source.AppendLine($"            b.System(new {s.Type}(), updates: {(s.Updates ? "true" : "false")});");
        }
        source.AppendLine("        }");
        source.AppendLine("    }");
        source.AppendLine("}");

        foreach (var r in sorted.Where(static r => r.Partial))
        {
            var marker = r.Kind switch
            {
                Kind.Component => "IOwnComponent",
                Kind.Resource => "IOwnResource",
                Kind.Message => "INetSerializable",
                _ => "IMirrorComponent",
            };
            Part(source, r.Namespace, $"partial {(r.Record ? "record" : "class")} {r.Name} : global::Rs13.Packs.{marker} {{ }}");
        }
        foreach (var s in systems.Where(static s => s.Partial))
        {
            var body = new StringBuilder();
            body.AppendLine($"partial class {s.Name}");
            body.AppendLine("{");
            body.AppendLine("    protected override void InjectDependencies(global::Rs13.Packs.SystemSet systems)");
            body.AppendLine("    {");
            body.AppendLine("        base.InjectDependencies(systems);");
            foreach (var (field, type, query) in s.Dependencies)
            {
                body.AppendLine(query ? $"        {field} = new {type}();" : $"        {field} = systems.Get<{type}>();");
            }
            body.AppendLine("    }");
            body.Append('}');
            Part(source, s.Namespace, body.ToString());
        }
        spc.AddSource("ModEntry.g.cs", source.ToString());
    }

    static void Contexts(StringBuilder source, ImmutableArray<Registration> sorted)
    {
        if (sorted.Any(static r => r.Kind != Kind.Mirror))
        {
            source.AppendLine("            b.Json = global::Rs13.Packs.ModJson.Default;");
        }
        if (sorted.Any(static r => r.Kind == Kind.Mirror))
        {
            source.AppendLine("            b.Mirrors = global::Rs13.Packs.MirrorJson.Default;");
        }
    }

    /// <summary>A declaration in the type's own namespace, so the partial joins it</summary>
    static void Part(StringBuilder source, string? ns, string declaration)
    {
        source.AppendLine();
        if (ns is null)
        {
            source.AppendLine(declaration);
            return;
        }
        source.AppendLine($"namespace {ns}");
        source.AppendLine("{");
        foreach (var line in declaration.Split('\n'))
        {
            source.AppendLine("    " + line.TrimEnd('\r'));
        }
        source.AppendLine("}");
    }

    static bool Lists(INamedTypeSymbol? context, string metadata)
    {
        if (context is null)
        {
            return false;
        }
        foreach (var attribute in context.GetAttributes())
        {
            if (attribute.AttributeClass?.Name == "JsonSerializableAttribute"
                && attribute.ConstructorArguments.Length == 1
                && attribute.ConstructorArguments[0].Value is INamedTypeSymbol listed
                && listed.ToDisplayString(MetadataFormat) == metadata)
            {
                return true;
            }
        }
        return false;
    }

    static readonly SymbolDisplayFormat MetadataFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

    static bool Derives(INamedTypeSymbol symbol, string baseName)
    {
        for (var type = symbol.BaseType; type is not null; type = type.BaseType)
        {
            if (type.ToDisplayString() == baseName)
            {
                return true;
            }
        }
        return false;
    }

    static bool IsPartial(INamedTypeSymbol symbol) =>
        symbol.DeclaringSyntaxReferences.Any(static r => r.GetSyntax() is TypeDeclarationSyntax t && t.Modifiers.Any(SyntaxKind.PartialKeyword));

    static string? NamespaceOf(INamedTypeSymbol symbol) =>
        symbol.ContainingNamespace.IsGlobalNamespace ? null : symbol.ContainingNamespace.ToDisplayString();

    /// <summary>A partial type is one declaration, taken on its first part</summary>
    static bool FirstPart(INamedTypeSymbol symbol, SyntaxNode node) =>
        symbol.DeclaringSyntaxReferences.Length <= 1 || symbol.DeclaringSyntaxReferences[0].GetSyntax() == node;

    enum Kind
    {
        Component,
        Resource,
        Message,
        Mirror,
    }

    sealed record Registration(
        Kind Kind,
        string Type,
        string Metadata,
        string Name,
        string? Namespace,
        string Path,
        bool Networked,
        bool Partial,
        bool Record,
        bool IsComponent,
        string Attribute,
        Location Location,
        ImmutableArray<Diagnostic> Problems)
    {
        public static Registration? Of(GeneratorSyntaxContext ctx)
        {
            if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) is not INamedTypeSymbol symbol || symbol.IsValueType || !FirstPart(symbol, ctx.Node))
            {
                return null;
            }
            Kind? kind = null;
            var networked = false;
            string? mirror = null;
            foreach (var attribute in symbol.GetAttributes())
            {
                switch (attribute.AttributeClass?.ToDisplayString())
                {
                    case Sdk + "RegisterComponentAttribute":
                        kind = Kind.Component;
                        break;
                    case Sdk + "RegisterResourceAttribute":
                        kind = Kind.Resource;
                        break;
                    case Sdk + "NetSerializableAttribute":
                        kind = Kind.Message;
                        break;
                    case Sdk + "NetworkedComponentAttribute":
                        networked = true;
                        break;
                    case Sdk + "MirrorComponentAttribute":
                        kind = Kind.Mirror;
                        mirror = attribute.ConstructorArguments[0].Value as string;
                        break;
                }
            }
            if (kind is not { } found)
            {
                return null;
            }
            var name = symbol.Name;
            var stripped = name.Length > "Component".Length && name.EndsWith("Component", StringComparison.Ordinal)
                ? name.Substring(0, name.Length - "Component".Length)
                : name;
            var path = mirror ?? $"{symbol.ContainingAssembly.Name}::{stripped}";
            var attributeName = found switch
            {
                Kind.Component => "RegisterComponent",
                Kind.Resource => "RegisterResource",
                Kind.Message => "NetSerializable",
                _ => "MirrorComponent(\"...\")",
            };
            var location = ctx.Node.GetLocation();
            var problems = ImmutableArray.CreateBuilder<Diagnostic>();
            if (symbol.ContainingType is { } outer)
            {
                problems.Add(Diagnostic.Create(Nested, location, name, outer.Name));
            }
            if (found != Kind.Mirror)
            {
                foreach (var member in symbol.GetMembers())
                {
                    if (Unstored(member) is { } why)
                    {
                        problems.Add(Diagnostic.Create(NotStored, member.Locations.FirstOrDefault() ?? location, name + ".", member.Name, why));
                    }
                }
            }
            return new Registration(
                found,
                symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                symbol.ToDisplayString(MetadataFormat),
                name,
                NamespaceOf(symbol),
                path,
                networked,
                IsPartial(symbol),
                symbol.IsRecord,
                Derives(symbol, Sdk + "Component"),
                attributeName,
                location,
                problems.ToImmutable());
        }

        // Why STJ would not round-trip the member, or null when it does
        static string? Unstored(ISymbol member)
        {
            if (member.IsStatic || member.IsImplicitlyDeclared)
            {
                return null;
            }
            var marked = member.GetAttributes().Any(static a => a.AttributeClass?.ToDisplayString() == Sdk + "DataFieldAttribute");
            switch (member)
            {
                case IFieldSymbol field:
                    if (field.AssociatedSymbol is not null)
                    {
                        return null;
                    }
                    if (!marked)
                    {
                        return "lacks [DataField]";
                    }
                    if (field.DeclaredAccessibility != Accessibility.Public)
                    {
                        return "is not public";
                    }
                    if (field.IsReadOnly)
                    {
                        return "is readonly";
                    }
                    return null;
                case IPropertySymbol property:
                    if (property.IsIndexer)
                    {
                        return "is an indexer";
                    }
                    if (!marked)
                    {
                        return "lacks [DataField]";
                    }
                    if (property.DeclaredAccessibility != Accessibility.Public)
                    {
                        return "is not public";
                    }
                    if (property.GetMethod?.DeclaredAccessibility != Accessibility.Public || property.SetMethod?.DeclaredAccessibility != Accessibility.Public)
                    {
                        return "needs a public getter and setter";
                    }
                    return null;
                default:
                    return null;
            }
        }
    }

    /// <summary>A system of the module, with the fields the generator fills</summary>
    sealed record SystemInfo(string Type, string Name, string? Namespace, bool Partial, bool Updates, ImmutableArray<(string Field, string Type, bool Query)> Dependencies);

    /// <summary>A class with a base list: a system, or a Component subclass that forgot its attribute</summary>
    sealed record Derived(SystemInfo? System, ImmutableArray<Diagnostic> Problems)
    {
        public static Derived? Of(GeneratorSyntaxContext ctx)
        {
            if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) is not INamedTypeSymbol symbol || !FirstPart(symbol, ctx.Node))
            {
                return null;
            }
            var location = ctx.Node.GetLocation();
            var problems = ImmutableArray.CreateBuilder<Diagnostic>();
            if (Derives(symbol, Sdk + "Component"))
            {
                if (!symbol.GetAttributes().Any(static a => a.AttributeClass?.ToDisplayString() is Sdk + "RegisterComponentAttribute" or Sdk + "MirrorComponentAttribute"))
                {
                    problems.Add(Diagnostic.Create(NotStored, location, symbol.Name, "", "derives from Component without [RegisterComponent] or [MirrorComponent]"));
                }
                return new Derived(null, problems.ToImmutable());
            }
            if (!Derives(symbol, Sdk + "EntitySystem") || symbol.IsAbstract)
            {
                return null;
            }
            var partial = IsPartial(symbol);
            if (!partial)
            {
                problems.Add(Diagnostic.Create(SystemShape, location, symbol.Name, "is not partial"));
            }
            if (symbol.InstanceConstructors.Any(static c => !c.IsImplicitlyDeclared && c.Parameters.Length > 0))
            {
                problems.Add(Diagnostic.Create(SystemShape, location, symbol.Name, "takes constructor parameters"));
            }
            // A base system's fields are the derived one's, so the chain up to EntitySystem is walked
            var dependencies = ImmutableArray.CreateBuilder<(string, string, bool)>();
            var updates = false;
            for (var type = symbol; type is not null && type.ToDisplayString() != Sdk + "EntitySystem"; type = type.BaseType)
            {
                var inherited = !SymbolEqualityComparer.Default.Equals(type, symbol);
                foreach (var member in type.GetMembers())
                {
                    if (member.IsImplicitlyDeclared)
                    {
                        continue;
                    }
                    var where = member.Locations.FirstOrDefault() ?? location;
                    if (member is IPropertySymbol { IsStatic: false, SetMethod: not null } property)
                    {
                        problems.Add(Diagnostic.Create(SystemState, where, type.Name, property.Name));
                        continue;
                    }
                    if (member is IMethodSymbol { Name: "Update", IsOverride: true, Parameters.Length: 1 })
                    {
                        updates = true;
                        continue;
                    }
                    if (member is not IFieldSymbol field || field.AssociatedSymbol is not null)
                    {
                        continue;
                    }
                    if (field.IsStatic)
                    {
                        if (!field.IsConst && !field.IsReadOnly)
                        {
                            problems.Add(Diagnostic.Create(SystemState, where, type.Name, field.Name));
                        }
                        continue;
                    }
                    if (!field.GetAttributes().Any(static a => a.AttributeClass?.ToDisplayString() == Sdk + "DependencyAttribute"))
                    {
                        problems.Add(Diagnostic.Create(SystemState, where, type.Name, field.Name));
                        continue;
                    }
                    if (field.IsReadOnly)
                    {
                        problems.Add(Diagnostic.Create(DependencyShape, where, type.Name, field.Name, "is readonly"));
                        continue;
                    }
                    if (inherited && field.DeclaredAccessibility == Accessibility.Private)
                    {
                        problems.Add(Diagnostic.Create(DependencyShape, where, type.Name, field.Name, "is private on a base system, which the derived system fills"));
                        continue;
                    }
                    if (field.Type is not INamedTypeSymbol target || Injectable(target) is not { } query)
                    {
                        problems.Add(Diagnostic.Create(DependencyShape, where, type.Name, field.Name, $"is a {field.Type.Name}, not a system, a built-in or an EntityQuery<T>"));
                        continue;
                    }
                    dependencies.Add((field.Name, target.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), query));
                }
            }
            // A system or a built-in comes from the set; an EntityQuery<T> is constructed, which is what the flag says
            static bool? Injectable(INamedTypeSymbol target)
            {
                if (target.IsGenericType && target.ConstructedFrom.ToDisplayString() == Sdk + "EntityQuery<T>")
                {
                    return true;
                }
                if (target.AllInterfaces.Any(static i => i.ToDisplayString() == Sdk + "IBuiltIn"))
                {
                    // The SDK's public built-ins are the ones the set holds; its internal implementations and a module's own are not
                    return target.DeclaredAccessibility == Accessibility.Public && target.ContainingNamespace.ToDisplayString() == "Rs13.Packs" ? false : null;
                }
                return Derives(target, Sdk + "EntitySystem") && !target.IsAbstract ? false : null;
            }
            var system = new SystemInfo(
                symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                symbol.Name,
                NamespaceOf(symbol),
                partial,
                updates,
                dependencies.ToImmutable());
            return new Derived(system, problems.ToImmutable());
        }
    }
}
