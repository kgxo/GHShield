using Grasshopper.Kernel;

namespace GHShield.Hooks
{
    /// <summary>
    /// Superseded by AdapterHook, which installs protection adapters for every
    /// interactive object type rather than the Graph Mapper alone.
    ///
    /// Kept as a forwarder so older call sites keep working.
    /// </summary>
    public static class GraphMapperHook
    {
        public static void Attach(GH_Document document)
        {
            AdapterHook.Attach(document);
        }
    }
}
