using TypedModuleWorld.wit.Imports.rs13.mods.v0_1_0;
using static TypedModuleWorld.wit.Imports.rs13.mods.v0_1_0.ITypedEcsImports;

namespace TypedModuleWorld;

public class TypedModuleWorldExportsImpl : ITypedModuleWorldExports
{
    public static void Setup()
    {
        var builder = new Rs13.Mods.ModBuilder();
        Rs13.Mods.ModEntry.Build(builder);
        Rs13.Mods.ModRuntime.Start(builder);
    }

    public static void Dispatch(Event @event) => Rs13.Mods.ModRuntime.Dispatch(@event);

    public static void DispatchTimer(string name) => Rs13.Mods.ModRuntime.DispatchTimer(name);

    public static void DispatchClient(string kind, ulong from, byte[] value) => Rs13.Mods.ModRuntime.DispatchClient(kind, from, value);
}
