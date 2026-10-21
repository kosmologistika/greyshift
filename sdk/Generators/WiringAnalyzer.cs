using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Rs13.Mods.Generators;

/// <summary>
/// What a system does with the world that would only fail at setup or in a
/// handler: an edge subscription the host does not relay, and world calls
/// made from <c>Initialize</c>, which runs with no world
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WiringAnalyzer : DiagnosticAnalyzer
{
    const string Sdk = "Rs13.Mods.";

    static readonly DiagnosticDescriptor InitializeWorld = new(
        "RS13008",
        "Initialize does not only subscribe",
        "{0}.Initialize {1}",
        "Rs13.Mods",
        DiagnosticSeverity.Warning,
        true);

    static readonly DiagnosticDescriptor NoEdge = new(
        "RS13010",
        "An edge the host does not relay",
        "{0} cannot be observed being {1}: the host relays only the edges typed.wit grants, and an own component has none",
        "Rs13.Mods",
        DiagnosticSeverity.Error,
        true);

    // EntitySystem's members that reach the host, so a handler may call them and Initialize may not
    static readonly HashSet<string> WorldCalls =
    [
        "HasComp", "TryComp", "Comp", "CompOrNull", "EnsureComp", "AddComp", "RemComp", "Resource",
        "Spawn", "TrySpawn", "Dirty", "QueueDel", "RaiseLocalEvent", "RaiseNetworkEvent", "Now", "Log", "EntityQueryEnumerator",
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(InitializeWorld, NoEdge);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterOperationAction(Invocation, OperationKind.Invocation);
        context.RegisterOperationAction(Property, OperationKind.PropertyReference);
        context.RegisterOperationBlockAction(InitializeBody);
    }

    static void Invocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        var owner = method.ContainingType;
        if (owner.ToDisplayString() == Sdk + "EntitySystem" && method.Name == "SubscribeLocalEvent" && method.TypeArguments.Length == 2)
        {
            Edge(context, invocation, method.TypeArguments[0], method.TypeArguments[1]);
        }
        if (ReachesTheWorld(owner, method.Name) && InInitialize(context.ContainingSymbol, invocation) is { } system)
        {
            context.ReportDiagnostic(Diagnostic.Create(InitializeWorld, invocation.Syntax.GetLocation(), system, $"calls {Named(owner, method.Name)}: Initialize runs inside setup with no world, so it only subscribes"));
        }
    }

    static void Property(OperationAnalysisContext context)
    {
        var reference = (IPropertyReferenceOperation)context.Operation;
        var owner = reference.Property.ContainingType;
        if (ReachesTheWorld(owner, reference.Property.Name) && InInitialize(context.ContainingSymbol, reference) is { } system)
        {
            context.ReportDiagnostic(Diagnostic.Create(InitializeWorld, reference.Syntax.GetLocation(), system, $"reads {Named(owner, reference.Property.Name)}: Initialize runs inside setup with no world, so it only subscribes"));
        }
    }

    /// <summary>A world member of EntitySystem, or any member of a built-in or an EntityQuery, which only answer in a handler</summary>
    static bool ReachesTheWorld(INamedTypeSymbol owner, string member)
    {
        if (owner.ToDisplayString() == Sdk + "EntitySystem")
        {
            return WorldCalls.Contains(member);
        }
        return owner.IsGenericType && owner.ConstructedFrom.ToDisplayString() == Sdk + "EntityQuery<T>"
            || owner.AllInterfaces.Any(static i => i.ToDisplayString() == Sdk + "IBuiltIn");
    }

    static string Named(INamedTypeSymbol owner, string member) =>
        owner.ToDisplayString() == Sdk + "EntitySystem" ? member : $"{owner.Name}.{member}";

    static void Edge(OperationAnalysisContext context, IInvocationOperation invocation, ITypeSymbol component, ITypeSymbol @event)
    {
        var (marker, verb) = @event.ToDisplayString() switch
        {
            Sdk + "ComponentAdd" => ("IAddObservable", "added"),
            Sdk + "ComponentRemove" => ("IRemoveObservable", "removed"),
            _ => (null, null),
        };
        if (marker is null || Implements(component, Sdk + marker))
        {
            return;
        }
        context.ReportDiagnostic(Diagnostic.Create(NoEdge, invocation.Syntax.GetLocation(), component.Name, verb));
    }

    /// <summary>A base system's Initialize subscribes too, so an override that skips it loses those subscriptions</summary>
    static void InitializeBody(OperationBlockAnalysisContext context)
    {
        if (context.OwningSymbol is not IMethodSymbol { Name: "Initialize", IsOverride: true, Parameters.Length: 0 } method
            || !Derives(method.ContainingType, Sdk + "EntitySystem")
            || method.OverriddenMethod is not { IsAbstract: false } overridden
            || overridden.ContainingType.ToDisplayString() == Sdk + "EntitySystem")
        {
            return;
        }
        var calls = context.OperationBlocks
            .SelectMany(static block => block.DescendantsAndSelf())
            .OfType<IInvocationOperation>()
            .Any(call => SymbolEqualityComparer.Default.Equals(call.TargetMethod, overridden));
        if (!calls)
        {
            var location = method.Locations.FirstOrDefault() ?? Location.None;
            context.ReportDiagnostic(Diagnostic.Create(InitializeWorld, location, method.ContainingType.Name, $"does not call base.Initialize(), so {overridden.ContainingType.Name}'s subscriptions are lost"));
        }
    }

    /// <summary>The system whose Initialize runs the operation, or null outside one. A lambda or local function written there runs later, as a handler</summary>
    static string? InInitialize(ISymbol containing, IOperation operation)
    {
        if (containing is not IMethodSymbol { Name: "Initialize", IsOverride: true, Parameters.Length: 0 } method
            || !Derives(method.ContainingType, Sdk + "EntitySystem"))
        {
            return null;
        }
        for (var parent = operation.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is IAnonymousFunctionOperation or ILocalFunctionOperation)
            {
                return null;
            }
        }
        return method.ContainingType.Name;
    }

    /// <summary>A type parameter carries the marker through its constraints</summary>
    static bool Implements(ITypeSymbol type, string marker) =>
        type is ITypeParameterSymbol parameter
            ? parameter.ConstraintTypes.Any(c => c.ToDisplayString() == marker || Implements(c, marker))
            : type.AllInterfaces.Any(i => i.ToDisplayString() == marker);

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
}
