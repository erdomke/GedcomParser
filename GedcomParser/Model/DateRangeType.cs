namespace GedcomParser
{
  public enum DateRangeType
  {
    Unknown,
    /// <summary>
    /// The precise date at which a singular event occurred.
    /// </summary>
    Date,
    /// <summary>
    /// The possible range within which a singular event/instance occurred.
    /// </summary>
    Range,
    /// <summary>
    /// The period/span over which a long-running event occurred.
    /// </summary>
    Period
  }
}
