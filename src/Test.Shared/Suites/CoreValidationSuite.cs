namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Unicode;

    /// <summary>
    /// Validation coverage for the core primitives: geometry, cell buffers, styled text, graphemes,
    /// and the surface-drawing extensions.
    /// </summary>
    public static class CoreValidationSuite
    {
        /// <summary>
        /// Builds the core-validation suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "CoreValidation",
                displayName: "Core Validation (geometry / buffers / text / draw)",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("CoreValidation", "Rect", "Rect rejects negative dimensions",
                        _ =>
                        {
                            Check.Throws<ArgumentOutOfRangeException>(() => new Rect(0, 0, -1, 4), "negative width");
                            Check.Throws<ArgumentOutOfRangeException>(() => new Rect(0, 0, 4, -1), "negative height");
                            Rect ok = new Rect(1, 2, 0, 0);
                            Check.Equal(0, ok.Width, "zero-size rect allowed");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("CoreValidation", "BufferAndSurface", "Buffer copy and surface wrap validate arguments",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => new BufferSurface(null!), "null buffer surface");

                            CellBuffer target = new CellBuffer(3, 2);
                            Check.Throws<ArgumentNullException>(() => target.CopyFrom(null!), "copy from null");
                            Check.Throws<ArgumentException>(() => target.CopyFrom(new CellBuffer(4, 4)), "copy mismatched dimensions");
                            Check.Throws<ArgumentOutOfRangeException>(() => target.Resize(-1, 2), "resize negative width");

                            CellBuffer source = new CellBuffer(3, 2);
                            source.Set(1, 1, Cell.Glyph("Z", CellStyle.Default, 1));
                            target.CopyFrom(source);
                            Check.Equal("Z", target.Get(1, 1).Grapheme, "equal-size copy replicates cells");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("CoreValidation", "StyledText", "Styled text and spans reject null",
                        _ =>
                        {
                            Check.Throws<ArgumentNullException>(() => new StyledText(null!), "null spans");
                            Check.Throws<ArgumentNullException>(() => StyledText.From(null!), "null From value");
                            Check.Throws<ArgumentNullException>(() => Text.From("x").Append((string)null!), "null Append string");
                            Check.Throws<ArgumentNullException>(() => Text.From("x").Append((StyledText)null!), "null Append text");
                            Check.Throws<ArgumentNullException>(() => new StyledSpan(null!, CellStyle.Default), "null span text");
                            Check.Throws<ArgumentNullException>(() => new Grapheme(null!, 1), "null grapheme text");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("CoreValidation", "DrawGuards", "Surface drawing extensions null-guard their arguments",
                        _ =>
                        {
                            CellBuffer buffer = new CellBuffer(6, 3);
                            ISurface surface = new BufferSurface(buffer);
                            CellStyle style = CellStyle.Default;

                            Check.Throws<ArgumentNullException>(() => surface.DrawText(0, 0, null!, style), "null draw text");
                            Check.Throws<ArgumentNullException>(() => ((ISurface)null!).DrawText(0, 0, "x", style), "null draw surface");
                            Check.Throws<ArgumentNullException>(() => surface.DrawStyledText(0, 0, (StyledText)null!), "null styled text");
                            Check.Throws<ArgumentNullException>(() => surface.DrawStyledText(0, 0, (StyledText)null!, style), "null styled text with base");
                            Check.Throws<ArgumentNullException>(() => ((ISurface)null!).DrawBox(new Rect(0, 0, 4, 3), style), "null box surface");
                            Check.Throws<ArgumentException>(() => surface.DrawHorizontalLine(0, 0, 4, null!, style), "null line glyph rejected");
                            Check.Throws<ArgumentException>(() => surface.DrawHorizontalLine(0, 0, 4, string.Empty, style), "empty line glyph rejected");
                            Check.Throws<ArgumentNullException>(() => Backdrop.Dim(null!, new Rect(0, 0, 1, 1)), "null dim buffer");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
