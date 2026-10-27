Add-Type -AssemblyName System.Drawing

$code = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class LogoKeyer
{
    public static void MakeTransparent(string srcPath, string destPath, int keyMin, int featherFrom)
    {
        using (Bitmap source = new Bitmap(srcPath))
        {
            int w = source.Width;
            int h = source.Height;
            Rectangle rect = new Rectangle(0, 0, w, h);

            using (Bitmap bmp = source.Clone(rect, PixelFormat.Format32bppArgb))
            {
                BitmapData data = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
                int stride = data.Stride;
                byte[] buf = new byte[stride * h];
                Marshal.Copy(data.Scan0, buf, 0, buf.Length);

                bool[] isBackground = new bool[w * h];
                Queue<int> queue = new Queue<int>();

                for (int x = 0; x < w; x++)
                {
                    Seed(buf, stride, isBackground, queue, w, h, x, 0, keyMin);
                    Seed(buf, stride, isBackground, queue, w, h, x, h - 1, keyMin);
                }

                for (int y = 0; y < h; y++)
                {
                    Seed(buf, stride, isBackground, queue, w, h, 0, y, keyMin);
                    Seed(buf, stride, isBackground, queue, w, h, w - 1, y, keyMin);
                }

                while (queue.Count > 0)
                {
                    int index = queue.Dequeue();
                    int x = index % w;
                    int y = index / w;

                    if (x > 0) Seed(buf, stride, isBackground, queue, w, h, x - 1, y, keyMin);
                    if (x < w - 1) Seed(buf, stride, isBackground, queue, w, h, x + 1, y, keyMin);
                    if (y > 0) Seed(buf, stride, isBackground, queue, w, h, x, y - 1, keyMin);
                    if (y < h - 1) Seed(buf, stride, isBackground, queue, w, h, x, y + 1, keyMin);
                }

                // Clear the background and feather the outline against it.
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * stride + x * 4;

                        if (isBackground[y * w + x])
                        {
                            buf[i] = 255;
                            buf[i + 1] = 255;
                            buf[i + 2] = 255;
                            buf[i + 3] = 0;
                            continue;
                        }

                        if (!TouchesBackground(isBackground, w, h, x, y))
                        {
                            continue;
                        }

                        int b = buf[i];
                        int g = buf[i + 1];
                        int r = buf[i + 2];
                        int min = Math.Min(r, Math.Min(g, b));

                        if (min <= featherFrom)
                        {
                            continue;
                        }

                        double whiteness = (min - featherFrom) / (255.0 - featherFrom);
                        int alpha = (int)Math.Round(255.0 * (1.0 - whiteness));

                        if (alpha < 0) alpha = 0;
                        if (alpha > 255) alpha = 255;
                        buf[i + 3] = (byte)alpha;
                    }
                }

                Marshal.Copy(buf, 0, data.Scan0, buf.Length);
                bmp.UnlockBits(data);

                using (Bitmap resized = Resize(bmp, 700))
                {
                    resized.Save(destPath, ImageFormat.Png);
                }
            }
        }
    }

    private static void Seed(byte[] buf, int stride, bool[] isBackground, Queue<int> queue, int w, int h, int x, int y, int keyMin)
    {
        int index = y * w + x;

        if (isBackground[index])
        {
            return;
        }

        int i = y * stride + x * 4;
        int b = buf[i];
        int g = buf[i + 1];
        int r = buf[i + 2];

        if (Math.Min(r, Math.Min(g, b)) < keyMin)
        {
            return;
        }

        isBackground[index] = true;
        queue.Enqueue(index);
    }

    private static bool TouchesBackground(bool[] isBackground, int w, int h, int x, int y)
    {
        if (x > 0 && isBackground[y * w + x - 1]) return true;
        if (x < w - 1 && isBackground[y * w + x + 1]) return true;
        if (y > 0 && isBackground[(y - 1) * w + x]) return true;
        if (y < h - 1 && isBackground[(y + 1) * w + x]) return true;

        return false;
    }

    private static Bitmap Resize(Bitmap source, int width)
    {
        int height = (int)Math.Round(source.Height * (width / (double)source.Width));
        Bitmap target = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        Graphics g = Graphics.FromImage(target);
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
        g.Clear(Color.Transparent);
        g.DrawImage(source, 0, 0, width, height);
        g.Dispose();

        return target;
    }
}
'@

Add-Type -TypeDefinition $code -ReferencedAssemblies System.Drawing

$src = Join-Path $env:USERPROFILE 'Downloads\KShop.png'
$dest = 'd:\ASP .NET\E-COMMERCE\wwwroot\images\kshop-logo.png'

[LogoKeyer]::MakeTransparent($src, $dest, 244, 228)

$image = [System.Drawing.Image]::FromFile($dest)
"$($image.Width)x$($image.Height) | $([math]::Round((Get-Item $dest).Length / 1024))KB"
$image.Dispose()
