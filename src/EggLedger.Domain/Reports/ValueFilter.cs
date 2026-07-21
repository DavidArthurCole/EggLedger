namespace EggLedger.Domain.Reports;

public static class ValueFilter {
    public static ReportResult Apply(ReportResult result, string op, double threshold) {
        if (string.IsNullOrEmpty(op) || result.Is2D) {
            return result;
        }

        Func<double, bool> keep = op switch {
            ">" => v => v > threshold,
            "<" => v => v < threshold,
            ">=" => v => v >= threshold,
            "<=" => v => v <= threshold,
            "=" => v => v == threshold,
            "!=" => v => v != threshold,
            _ => _ => true,
        };

        var labels = new List<string>();
        if (result.IsFloat) {
            var floats = new List<double>();
            for (int i = 0; i < result.Labels.Count; i++) {
                double v = i < result.FloatValues.Count ? result.FloatValues[i] : 0;
                if (keep(v)) {
                    labels.Add(result.Labels[i]);
                    floats.Add(v);
                }
            }
            return new ReportResult { Labels = labels, FloatValues = floats, IsFloat = true, Weight = result.Weight };
        }

        var values = new List<long>();
        for (int i = 0; i < result.Labels.Count; i++) {
            long v = i < result.Values.Count ? result.Values[i] : 0;
            if (keep(v)) {
                labels.Add(result.Labels[i]);
                values.Add(v);
            }
        }
        return new ReportResult { Labels = labels, Values = values, IsFloat = false, Weight = result.Weight };
    }
}
