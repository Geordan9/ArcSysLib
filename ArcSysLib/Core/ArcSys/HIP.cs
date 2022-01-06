using System;
using System.Drawing;
using System.Drawing.Imaging;
using ArcSysLib.Util.Extension;

namespace ArcSysLib.Core.ArcSys;

public class HIP
{
    [Flags]
    public enum Encoding
    {
        Unknown = 0x0,
        RawRepeat = 0x1,
        Key = 0x2,
        Raw = 0x8,
        RawSignRepeat = 0x10,
        RawCanvas = 0x20
    }

    [Flags]
    public enum ExtraParams
    {
        Unknown = 0x2,
        RenderableLayers = 0x20
    }

    [Flags]
    public enum Format
    {
        Unknown = 0x0,
        Format8bppIndexed = 0x1,
        Format16bppGrayScale = 0x4,
        Format32bppArgb = 0x10,
        Format16bppRgb565 = 0x40
    }

    public static byte[] MagicBytes { get; } = {0x48, 0x49, 0x50, 0x00};

    public uint HeaderSize { get; set; }

    public uint FileLength { get; set; }

    public uint ColorRange { get; set; }

    public int CanvasWidth { get; set; }

    public int CanvasHeight { get; set; }

    public Parameters Params { get; set; }

    public int ImageWidth { get; set; }

    public int ImageHeight { get; set; }

    public Color[] Palette { get; set; }

    public int OffsetX { get; set; }

    public int OffsetY { get; set; }

    public PixelFormat PixelFormat
    {
        get => Params.format == Format.Unknown ? PixelFormat.Format32bppArgb : Params.format.GetPixelFormat();
        set
        {
            var _params = Params;
            _params.format = value.GetHIPFormat();
            Params = _params;
        }
    }

    public Encoding PixelEncoding
    {
        get => Params.Encoding;
        set
        {
            var _params = Params;
            _params.Encoding = value;
            Params = _params;
        }
    }

    public bool MissingPalette => (ColorRange == 0) & (PixelFormat == PixelFormat.Format8bppIndexed);

    public struct Parameters
    {
        public Format format;
        public Encoding Encoding;
        public byte layeredImages; // Most likely, but unused.
        public ExtraParams extraParams;
    }
}