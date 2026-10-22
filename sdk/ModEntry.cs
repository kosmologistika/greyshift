namespace Rs13.Packs;

/// <summary>
/// What the SDK generator writes for a module: the partial methods have an
/// access modifier, so a module built without the generator does not compile
/// </summary>
static partial class ModEntry
{
    /// <summary>The module's own types and its systems</summary>
    internal static partial void Build(ModBuilder b);
}
