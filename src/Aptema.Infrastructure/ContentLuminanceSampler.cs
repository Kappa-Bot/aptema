using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Aptema.Core;

namespace Aptema.Infrastructure;

public sealed class ContentLuminanceSampler : IContentLuminanceSampler
{
    private const int SampleWidth = 96;
    private const int SampleHeight = 54;

    public ValueTask<ContentLuminanceSample> SampleAsync(bool enabled, CancellationToken cancellationToken)
    {
        if (!enabled)
        {
            return ValueTask.FromResult(ContentLuminanceSample.Unknown);
        }

        try
        {
            using var bitmap = new Bitmap(SampleWidth, SampleHeight, PixelFormat.Format24bppRgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(0, 0, 0, 0, new Size(SampleWidth, SampleHeight), CopyPixelOperation.SourceCopy);
            }

            var data = bitmap.LockBits(new Rectangle(0, 0, SampleWidth, SampleHeight), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                var raw = new byte[Math.Abs(data.Stride) * SampleHeight];
                Marshal.Copy(data.Scan0, raw, 0, raw.Length);
                var rgb = new byte[SampleWidth * SampleHeight * 3];
                var target = 0;
                for (var y = 0; y < SampleHeight; y++)
                {
                    var row = y * data.Stride;
                    for (var x = 0; x < SampleWidth; x++)
                    {
                        var source = row + (x * 3);
                        rgb[target++] = raw[source + 2];
                        rgb[target++] = raw[source + 1];
                        rgb[target++] = raw[source];
                    }
                }

                return ValueTask.FromResult(LuminanceAnalyzer.Analyze(rgb));
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }
        catch (ExternalException)
        {
            return ValueTask.FromResult(ContentLuminanceSample.Unknown);
        }
        catch (InvalidOperationException)
        {
            return ValueTask.FromResult(ContentLuminanceSample.Unknown);
        }
    }
}
