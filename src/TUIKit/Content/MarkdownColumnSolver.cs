namespace TUIKit.Content
{
    using System;

    // Chooses column widths from content: natural widths when they fit, otherwise minimums plus the remaining
    // space given to the smallest shortfalls first, so short columns stay whole. Returns null when the minimums
    // do not fit.
    internal static class MarkdownColumnSolver
    {
        internal static int[]? Solve(int[] natural, int[] minimum, int budget, bool expand)
        {
            int columns = natural.Length;
            int naturalSum = Sum(natural);
            if (naturalSum <= budget)
            {
                int[] widths = (int[])natural.Clone();
                if (expand && naturalSum < budget)
                {
                    int[] extra = Share(budget - naturalSum, natural);
                    for (int c = 0; c < columns; c++)
                        widths[c] += extra[c];
                }

                return widths;
            }

            int minimumSum = Sum(minimum);
            if (minimumSum > budget)
                return null;

            int[] shortfall = new int[columns];
            for (int c = 0; c < columns; c++)
                shortfall[c] = Math.Max(0, natural[c] - minimum[c]);

            int[] share = FairShare(budget - minimumSum, shortfall, natural);
            int[] result = new int[columns];
            for (int c = 0; c < columns; c++)
                result[c] = minimum[c] + share[c];

            return result;
        }

        // Gives spare columns to the columns that want the least first, each in full while space lasts, so short
        // columns (keys, numbers, status words) stay on one line and the widest prose column absorbs the squeeze.
        // Ties go to the narrower column, then to column order.
        internal static int[] FairShare(int amount, int[] wants, int[] natural)
        {
            int columns = wants.Length;
            int[] given = new int[columns];
            int[] order = new int[columns];
            for (int c = 0; c < columns; c++)
                order[c] = c;

            Array.Sort(order, (a, b) =>
            {
                if (wants[a] != wants[b])
                    return wants[a].CompareTo(wants[b]);
                if (natural[a] != natural[b])
                    return natural[a].CompareTo(natural[b]);

                return a.CompareTo(b);
            });
            int left = amount;
            for (int k = 0; k < columns && left > 0; k++)
            {
                int c = order[k];
                int take = Math.Min(Math.Max(0, wants[c]), left);
                given[c] = take;
                left -= take;
            }

            return given;
        }

        // Splits the budget evenly with a floor of one column each, for the Shrink fallback.
        internal static int[] Even(int columns, int budget)
        {
            int[] widths = new int[columns];
            int usable = Math.Max(columns, budget);
            int each = usable / columns;
            int remainder = usable % columns;
            for (int c = 0; c < columns; c++)
                widths[c] = Math.Max(1, each + (c < remainder ? 1 : 0));

            return widths;
        }

        // Distributes 'amount' across columns in proportion to 'weights' by largest remainder. Zero weights share
        // evenly.
        internal static int[] Share(int amount, int[] weights)
        {
            int columns = weights.Length;
            int[] result = new int[columns];
            if (amount <= 0 || columns == 0)
                return result;

            long total = 0;
            for (int c = 0; c < columns; c++)
                total += Math.Max(0, weights[c]);

            if (total == 0)
            {
                for (int c = 0; c < columns; c++)
                    result[c] = amount / columns + (c < amount % columns ? 1 : 0);

                return result;
            }

            long[] remainders = new long[columns];
            int given = 0;
            for (int c = 0; c < columns; c++)
            {
                long numerator = (long)amount * Math.Max(0, weights[c]);
                result[c] = (int)(numerator / total);
                remainders[c] = numerator % total;
                given += result[c];
            }

            int left = amount - given;
            while (left > 0)
            {
                int best = -1;
                for (int c = 0; c < columns; c++)
                {
                    if (weights[c] <= 0)
                        continue;
                    if (best < 0 || remainders[c] > remainders[best])
                        best = c;
                }

                if (best < 0)
                    break;

                result[best]++;
                remainders[best] = -1;
                left--;
            }

            return result;
        }

        private static int Sum(int[] values)
        {
            int total = 0;
            for (int i = 0; i < values.Length; i++)
                total += values[i];

            return total;
        }
    }
}
