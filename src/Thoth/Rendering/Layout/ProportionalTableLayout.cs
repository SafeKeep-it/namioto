namespace Thoth.Rendering.Layout;

public static class ProportionalTableLayout
{
    public static int[] ComputeColumnWidths(int totalWidth, IReadOnlyList<int> weights)
    {
        var widths = new int[Math.Max(0, weights.Count)];
        if (totalWidth <= 0 || widths.Length == 0) return widths;

        var normalized = new int[widths.Length];
        var sum = 0;
        for (var i = 0; i < widths.Length; i++)
        {
            var weight = weights[i];
            if (weight <= 0) weight = 1;
            normalized[i] = weight;
            sum += weight;
        }

        FillWidths(totalWidth, widths, normalized, sum);
        return widths;
    }

    public static int[] ComputeColumnWidths(int totalWidth, ReadOnlySpan<int> weights)
    {
        var widths = new int[Math.Max(0, weights.Length)];
        if (totalWidth <= 0 || widths.Length == 0) return widths;

        var normalized = new int[widths.Length];
        var sum = 0;
        for (var i = 0; i < widths.Length; i++)
        {
            var weight = weights[i];
            if (weight <= 0) weight = 1;
            normalized[i] = weight;
            sum += weight;
        }

        FillWidths(totalWidth, widths, normalized, sum);
        return widths;
    }

    static void FillWidths(int totalWidth, int[] widths, int[] normalized, int sum)
    {
        var remainders = new double[widths.Length];
        var allocated = 0;
        for (var i = 0; i < widths.Length; i++)
        {
            var exact = (double)totalWidth * normalized[i] / sum;
            var floor = (int)Math.Floor(exact);
            widths[i] = floor;
            remainders[i] = exact - floor;
            allocated += floor;
        }

        var remaining = totalWidth - allocated;
        while (remaining > 0)
        {
            var pick = 0;
            var best = double.MinValue;

            for (var i = 0; i < remainders.Length; i++)
            {
                if (remainders[i] <= best) continue;
                best = remainders[i];
                pick = i;
            }

            widths[pick]++;
            remainders[pick] = double.NegativeInfinity;
            remaining--;
        }
    }
}
