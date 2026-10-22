using TypedModuleWorld.wit.Imports.rs13.mods.v0_1_0;
using static TypedModuleWorld.wit.Imports.rs13.mods.v0_1_0.ITypedEcsImports;

namespace TypedModuleWorld;

public class TypedModuleWorldExportsImpl : ITypedModuleWorldExports
{
    public static void Setup()
    {
        var builder = new Rs13.Packs.ModBuilder();
        Rs13.Packs.ModEntry.Build(builder);
        Rs13.Packs.ModRuntime.Start(builder);
    }

    public static void Dispatch(Event @event) => Rs13.Packs.ModRuntime.Dispatch(@event);

    public static void DispatchTimer(string name) => Rs13.Packs.ModRuntime.DispatchTimer(name);

    public static void DispatchClient(string kind, ulong from, byte[] value) => Rs13.Packs.ModRuntime.DispatchClient(kind, from, value);
}
