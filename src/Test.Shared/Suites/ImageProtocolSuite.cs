namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using TUIKit;
    using TUIKit.Content;

    /// <summary>
    /// Touchstone suite for the terminal image graphics protocols (10.40): the sixel and kitty
    /// encoders, with positive and negative cases.
    /// </summary>
    public static class ImageProtocolSuite
    {
        /// <summary>
        /// Builds the suite descriptor.
        /// </summary>
        /// <returns>The suite descriptor.</returns>
        public static TestSuiteDescriptor Suite()
        {
            return new TestSuiteDescriptor(
                suiteId: "ImageProtocol",
                displayName: "Sixel & Kitty Image Protocols",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor("ImageProtocol", "Sixel", "Sixel encoder wraps a valid, deterministic sequence",
                        _ =>
                        {
                            Color[,] pixels =
                            {
                                { Color.FromRgb(255, 0, 0), Color.FromRgb(0, 255, 0) },
                                { Color.FromRgb(0, 0, 255), Color.FromRgb(255, 255, 255) }
                            };

                            string sixel = SixelEncoder.Encode(pixels);
                            Check.True(sixel.StartsWith("\u001bP", StringComparison.Ordinal), "begins with DCS");
                            Check.True(sixel.Contains("q"), "contains the sixel introducer");
                            Check.True(sixel.Contains("\"1;1;2;2"), "carries raster attributes for the 2x2 image");
                            Check.True(sixel.Contains("#0;2;"), "defines a palette color in RGB mode");
                            Check.True(sixel.Contains("-"), "emits a band terminator");
                            Check.True(sixel.EndsWith("\u001b\\", StringComparison.Ordinal), "ends with ST");
                            Check.Equal(sixel, SixelEncoder.Encode(pixels), "encoding is deterministic");

                            Check.Throws<ArgumentNullException>(() => SixelEncoder.Encode(null!), "null pixels");
                            Check.Throws<ArgumentException>(() => SixelEncoder.Encode(new Color[0, 0]), "empty grid");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ImageProtocol", "SixelRle", "Sixel encoder run-length-encodes wide solid rows",
                        _ =>
                        {
                            Color[,] pixels = new Color[1, 40];
                            for (int x = 0; x < 40; x++)
                                pixels[0, x] = Color.FromRgb(10, 20, 30);

                            string sixel = SixelEncoder.Encode(pixels);
                            Check.True(sixel.Contains("!"), "uses run-length encoding for a wide solid row");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ImageProtocol", "Kitty", "Kitty encoder emits a valid graphics APC sequence",
                        _ =>
                        {
                            Color[,] pixels =
                            {
                                { Color.FromRgb(1, 2, 3), Color.FromRgb(4, 5, 6) },
                                { Color.FromRgb(7, 8, 9), Color.FromRgb(10, 11, 12) }
                            };

                            string kitty = KittyImageEncoder.Encode(pixels);
                            Check.True(kitty.StartsWith("\u001b_G", StringComparison.Ordinal), "begins with the graphics APC");
                            Check.True(kitty.Contains("a=T"), "transmits and displays");
                            Check.True(kitty.Contains("f=24"), "declares 24-bit RGB");
                            Check.True(kitty.Contains("s=2"), "declares width");
                            Check.True(kitty.Contains("v=2"), "declares height");
                            Check.True(kitty.Contains(";"), "separates control data from payload");
                            Check.True(kitty.EndsWith("\u001b\\", StringComparison.Ordinal), "ends with ST");
                            Check.Equal(kitty, KittyImageEncoder.Encode(pixels), "encoding is deterministic");

                            Check.Throws<ArgumentNullException>(() => KittyImageEncoder.Encode(null!), "null pixels");
                            Check.Throws<ArgumentException>(() => KittyImageEncoder.Encode(new Color[0, 0]), "empty grid");
                            return Task.CompletedTask;
                        }),

                    new TestCaseDescriptor("ImageProtocol", "KittyChunks", "Kitty encoder chunks a large payload with continuation flags",
                        _ =>
                        {
                            Color[,] pixels = new Color[40, 40];
                            for (int y = 0; y < 40; y++)
                            {
                                for (int x = 0; x < 40; x++)
                                    pixels[y, x] = Color.FromRgb((byte)x, (byte)y, 128);
                            }

                            string kitty = KittyImageEncoder.Encode(pixels);
                            Check.True(kitty.Contains("m=1"), "marks non-final chunks with m=1");
                            Check.True(kitty.Contains("m=0"), "marks the final chunk with m=0");

                            int count = 0;
                            int index = 0;
                            while ((index = kitty.IndexOf("\u001b_G", index, StringComparison.Ordinal)) >= 0)
                            {
                                count++;
                                index += 3;
                            }

                            Check.True(count >= 2, "large image split into multiple APC sequences");
                            return Task.CompletedTask;
                        })
                });
        }
    }
}
