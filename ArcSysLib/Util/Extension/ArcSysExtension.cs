using System.Drawing.Imaging;
using ArcSysLib.Core.ArcSys;

namespace ArcSysLib.Util.Extension;

public static class ArcSysExtension
{
    public static PixelFormat GetPixelFormat(this HIP.Format format)
    {
        return format switch
        {
            HIP.Format.Format8bppIndexed => PixelFormat.Format8bppIndexed,
            HIP.Format.Format16bppGrayScale => PixelFormat.Format16bppGrayScale,
            HIP.Format.Format32bppArgb => PixelFormat.Format32bppArgb,
            HIP.Format.Format16bppRgb565 => PixelFormat.Format16bppRgb565,
            _ => PixelFormat.Undefined
        };
    }

    public static HIP.Format GetHIPFormat(this PixelFormat format)
    {
        return format switch
        {
            PixelFormat.Format8bppIndexed => HIP.Format.Format8bppIndexed,
            PixelFormat.Format16bppGrayScale => HIP.Format.Format16bppGrayScale,
            PixelFormat.Format32bppArgb => HIP.Format.Format32bppArgb,
            PixelFormat.Format16bppRgb565 => HIP.Format.Format16bppRgb565,
            _ => HIP.Format.Unknown
        };
    }
}