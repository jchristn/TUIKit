namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Ascii;
    using TUIKit.Ascii.Fonts;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>
    /// Touchstone suite for the ASCII-art font engine: the composition/kerning/smushing engine, the
    /// FIGlet loader, the font-library manager, and the <see cref="AsciiArtText"/> widget. Covers both
    /// positive behavior and negative/guard cases.
    /// </summary>
    public static class AsciiArtSuite
    {
        /// <summary>
        /// Builds the ASCII-art suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "AsciiArt",
                displayName: "ASCII Art Fonts",
                cases: new List<TestCaseDescriptor>
                {
                    EngineBlockRenders(),
                    EngineHeightAndUniformWidth(),
                    EngineEmptyAndUnknown(),
                    EngineLayoutWidthOrdering(),
                    EngineAlignment(),
                    EngineRenderNullGuards(),
                    SmushEqualCharacter(),
                    SmushOppositePair(),
                    SmushBigX(),
                    SmushUnderscore(),
                    SmushHierarchy(),
                    SmushHardBlank(),
                    KerningDoesNotMergeInk(),
                    OptionsMaxWidthGuard(),
                    LoaderRoundTrip(),
                    LoaderStandardFixture(),
                    LoaderNegative(),
                    LoaderRenderAsync(),
                    LibraryRegisterAndResolve(),
                    LibraryCaseInsensitive(),
                    LibraryDuplicateAndTryRegister(),
                    LibraryUnregisterAndMissing(),
                    LibraryNullGuards(),
                    LibraryEnumerateAndAsync(),
                    LibraryConcurrency(),
                    LibraryDisposed(),
                    LibraryLicenseGate(),
                    WidgetRendersAndSwitchesFont(),
                    WidgetMeasureAndAlignment(),
                    WidgetGuards()
                });
        }

        // ---- Engine ------------------------------------------------------------------------------

        private static TestCaseDescriptor EngineBlockRenders()
        {
            return new TestCaseDescriptor("AsciiArt", "BlockRenders", "Block font renders ink rows",
                _ =>
                {
                    BlockAsciiFont font = new BlockAsciiFont();
                    IReadOnlyList<string> rows = AsciiArt.Render("HI", font);
                    Check.Equal(5, rows.Count, "block height is five rows");
                    bool ink = false;
                    foreach (string row in rows)
                    {
                        if (row.Contains("█"))
                            ink = true;
                    }

                    Check.True(ink, "block ink is drawn");
                    return Task.CompletedTask;
                });
        }

        private static TestCaseDescriptor EngineHeightAndUniformWidth()
        {
            return new TestCaseDescriptor("AsciiArt", "UniformWidth", "Every row shares the same width",
                _ =>
                {
                    IAsciiFont font = AsciiFontLibrary.Default.Get("Standard");
                    IReadOnlyList<string> rows = AsciiArt.Render("Width!", font);
                    Check.Equal(font.Metrics.Height, rows.Count, "row count equals font height");
                    for (int i = 1; i < rows.Count; i++)
                        Check.Equal(rows[0].Length, rows[i].Length, "rows share a width");

                    return Task.CompletedTask;
                });
        }

        private static TestCaseDescriptor EngineEmptyAndUnknown()
        {
            return new TestCaseDescriptor("AsciiArt", "EmptyAndUnknown", "Empty and unknown input render blank, no throw",
                _ =>
                {
                    BlockAsciiFont font = new BlockAsciiFont();

                    IReadOnlyList<string> empty = AsciiArt.Render(string.Empty, font);
                    Check.Equal(5, empty.Count, "empty text still yields height rows");
                    foreach (string row in empty)
                        Check.Equal(0, row.Length, "empty text has zero width");

                    Check.False(font.TryGetGlyph('~', out AsciiGlyph _unused), "tilde is unsupported by the block font");
                    IReadOnlyList<string> unknown = AsciiArt.Render("~", font);
                    Check.Equal(5, unknown.Count, "unknown char still yields height rows");
                    bool anyInk = false;
                    foreach (string row in unknown)
                    {
                        if (row.Contains("█"))
                            anyInk = true;
                    }

                    Check.False(anyInk, "unknown char renders no ink");

                    Check.True(font.TryGetGlyph('A', out AsciiGlyph glyph), "A is supported");
                    Check.Equal(5, glyph.Height, "glyph height matches font");
                    return Task.CompletedTask;
                });
        }

        private static TestCaseDescriptor EngineLayoutWidthOrdering()
        {
            return new TestCaseDescriptor("AsciiArt", "LayoutWidthOrdering", "FullWidth is widest, Smushing no wider than Kerning",
                _ =>
                {
                    IAsciiFont font = AsciiFontLibrary.Default.Get("Standard");
                    int full = Width(font, "AAAA", AsciiLayoutMode.FullWidth);
                    int kern = Width(font, "AAAA", AsciiLayoutMode.Kerning);
                    int smush = Width(font, "AAAA", AsciiLayoutMode.Smushing);

                    Check.True(full >= kern, "full width is at least kerning width");
                    Check.True(kern >= smush, "kerning width is at least smushing width");
                    Check.True(full > smush, "smushing is strictly tighter than full width for this text");
                    return Task.CompletedTask;
                });
        }

        private static TestCaseDescriptor EngineAlignment()
        {
            return new TestCaseDescriptor("AsciiArt", "Alignment", "MaxWidth alignment pads left/center/right",
                _ =>
                {
                    IAsciiFont font = new BlockAsciiFont();
                    IReadOnlyList<string> left = AsciiArt.Render("A", font, new AsciiArtOptions { MaxWidth = 40, Alignment = AsciiArtAlignment.Left });
                    IReadOnlyList<string> right = AsciiArt.Render("A", font, new AsciiArtOptions { MaxWidth = 40, Alignment = AsciiArtAlignment.Right });
                    IReadOnlyList<string> center = AsciiArt.Render("A", font, new AsciiArtOptions { MaxWidth = 40, Alignment = AsciiArtAlignment.Center });

                    Check.Equal(40, left[0].Length, "left padded to max width");
                    Check.Equal(40, right[0].Length, "right padded to max width");
                    Check.Equal(40, center[0].Length, "center padded to max width");

                    Check.True(FirstInk(left) < FirstInk(center), "left ink starts before center ink");
                    Check.True(FirstInk(center) < FirstInk(right), "center ink starts before right ink");
                    return Task.CompletedTask;
                });
        }

        private static TestCaseDescriptor EngineRenderNullGuards()
        {
            return new TestCaseDescriptor("AsciiArt", "RenderNullGuards", "Render guards null text and font",
                _ =>
                {
                    IAsciiFont font = new BlockAsciiFont();
                    Check.Throws<ArgumentNullException>(() => AsciiArt.Render(null!, font), "null text");
                    Check.Throws<ArgumentNullException>(() => AsciiArt.Render("x", null!), "null font");
                    return Task.CompletedTask;
                });
        }

        // ---- Smushing rules (crafted single-row fonts) -------------------------------------------

        private static TestCaseDescriptor SmushEqualCharacter()
        {
            return new TestCaseDescriptor("AsciiArt", "SmushEqualChar", "Equal-character smushing merges identical edges",
                _ =>
                {
                    IAsciiFont font = OneRowFont((int)AsciiSmushRule.EqualCharacter, null);
                    Check.Equal(1, Width(font, "AA", null), "two A's smush to one column");
                    Check.Equal(2, Width(font, "AA", AsciiLayoutMode.FullWidth), "full width keeps both columns");
                    return Task.CompletedTask;
                });
        }

        private static TestCaseDescriptor SmushOppositePair()
        {
            return new TestCaseDescriptor("AsciiArt", "SmushOppositePair", "Opposite brackets smush to a bar",
                _ =>
                {
                    IAsciiFont font = OneRowFont((int)AsciiSmushRule.OppositePair, null);
                    Check.Equal("|", Row0(font, "[]"), "[] smushes into |");
                    Check.Equal("|", Row0(font, "()"), "() smushes into |");
                    return Task.CompletedTask;
                });
        }

        private static TestCaseDescriptor SmushBigX()
        {
            return new TestCaseDescriptor("AsciiArt", "SmushBigX", "Big-X smushing combines slashes",
                _ =>
                {
                    IAsciiFont font = OneRowFont((int)AsciiSmushRule.BigX, null);
                    Check.Equal("|", Row0(font, "/\\"), "/\\ smushes into |");
                    Check.Equal("Y", Row0(font, "\\/"), "\\/ smushes into Y");
                    Check.Equal("X", Row0(font, "><"), ">< smushes into X");
                    return Task.CompletedTask;
                });
        }

        private static TestCaseDescriptor SmushUnderscore()
        {
            return new TestCaseDescriptor("AsciiArt", "SmushUnderscore", "Underscore is replaced by a border character",
                _ =>
                {
                    IAsciiFont font = OneRowFont((int)AsciiSmushRule.Underscore, null);
                    Check.Equal("/", Row0(font, "_/"), "underscore yields to slash");
                    Check.Equal("|", Row0(font, "|_"), "underscore yields to bar");
                    return Task.CompletedTask;
                });
        }

        private static TestCaseDescriptor SmushHierarchy()
        {
            return new TestCaseDescriptor("AsciiArt", "SmushHierarchy", "Hierarchy smushing keeps the higher class",
                _ =>
                {
                    IAsciiFont font = OneRowFont((int)AsciiSmushRule.Hierarchy, null);
                    Check.Equal("/", Row0(font, "|/"), "slash outranks bar");
                    Check.Equal("<", Row0(font, "(<"), "angle outranks paren");
                    return Task.CompletedTask;
                });
        }

        private static TestCaseDescriptor SmushHardBlank()
        {
            return new TestCaseDescriptor("AsciiArt", "SmushHardBlank", "Hardblank smushing merges two hardblanks",
                _ =>
                {
                    Dictionary<char, string> overrides = new Dictionary<char, string> { { 'A', "$" } };
                    IAsciiFont font = OneRowFont((int)AsciiSmushRule.HardBlank, overrides);
                    IReadOnlyList<string> rows = AsciiArt.Render("AA", font, new AsciiArtOptions { TrimBlankColumns = false });
                    Check.Equal(1, rows[0].Length, "two hardblanks smush to one column");
                    Check.Equal(" ", rows[0], "hardblank renders as a space");
                    return Task.CompletedTask;
                });
        }

        private static TestCaseDescriptor KerningDoesNotMergeInk()
        {
            return new TestCaseDescriptor("AsciiArt", "KerningNoMerge", "Kerning never merges two ink columns",
                _ =>
                {
                    IAsciiFont font = OneRowFont((int)AsciiSmushRule.EqualCharacter, null);
                    Check.Equal("AA", Row0Layout(font, "AA", AsciiLayoutMode.Kerning), "kerning keeps both A's");
                    return Task.CompletedTask;
                });
        }

        private static TestCaseDescriptor OptionsMaxWidthGuard()
        {
            return new TestCaseDescriptor("AsciiArt", "MaxWidthGuard", "Negative MaxWidth is rejected",
                _ =>
                {
                    Check.Throws<ArgumentOutOfRangeException>(() => new AsciiArtOptions { MaxWidth = -1 }, "negative max width");
                    return Task.CompletedTask;
                });
        }

        // ---- Loader ------------------------------------------------------------------------------

        private static TestCaseDescriptor LoaderRoundTrip()
        {
            return new TestCaseDescriptor("AsciiArt", "LoaderRoundTrip", "Loader parses a hand-written FIGlet font",
                _ =>
                {
                    IAsciiFont font = OneRowFont(0, null);
                    Check.Equal(1, font.Metrics.Height, "height parsed from header");
                    Check.Equal('$', font.Metrics.HardBlank, "hardblank parsed from header");
                    Check.True(font.TryGetGlyph('A', out AsciiGlyph _present), "A glyph present");
                    Check.Equal("Test", font.Name, "explicit name applied");
                    return Task.CompletedTask;
                });
        }

        private static TestCaseDescriptor LoaderStandardFixture()
        {
            return new TestCaseDescriptor("AsciiArt", "StandardFixture", "Standard font renders the expected glyphs",
                _ =>
                {
                    IAsciiFont font = AsciiFontLibrary.Default.Get("Standard");
                    Check.Equal(6, font.Metrics.Height, "Standard is six rows tall");
                    IReadOnlyList<string> rows = AsciiArt.Render("HI", font);
                    Check.Equal(11, rows[0].Length, "Standard HI is eleven columns wide");
                    Check.Equal("| | | |_ _|", rows[1], "Standard HI second row matches fixture");
                    foreach (string row in rows)
                        Check.False(row.Contains("$"), "hardblank replaced by space");

                    return Task.CompletedTask;
                });
        }

        private static TestCaseDescriptor LoaderNegative()
        {
            return new TestCaseDescriptor("AsciiArt", "LoaderNegative", "Loader rejects null and malformed fonts",
                _ =>
                {
                    Check.Throws<ArgumentNullException>(() => FigletFontLoader.Load((string)null!), "null content");
                    Check.Throws<ArgumentNullException>(() => FigletFontLoader.Load((System.IO.Stream)null!), "null stream");
                    Check.Throws<AsciiFontException>(() => FigletFontLoader.Load("not a figlet font"), "bad signature");
                    Check.Throws<AsciiFontException>(() => FigletFontLoader.Load(string.Empty), "empty content");
                    Check.Throws<AsciiFontException>(() => FigletFontLoader.Load("flf2a$ 6 5 16 15 99\nonly one line"), "truncated glyphs");
                    return Task.CompletedTask;
                });
        }

        private static TestCaseDescriptor LoaderRenderAsync()
        {
            return new TestCaseDescriptor("AsciiArt", "RenderAsync", "RenderAsync mirrors Render and honors cancellation",
                async ct =>
                {
                    IAsciiFont font = new BlockAsciiFont();
                    IReadOnlyList<string> sync = AsciiArt.Render("OK", font);
                    IReadOnlyList<string> async = await AsciiArt.RenderAsync("OK", font, null, ct).ConfigureAwait(false);
                    Check.Equal(sync.Count, async.Count, "async row count matches sync");
                    Check.Equal(sync[0], async[0], "async first row matches sync");

                    using (CancellationTokenSource cts = new CancellationTokenSource())
                    {
                        cts.Cancel();
                        await Check.ThrowsAsync<OperationCanceledException>(
                            () => AsciiArt.RenderAsync("OK", font, null, cts.Token), "cancelled render").ConfigureAwait(false);
                    }
                });
        }

        // ---- Library manager ---------------------------------------------------------------------

        private static TestCaseDescriptor LibraryRegisterAndResolve()
        {
            return new TestCaseDescriptor("AsciiArt", "LibraryRegister", "Register then resolve returns the same font",
                _ =>
                {
                    using (AsciiFontLibrary library = new AsciiFontLibrary())
                    {
                        IAsciiFont font = new BlockAsciiFont();
                        library.Register(font);
                        Check.True(library.Contains("Block"), "contains after register");
                        Check.Equal(1, library.Count, "count reflects registration");
                        Check.Equal(library.Names.Count, library.Count, "names count matches count");
                        Check.True(library.TryGet("Block", out IAsciiFont resolved), "try get succeeds");
                        Check.True(ReferenceEquals(font, resolved), "same instance resolved");
                        Check.True(ReferenceEquals(font, library.Get("Block")), "get returns same instance");
                    }

                    return Task.CompletedTask;
                });
        }

        private static TestCaseDescriptor LibraryCaseInsensitive()
        {
            return new TestCaseDescriptor("AsciiArt", "LibraryCaseInsensitive", "Name lookup is case-insensitive",
                _ =>
                {
                    using (AsciiFontLibrary library = new AsciiFontLibrary())
                    {
                        library.Register(new BlockAsciiFont());
                        Check.True(library.Contains("block"), "lowercase resolves");
                        Check.True(library.Contains("BLOCK"), "uppercase resolves");
                        Check.True(library.Contains("Block"), "exact resolves");
                    }

                    return Task.CompletedTask;
                });
        }

        private static TestCaseDescriptor LibraryDuplicateAndTryRegister()
        {
            return new TestCaseDescriptor("AsciiArt", "LibraryDuplicate", "Duplicate registration is rejected; TryRegister returns false",
                _ =>
                {
                    using (AsciiFontLibrary library = new AsciiFontLibrary())
                    {
                        library.Register(new BlockAsciiFont());
                        Check.Throws<AsciiFontException>(() => library.Register(new BlockAsciiFont()), "duplicate throws");
                        Check.False(library.TryRegister(new BlockAsciiFont()), "try register duplicate is false");
                        Check.True(library.TryRegister(OneRowFont(0, null)), "try register new name is true");
                        Check.Equal(2, library.Count, "count reflects two fonts");
                    }

                    return Task.CompletedTask;
                });
        }

        private static TestCaseDescriptor LibraryUnregisterAndMissing()
        {
            return new TestCaseDescriptor("AsciiArt", "LibraryUnregister", "Unregister removes; missing get throws",
                _ =>
                {
                    using (AsciiFontLibrary library = new AsciiFontLibrary())
                    {
                        library.Register(new BlockAsciiFont());
                        Check.True(library.Unregister("Block"), "unregister returns true");
                        Check.False(library.Unregister("Block"), "second unregister is false");
                        Check.False(library.TryGet("Block", out IAsciiFont _removed), "try get is false after removal");
                        Check.Throws<AsciiFontException>(() => library.Get("Nope"), "missing get throws");
                    }

                    return Task.CompletedTask;
                });
        }

        private static TestCaseDescriptor LibraryNullGuards()
        {
            return new TestCaseDescriptor("AsciiArt", "LibraryNullGuards", "Library guards null arguments",
                _ =>
                {
                    using (AsciiFontLibrary library = new AsciiFontLibrary())
                    {
                        Check.Throws<ArgumentNullException>(() => library.Register(null!), "register null");
                        Check.Throws<ArgumentNullException>(() => library.TryRegister(null!), "try register null");
                        Check.Throws<ArgumentNullException>(() => library.Contains(null!), "contains null");
                        Check.Throws<ArgumentNullException>(() => library.TryGet(null!, out IAsciiFont _null), "try get null");
                        Check.Throws<ArgumentNullException>(() => library.Unregister(null!), "unregister null");
                    }

                    return Task.CompletedTask;
                });
        }

        private static TestCaseDescriptor LibraryEnumerateAndAsync()
        {
            return new TestCaseDescriptor("AsciiArt", "LibraryEnumerate", "Enumerate and EnumerateAsync agree; cancellation observed",
                async ct =>
                {
                    using (AsciiFontLibrary library = new AsciiFontLibrary())
                    {
                        library.Register(new BlockAsciiFont());
                        library.Register(OneRowFont(0, null));

                        int sync = 0;
                        foreach (IAsciiFont font in library.Enumerate())
                        {
                            Check.True(font != null, "enumerated font not null");
                            sync++;
                        }

                        int async = 0;
                        await foreach (IAsciiFont font in library.EnumerateAsync(ct).ConfigureAwait(false))
                        {
                            Check.True(font != null, "async enumerated font not null");
                            async++;
                        }

                        Check.Equal(sync, async, "sync and async yield the same count");
                        Check.Equal(2, sync, "two fonts enumerated");

                        using (CancellationTokenSource cts = new CancellationTokenSource())
                        {
                            cts.Cancel();
                            await Check.ThrowsAsync<OperationCanceledException>(
                                async () =>
                                {
                                    await foreach (IAsciiFont font in library.EnumerateAsync(cts.Token).ConfigureAwait(false))
                                    {
                                        _ = font;
                                    }
                                },
                                "cancelled enumeration").ConfigureAwait(false);
                        }
                    }
                });
        }

        private static TestCaseDescriptor LibraryConcurrency()
        {
            return new TestCaseDescriptor("AsciiArt", "LibraryConcurrency", "Concurrent readers and writers do not corrupt the registry",
                async ct =>
                {
                    using (AsciiFontLibrary library = new AsciiFontLibrary())
                    {
                        List<Task> tasks = new List<Task>();
                        for (int i = 0; i < 8; i++)
                        {
                            int index = i;
                            tasks.Add(Task.Run(() =>
                            {
                                library.TryRegister(OneRowFont(0, null, "F" + index));
                                _ = library.Contains("F" + index);
                                foreach (IAsciiFont font in library.Enumerate())
                                    _ = font.Name;
                            }, ct));
                        }

                        await Task.WhenAll(tasks).ConfigureAwait(false);
                        Check.Equal(8, library.Count, "all eight distinct fonts registered exactly once");
                    }
                });
        }

        private static TestCaseDescriptor LibraryDisposed()
        {
            return new TestCaseDescriptor("AsciiArt", "LibraryDisposed", "Disposed library rejects further use",
                _ =>
                {
                    AsciiFontLibrary library = new AsciiFontLibrary();
                    library.Register(new BlockAsciiFont());
                    library.Dispose();
                    Check.Throws<ObjectDisposedException>(() => library.Contains("Block"), "contains after dispose");
                    Check.Throws<ObjectDisposedException>(() => library.Register(new BlockAsciiFont()), "register after dispose");
                    Check.Throws<ObjectDisposedException>(() => { int _ = library.Count; }, "count after dispose");
                    library.Dispose();
                    return Task.CompletedTask;
                });
        }

        private static TestCaseDescriptor LibraryLicenseGate()
        {
            return new TestCaseDescriptor("AsciiArt", "LicenseGate", "Default registers the cleared, expansive roster",
                _ =>
                {
                    AsciiFontLibrary library = AsciiFontLibrary.Default;
                    Check.True(library.Contains("Block"), "default block font present");
                    Check.True(library.Contains("Standard"), "Standard present");
                    Check.True(library.Contains("Slant"), "Slant present");
                    Check.True(library.Contains("Doom"), "Doom present");
                    Check.True(library.Contains("Graffiti"), "Graffiti present");
                    Check.True(library.Count > 50, "expansive roster registered");
                    return Task.CompletedTask;
                });
        }

        // ---- Widget ------------------------------------------------------------------------------

        private static TestCaseDescriptor WidgetRendersAndSwitchesFont()
        {
            return new TestCaseDescriptor("AsciiArt", "WidgetRenders", "Widget renders and re-renders on font change",
                _ =>
                {
                    AsciiArtText widget = new AsciiArtText("A");
                    Check.True(widget.ArtWidth > 0, "widget has width");
                    WidgetTester blockTester = WidgetTester.For(widget, 40, 8).Render();
                    Check.True(blockTester.Contains("█"), "block ink drawn");

                    int before = widget.ArtWidth;
                    widget.Font = AsciiFontLibrary.Default.Get("Standard");
                    Check.True(widget.ArtWidth != before || !WidgetTester.For(widget, 40, 8).Render().Contains("█"),
                        "changing font re-composes the art");
                    return Task.CompletedTask;
                });
        }

        private static TestCaseDescriptor WidgetMeasureAndAlignment()
        {
            return new TestCaseDescriptor("AsciiArt", "WidgetMeasure", "Widget measures to font height and aligns",
                _ =>
                {
                    AsciiArtText widget = new AsciiArtText("A") { Alignment = AsciiArtAlignment.Right };
                    Size measured = widget.Measure(new Size(200, 200));
                    Check.Equal(5, measured.Height, "block widget is five rows tall");
                    Check.True(measured.Width > 0 && measured.Width <= 200, "width within available");

                    string right = WidgetTester.For(widget, 40, 6).Render().Text();
                    widget.Alignment = AsciiArtAlignment.Left;
                    string left = WidgetTester.For(widget, 40, 6).Render().Text();
                    Check.True(right != left, "alignment changes the rendered offset");
                    return Task.CompletedTask;
                });
        }

        private static TestCaseDescriptor WidgetGuards()
        {
            return new TestCaseDescriptor("AsciiArt", "WidgetGuards", "Widget guards nulls and tiny surfaces",
                _ =>
                {
                    Check.Throws<ArgumentNullException>(() => new AsciiArtText(null!), "null text");
                    Check.Throws<ArgumentNullException>(() => new AsciiArtText("x", null!), "null font");
                    AsciiArtText widget = new AsciiArtText("A");
                    Check.Throws<ArgumentNullException>(() => widget.Font = null!, "null font set");
                    Check.Throws<ArgumentNullException>(() => widget.Text = null!, "null text set");

                    // A one-cell surface must clip without throwing.
                    WidgetTester.For(widget, 1, 1).Render();
                    return Task.CompletedTask;
                });
        }

        // ---- Helpers -----------------------------------------------------------------------------

        private static int Width(IAsciiFont font, string text, AsciiLayoutMode? layout)
        {
            AsciiArtOptions options = new AsciiArtOptions { TrimBlankColumns = false };
            if (layout.HasValue)
                options.Layout = layout.Value;

            IReadOnlyList<string> rows = AsciiArt.Render(text, font, options);
            return rows.Count == 0 ? 0 : rows[0].Length;
        }

        private static string Row0(IAsciiFont font, string text)
        {
            return AsciiArt.Render(text, font, new AsciiArtOptions { TrimBlankColumns = false })[0];
        }

        private static string Row0Layout(IAsciiFont font, string text, AsciiLayoutMode layout)
        {
            return AsciiArt.Render(text, font, new AsciiArtOptions { Layout = layout, TrimBlankColumns = false })[0];
        }

        private static int FirstInk(IReadOnlyList<string> rows)
        {
            int min = int.MaxValue;
            for (int r = 0; r < rows.Count; r++)
            {
                for (int c = 0; c < rows[r].Length; c++)
                {
                    if (rows[r][c] != ' ')
                    {
                        if (c < min)
                            min = c;
                        break;
                    }
                }
            }

            return min == int.MaxValue ? 0 : min;
        }

        private static IAsciiFont OneRowFont(int oldLayout, IDictionary<char, string>? overrides, string name = "Test")
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("flf2a$ 1 1 20 ").Append(oldLayout).Append(" 0\n");
            for (int code = 32; code <= 126; code++)
            {
                char c = (char)code;
                string glyph = overrides != null && overrides.TryGetValue(c, out string? value) ? value : c.ToString();
                builder.Append(glyph).Append("@@\n");
            }

            return FigletFontLoader.Load(builder.ToString(), name);
        }
    }
}
