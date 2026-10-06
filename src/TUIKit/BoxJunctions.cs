namespace TUIKit
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;

    internal static class BoxJunctions
    {
        // Each entry: code point, then the weight of the up, down, left, and right arms
        // (0 none, 1 light, 2 heavy, 3 double). Covers every light/heavy combination in U+2500-U+254B,
        // the half and mixed straight lines in U+2574-U+257F, and the single/double set in U+2550-U+256C.
        private const string Table =
            "2500:0011 2501:0022 2502:1100 2503:2200 " +
            "250C:0101 250D:0102 250E:0201 250F:0202 2510:0110 2511:0120 2512:0210 2513:0220 " +
            "2514:1001 2515:1002 2516:2001 2517:2002 2518:1010 2519:1020 251A:2010 251B:2020 " +
            "251C:1101 251D:1102 251E:2101 251F:1201 2520:2201 2521:2102 2522:1202 2523:2202 " +
            "2524:1110 2525:1120 2526:2110 2527:1210 2528:2210 2529:2120 252A:1220 252B:2220 " +
            "252C:0111 252D:0121 252E:0112 252F:0122 2530:0211 2531:0221 2532:0212 2533:0222 " +
            "2534:1011 2535:1021 2536:1012 2537:1022 2538:2011 2539:2021 253A:2012 253B:2022 " +
            "253C:1111 253D:1121 253E:1112 253F:1122 2540:2111 2541:1211 2542:2211 2543:2121 " +
            "2544:2112 2545:1221 2546:1212 2547:2122 2548:1222 2549:2221 254A:2212 254B:2222 " +
            "2574:0010 2575:1000 2576:0001 2577:0100 2578:0020 2579:2000 257A:0002 257B:0200 " +
            "257C:0012 257D:1200 257E:0021 257F:2100 " +
            "2550:0033 2551:3300 2552:0103 2553:0301 2554:0303 2555:0130 2556:0310 2557:0330 " +
            "2558:1003 2559:3001 255A:3003 255B:1030 255C:3010 255D:3030 255E:1103 255F:3301 " +
            "2560:3303 2561:1130 2562:3310 2563:3330 2564:0133 2565:0311 2566:0333 2567:1033 " +
            "2568:3011 2569:3033 256A:1133 256B:3311 256C:3333";

        private static readonly Dictionary<int, string> _GlyphByArms = new Dictionary<int, string>();
        private static readonly Dictionary<string, int> _ArmsByGlyph = new Dictionary<string, int>(StringComparer.Ordinal);

        static BoxJunctions()
        {
            string[] entries = Table.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string entry in entries)
            {
                int codePoint = int.Parse(entry.Substring(0, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                int arms = Pack(entry[5] - '0', entry[6] - '0', entry[7] - '0', entry[8] - '0');
                string glyph = char.ConvertFromUtf32(codePoint);
                _GlyphByArms[arms] = glyph;
                _ArmsByGlyph[glyph] = arms;
            }

            // Rounded corners read as light corners; they are produced only by RoundedCorner.
            _ArmsByGlyph["╭"] = Pack(0, 1, 0, 1);
            _ArmsByGlyph["╮"] = Pack(0, 1, 1, 0);
            _ArmsByGlyph["╯"] = Pack(1, 0, 1, 0);
            _ArmsByGlyph["╰"] = Pack(1, 0, 0, 1);
        }

        internal static int Pack(int up, int down, int left, int right)
        {
            return (up << 6) | (down << 4) | (left << 2) | right;
        }

        internal static int Arm(int arms, int shift)
        {
            return (arms >> shift) & 3;
        }

        internal static bool TryGetArms(string? glyph, out int arms)
        {
            arms = 0;
            return glyph != null && _ArmsByGlyph.TryGetValue(glyph, out arms);
        }

        internal static string? Glyph(int arms)
        {
            return _GlyphByArms.TryGetValue(arms, out string? glyph) ? glyph : null;
        }

        // Combines what is already in a cell with a new segment. The new segment's arms win where both
        // have an arm, so a box drawn later (the focused one) stays whole over a shared line.
        internal static string Merge(string? existing, int newArms)
        {
            string? plain = Glyph(newArms);
            if (!TryGetArms(existing, out int oldArms))
                return plain ?? " ";

            int merged = Combine(oldArms, newArms);
            string? glyph = Glyph(merged);
            if (glyph != null)
                return glyph;

            // Mixed families with no glyph (heavy with double): bring the old arms into the new family.
            int weight = Math.Max(Math.Max(Arm(newArms, 6), Arm(newArms, 4)), Math.Max(Arm(newArms, 2), Arm(newArms, 0)));
            int coerced = 0;
            for (int shift = 0; shift <= 6; shift += 2)
            {
                int arm = Arm(oldArms, shift);
                if (arm != 0)
                    arm = weight == 3 ? 1 : (arm == 3 ? weight : arm);
                coerced |= arm << shift;
            }

            int combined = Combine(coerced, newArms);
            glyph = Glyph(combined);
            if (glyph != null)
                return glyph;

            // Still no glyph (single and double meeting on one axis): carry the new weight along any
            // axis whose two arms disagree, so the line stays connected.
            glyph = Glyph(UnifyAxis(UnifyAxis(combined, 6, 4, weight), 2, 0, weight));
            return glyph ?? plain ?? " ";
        }

        // Draws a cell of a box that must stay whole: every arm in the cell, the new box's and any line
        // already there, takes the new box's weight, so the result is a single-weight glyph that still
        // connects to the neighbours. Returns null when that family has no such glyph (double lines with
        // a half arm), so the caller can fall back to Merge.
        internal static string? Whole(string? existing, int newArms, int weight)
        {
            int oldArms;
            if (!TryGetArms(existing, out oldArms))
                oldArms = 0;

            int promoted = 0;
            for (int shift = 0; shift <= 6; shift += 2)
            {
                if (Arm(newArms, shift) != 0 || Arm(oldArms, shift) != 0)
                    promoted |= weight << shift;
            }

            return Glyph(promoted);
        }

        internal static string RoundedCorner(int arms)
        {
            if (arms == Pack(0, 1, 0, 1))
                return "╭";
            if (arms == Pack(0, 1, 1, 0))
                return "╮";
            if (arms == Pack(1, 0, 1, 0))
                return "╯";
            return "╰";
        }

        private static int UnifyAxis(int arms, int firstShift, int secondShift, int weight)
        {
            int first = Arm(arms, firstShift);
            int second = Arm(arms, secondShift);
            if (first == 0 || second == 0 || first == second)
                return arms;

            arms &= ~((3 << firstShift) | (3 << secondShift));
            return arms | (weight << firstShift) | (weight << secondShift);
        }

        private static int Combine(int oldArms, int newArms)
        {
            int result = 0;
            for (int shift = 0; shift <= 6; shift += 2)
            {
                int arm = Arm(newArms, shift);
                if (arm == 0)
                    arm = Arm(oldArms, shift);
                result |= arm << shift;
            }

            return result;
        }
    }
}
