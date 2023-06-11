namespace GedcomParser
{
  internal class ReportStyle
  {
    public string FontName { get; set; } = "Verdana";
    public double BaseFontSize { get; set; } = 16;

    public static ReportStyle Default { get; } = new ReportStyle();
  }
}
