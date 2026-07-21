using System.Globalization;

namespace EggLedger.Domain.Export.Xlsx;

public enum XlsxStyle {
    None = 0,

    Datetime = 1,
}

public readonly struct XlsxCell {
    public string StrVal { get; }
    public double NumVal { get; }
    public XlsxStyle Style { get; }
    public bool IsNum { get; }

    private XlsxCell(string strVal, double numVal, XlsxStyle style, bool isNum) {
        StrVal = strVal;
        NumVal = numVal;
        Style = style;
        IsNum = isNum;
    }

    public static XlsxCell String(string s) => new(s, 0, XlsxStyle.None, false);

    public static XlsxCell Number(double v) => new("", v, XlsxStyle.None, true);

    public static XlsxCell Datetime(DateTimeOffset t) =>
        new("", ExcelSerial(t), XlsxStyle.Datetime, true);

    public static string CellRef(int col, int row) => ColName(col) + row.ToString(CultureInfo.InvariantCulture);

    public static string ColName(int col) {
        var name = "";
        while (col > 0) {
            col--;
            name = (char)('A' + col % 26) + name;
            col /= 26;
        }
        return name;
    }

    public static double ExcelSerial(DateTimeOffset t) {
        var epoch = new DateTimeOffset(1899, 12, 30, 0, 0, 0, TimeSpan.Zero);
        return (t.ToUniversalTime() - epoch).TotalHours / 24.0;
    }
}
