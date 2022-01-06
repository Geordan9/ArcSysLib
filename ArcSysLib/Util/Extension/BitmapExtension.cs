using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace ArcSysLib.Util.Extension;

public static class BitmapExtension
{
    public static byte[] GetPixels(this Bitmap bmp)
    {
        var Bpp = Image.GetPixelFormatSize(bmp.PixelFormat) >> 3;
        var bitmapData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly,
            bmp.PixelFormat);
        var length = bitmapData.Stride * bitmapData.Height;
        var pixels = new byte[length];
        Marshal.Copy(bitmapData.Scan0, pixels, 0, length);
        bmp.UnlockBits(bitmapData);
        var widthBpp = bitmapData.Width * Bpp;
        var fixedPixels = new byte[widthBpp * bitmapData.Height];
        var fpIndex = 0;
        for (var i = 0; i < pixels.Length; i++)
        {
            if (i % bitmapData.Stride >= widthBpp)
                continue;
            fixedPixels[fpIndex] = pixels[i];
            fpIndex++;
        }

        return fixedPixels;
    }
}