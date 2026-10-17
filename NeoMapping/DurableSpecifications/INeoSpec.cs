using NeoGameLib.NeoGO;

namespace NeoGameLib.NeoMapping.DurableSpecifications;

/// <summary>
/// Interface that marks spec's authoring data applying to a NeoObject as an actual NeoComponent when a map is built.
/// </summary>
public interface INeoSpec
{
    /// <summary>
    /// Adds this spec's components to the target object. This is fired during map Build so buses and Assets are available.
    /// </summary>
    void Apply(NeoObject target);
}
