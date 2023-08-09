namespace GedcomParser
{
  internal class ReportStyle
  {
    public double PageWidthInches { get; set; } = 7.25;
    public string FontName { get; set; } = "Calibri";
    public double BaseFontSize { get; set; } = 12;
    public string[] Colors { get; set; } = new[] { "rgb(136,174,225)", "rgb(11,69,18)", "rgb(206,59,246)", "rgb(115,174,34)", "rgb(63,33,153)", "rgb(55,213,26)", "rgb(196,39,117)", "rgb(69,222,178)", "rgb(101,34,75)", "rgb(191,203,170)", "rgb(19,63,89)" };

    public static ReportStyle Default { get; } = new ReportStyle();
  }
}
