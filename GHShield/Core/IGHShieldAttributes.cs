namespace GHShield.Core
{
    /// <summary>
    /// Marker for any attributes class GHShield has swapped onto an object.
    ///
    /// Lets the adapter hook tell "already protected" from "still stock"
    /// without knowing which of the specific adapter types it is looking at.
    /// </summary>
    public interface IGHShieldAttributes
    {
    }
}
