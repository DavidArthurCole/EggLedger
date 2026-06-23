using System.Globalization;

namespace EggLedger.Domain.Export.Xlsx;

/// <summary>Pre-defined number format style. Port of Go xlsxwriter StyleID.</summary>
public enum XlsxStyle
{
    /// <summary>Default, no number format.</summary>
    None = 0,

    /// <summary>yyyy-mm-dd hh:mm:ss.</summary>
    Datetime = 1,
}

/// <summary>
/// A single spreadsheet value. Port of Go xlsxwriter.Cell. Construct via the
/// static factory methods.
/// </summary>
public readonly struct XlsxCell
{
    public string StrVal { get; }
    public double NumVal { get; }
    public XlsxStyle Style { get; }
    public bool IsNum { get; }

    private XlsxCell(string strVal, double numVal, XlsxStyle style, bool isNum)
    {
        StrVal = strVal;
        NumVal = numVal;
        Style = style;
        IsNum = isNum;
    }

    /// <summary>Plain text cell.</summary>
    public static XlsxCell String(string s) => new(s, 0, XlsxStyle.None, false);

    /// <summary>Plain numeric cell with no number format applied.</summary>
    public static XlsxCell Number(double v) => new("", v, XlsxStyle.None, true);

    /// <summary>
    /// Numeric cell formatted as yyyy-mm-dd hh:mm:ss. Converted to an Excel 1900
    /// date serial (days since Dec 30, 1899 UTC).
    /// </summary>
    public static XlsxCell Datetime(DateTimeOffset t) =>
        new("", ExcelSerial(t), XlsxStyle.Datetime, true);

    /// <summary>1-based (col,row) to a cell reference like "A1".</summary>
    public static string CellRef(int col, int row) => ColName(col) + row.ToString(CultureInfo.InvariantCulture);

    /// <summary>1-based column index to letters ("A".."Z","AA",...).</summary>
    public static string ColName(int col)
    {
        var name = "";
        while (col > 0)
        {
            col--;
            name = (char)('A' + col % 26) + name;
            col /= 26;
        }
        return name;
    }

    /// <summary>
    /// Time to Excel 1900 date serial. Day 1 = Jan 1, 1900; epoch is Dec 30,
    /// 1899 UTC. Mirrors Go excelSerial: t.UTC().Sub(epoch).Hours()/24.
    /// </summary>
    public static double ExcelSerial(DateTimeOffset t)
    {
        var epoch = new DateTimeOffset(1899, 12, 30, 0, 0, 0, TimeSpan.Zero);
        return (t.ToUniversalTime() - epoch).TotalHours / 24.0;
    }
}
