namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Testing;
    using TUIKit.Widgets;

    /// <summary>
    /// Touchstone suite for banners, shadows, dimming, and images: FIGlet banners (10.36), drop shadows (10.38), backdrop dimming
    /// (10.39), and half-block image rendering (10.40).
    /// </summary>
    public static class VisualEffectsSuite
    {
        /// <summary>
        /// Builds the banners, shadows, dimming, and images suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "VisualEffects",
                displayName: "Banners, Shadows, Dimming & Images",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("VisualEffects", "Banner", "Banner renders five rows of block text",
                        _ =>
                        {
                            IReadOnlyList<string> rows = Banner.Render("HI");
                            Check.Equal(5, rows.Count, "five rows tall");
                            foreach (string row in rows)
                                Check.Equal(rows[0].Length, row.Length, "rows share a width");
                            Check.True(rows[0].Contains("█"), "ink drawn as block glyph");

                            BannerText banner = new BannerText("A");
                            Check.True(banner.BannerWidth > 0, "banner has width");
                            WidgetTester tester = WidgetTester.For(banner, 20, 5).Render();
                            Check.True(tester.Text().Contains("█"), "banner widget draws blocks");

                            Check.Throws<ArgumentNullException>(() => Banner.Render(null!), "null text");
                            Check.Throws<ArgumentNullException>(() => new BannerText(null!), "null banner text");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("VisualEffects", "BannerUnknown", "Banner renders unknown characters as blank",
                        _ =>
                        {
                            IReadOnlyList<string> rows = Banner.Render("~");
                            Check.Equal(5, rows.Count, "still five rows");
                            bool anyInk = false;
                            foreach (string row in rows)
                            {
                                if (row.Contains("█"))
                                    anyInk = true;
                            }

                            Check.False(anyInk, "unknown glyph is blank");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("VisualEffects", "Shadow", "Drop shadow draws to the right and below",
                        _ =>
                        {
                            CellBuffer buffer = new CellBuffer(10, 6);
                            ISurface surface = new BufferSurface(buffer);
                            surface.DrawShadow(new Rect(1, 1, 4, 3));

                            Check.Equal("░", buffer.Get(5, 2).Grapheme, "shadow on the right edge");
                            Check.Equal("░", buffer.Get(2, 4).Grapheme, "shadow on the bottom edge");
                            Check.Equal(" ", buffer.Get(1, 1).Grapheme, "no shadow over the box itself");

                            Check.Throws<ArgumentNullException>(() => SurfaceExtensionsProxy(null!), "null surface");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("VisualEffects", "Dim", "Backdrop dims cells without losing glyphs",
                        _ =>
                        {
                            CellBuffer buffer = new CellBuffer(6, 2);
                            ISurface surface = new BufferSurface(buffer);
                            surface.DrawText(0, 0, "hello", CellStyle.Default);

                            Check.False(buffer.Get(0, 0).Style.HasAttribute(CellAttributes.Dim), "not dim before");
                            Backdrop.Dim(buffer);
                            Check.True(buffer.Get(0, 0).Style.HasAttribute(CellAttributes.Dim), "dim after");
                            Check.Equal("h", buffer.Get(0, 0).Grapheme, "glyph preserved");

                            Check.Throws<ArgumentNullException>(() => Backdrop.Dim(null!), "null buffer");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("VisualEffects", "HalfBlockImage", "Half-block image packs two pixels per cell",
                        _ =>
                        {
                            Color[,] pixels =
                            {
                                { Color.FromRgb(255, 0, 0), Color.FromRgb(0, 255, 0) },
                                { Color.FromRgb(0, 0, 255), Color.FromRgb(255, 255, 0) }
                            };
                            HalfBlockImage image = new HalfBlockImage(pixels);
                            Check.Equal(2, image.PixelWidth, "two pixels wide");
                            Check.Equal(2, image.PixelHeight, "two pixels tall");

                            CellBuffer buffer = new CellBuffer(2, 1);
                            image.Render(new BufferSurface(buffer));
                            Cell cell = buffer.Get(0, 0);
                            Check.Equal("▀", cell.Grapheme, "upper half-block glyph");
                            Check.Equal(255, cell.Style.Foreground.R, "top pixel is the foreground");
                            Check.Equal(255, cell.Style.Background.B, "bottom pixel is the background");

                            HalfBlockImage sampled = new HalfBlockImage(3, 3, (x, y) => Color.FromRgb((byte)x, (byte)y, 0));
                            Check.Equal(2, sampled.Measure(new Size(10, 10)).Height, "3 pixel rows -> 2 cell rows");

                            Check.Throws<ArgumentNullException>(() => new HalfBlockImage((Color[,])null!), "null pixels");
                            Check.Throws<ArgumentOutOfRangeException>(() => new HalfBlockImage(0, 2, (x, y) => Color.Default), "zero width");
                            return Task.CompletedTask;
                        })
                });
        }

        private static void SurfaceExtensionsProxy(ISurface surface)
        {
            surface.DrawShadow(new Rect(0, 0, 1, 1));
        }
    }
}
