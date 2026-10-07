namespace NeoGameLib.NeoInput;

/// <summary>
/// maps raw keyboard axis values mapped to a value range, 0 to 1, to -1, etc.
/// </summary>
/// <remarks>
/// I've finally found out how to make comments that can be picked up by intellisense. yayyyyyyy
/// </remarks>
public enum NeoAxisRange
{
    /// <summary>
    /// 0 -> 1
    /// </summary>
    Positive,
    /// <summary>
    /// 0 -> -1
    /// </summary>
    Negative,
    /// <summary>
    /// -1 -> 1
    /// </summary>
    Full,
    /// <summary>
    /// 1 -> 0
    /// </summary>
    PositiveInverted,
    /// <summary>
    /// -1 -> 0
    /// </summary>
    NegativeInverted,
    /// <summary>
    /// 1 -> -1
    /// </summary>
    FullInverted
}
