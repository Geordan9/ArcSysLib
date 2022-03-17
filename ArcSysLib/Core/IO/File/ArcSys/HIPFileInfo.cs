using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using ArcSysLib.Core.ArcSys;
using ArcSysLib.Util;
using ArcSysLib.Util.Extension;
using VFSILib.Common.Enum;
using VFSILib.Core.IO;

namespace ArcSysLib.Core.IO.File.ArcSys;

public class HIPFileInfo : ArcSysFileInfo
{
    public HIPFileInfo(string path, bool preCheck = true) : base(path, preCheck)
    {
        var ext = Path.GetExtension(path).ToLower();
        var isNativeImage = ImageTools.NativeImageExtensions.Contains(ext);
        var isDDS = ext == ".dds";
        if (isNativeImage || isDDS)
        {
            Bitmap bmp = null;
            if (isNativeImage)
                bmp = (Bitmap) Image.FromFile(path, true);
            else if (isDDS) bmp = new DDSFileInfo(path).GetImage();
            CreateHIP(bmp);
        }
        else if (preCheck)
        {
            InitGetHeader();
        }
    }

    public HIPFileInfo(string path, HIP.Encoding hipEncoding, bool layeredImage = false, int offsetX = 0,
        int offsetY = 0, int canvasWidth = 0, int canvasHeight = 0, Color[] importedPalette = null,
        ByteOrder endianness = ByteOrder.LittleEndian) : base(path)
    {
        Palette = importedPalette;
        Endianness = endianness;
        var ext = Path.GetExtension(path).ToLower();
        Bitmap bmp = null;
        var native = ImageTools.NativeImageExtensions.Contains(ext);
        if (native)
            bmp = (Bitmap) Image.FromFile(path, true);
        else if (ext == ".dds") bmp = new DDSFileInfo(path).GetImage();

        CreateHIP(bmp, hipEncoding, layeredImage, offsetX, offsetY, canvasWidth, canvasHeight);
    }

    public HIPFileInfo(string path, HIP.Encoding hipEncoding, ref HIP refHIPFile, Color[] importedPalette = null,
        ByteOrder endianness = ByteOrder.LittleEndian) : this(path, hipEncoding,
        refHIPFile.Params.extraParams.HasFlag(HIP.ExtraParams.RenderableLayers) &&
        refHIPFile.Params.layeredImages != 0,
        refHIPFile.OffsetX, refHIPFile.OffsetY, refHIPFile.CanvasWidth,
        refHIPFile.CanvasHeight, importedPalette, endianness)
    {
    }

    public HIPFileInfo(Bitmap bmp, Color[] importedPalette = null, ByteOrder endianness = ByteOrder.LittleEndian) :
        base(string.Empty, false)
    {
        Palette = importedPalette;
        Endianness = endianness;
        CreateHIP(bmp);
    }

    public HIPFileInfo(Bitmap bmp, HIP.Encoding hipEncoding, bool layeredImage = false, int offsetX = 0,
        int offsetY = 0, int canvasWidth = 0, int canvasHeight = 0, Color[] importedPalette = null,
        ByteOrder endianness = ByteOrder.LittleEndian) : base(string.Empty)
    {
        Palette = importedPalette;
        Endianness = endianness;
        CreateHIP(bmp, hipEncoding, layeredImage, offsetX, offsetY, canvasWidth, canvasHeight);
    }

    public HIPFileInfo(Bitmap bmp, HIP.Encoding hipEncoding, ref HIP refHIPFile, Color[] importedPalette = null,
        ByteOrder endianness = ByteOrder.LittleEndian) : this(bmp, hipEncoding,
        refHIPFile.Params.extraParams.HasFlag(HIP.ExtraParams.RenderableLayers) &&
        refHIPFile.Params.layeredImages != 0,
        refHIPFile.OffsetX, refHIPFile.OffsetY, refHIPFile.CanvasWidth,
        refHIPFile.CanvasHeight, importedPalette, endianness)
    {
    }

    public HIPFileInfo(string path, ulong length, ulong offset, ArcSysDirectoryInfo parent, bool preCheck = true) :
        base(path, length,
            offset, parent, preCheck)
    {
        if (preCheck)
            InitGetHeader();
    }

    public HIPFileInfo(MemoryStream memstream, string name = "Memory", bool preCheck = true) : base(memstream, name,
        preCheck)
    {
        if (preCheck)
            InitGetHeader();
    }

    // Properties

    public HIP HIPFile { get; } = new();

    public Color[] Palette
    {
        get
        {
            if (HIPFile.Palette != null)
                return HIPFile.Palette;

            return GetPalette();
        }
        private set => HIPFile.Palette = value;
    }

    public bool IsValidHIP => MagicBytes.SequenceEqual(HIP.MagicBytes);

    // Methods

    private void CheckEndianness(byte[] bytes)
    {
        if (Endianness == ByteOrder.LittleEndian)
            if (bytes[0] == 0x0)
                Endianness = ByteOrder.BigEndian;
        EndiannessChecked = true;
    }

    private void InitGetHeader()
    {
        var stream = GetReadStream(true);
        if (stream == null)
            return;
        using (stream)
        {
            if (FileLength < 32)
                return;

            try
            {
                using var reader = new EndiannessAwareBinaryReader(stream, Endianness);
                if (!EndiannessChecked)
                {
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    CheckEndianness(reader.ReadBytes(4));
                    reader.ChangeEndianness(Endianness);
                    reader.BaseStream.Seek(-8, SeekOrigin.Current);
                }

                ReadHeaderInfo(reader);
                reader.Close();
            }
            catch
            {
            }
        }
    }

    private void ReadHeaderInfo(EndiannessAwareBinaryReader reader)
    {
        MagicBytes = reader.ReadBytes(4, ByteOrder.LittleEndian);
        if (!IsValidHIP) return;

        reader.BaseStream.Seek(4, SeekOrigin.Current);
        FileLength = HIPFile.FileLength = reader.ReadUInt32();
        if (FileLength > ParentDefinedLength)
            FileLength = ParentDefinedLength;
        HIPFile.ColorRange = reader.ReadUInt32();
        HIPFile.CanvasWidth = reader.ReadInt32();
        HIPFile.CanvasHeight = reader.ReadInt32();

        var paramBytes = reader.ReadBytes(4);

        HIPFile.PixelFormat = ((HIP.Format) paramBytes[0]).GetPixelFormat();

        HIPFile.PixelEncoding = (HIP.Encoding) paramBytes[1];
        var parameters = HIPFile.Params;
        parameters.layeredImages = paramBytes[2];
        parameters.extraParams = (HIP.ExtraParams) paramBytes[3];
        HIPFile.Params = parameters;

        var renderableLayers = parameters.extraParams.HasFlag(HIP.ExtraParams.RenderableLayers);

        var layerHeaderSize = reader.ReadUInt32();
        HIPFile.HeaderSize = 0x20 + layerHeaderSize;

        var isPaletteImage = HIPFile.PixelFormat == PixelFormat.Format8bppIndexed;

        if (renderableLayers && HIPFile.Params.layeredImages != 0)
        {
            HIPFile.ImageWidth = reader.ReadInt32();
            HIPFile.ImageHeight = reader.ReadInt32();
            HIPFile.OffsetX = reader.ReadInt32();
            HIPFile.OffsetY = reader.ReadInt32();
            layerHeaderSize -= 0x10;
        }
        else
        {
            HIPFile.ImageWidth = HIPFile.CanvasWidth;
            HIPFile.ImageHeight = HIPFile.CanvasHeight;
            HIPFile.OffsetX = HIPFile.OffsetY = 0;
        }

        reader.BaseStream.Seek(layerHeaderSize, SeekOrigin.Current);
        if (isPaletteImage)
            reader.BaseStream.Seek(4 * HIPFile.ColorRange, SeekOrigin.Current);

        var savpos = reader.BaseStream.Position;
        if (reader.ReadInt32(ByteOrder.LittleEndian) == 0x73676573)
        {
            FileLength = ParentDefinedLength;
        }
        else
        {
            reader.BaseStream.Seek(12, SeekOrigin.Current);
            if (reader.BaseStream.Position != reader.BaseStream.Length)
                FileLength = ParentDefinedLength;
            else
                reader.BaseStream.Position = savpos;
        }
    }

    public Bitmap GetImage()
    {
        return GetImage(false, null);
    }

    public Bitmap GetImage(bool keepCanvas)
    {
        return GetImage(keepCanvas, null);
    }

    public Bitmap GetImage(Color[] importedPalette)
    {
        return GetImage(false, importedPalette);
    }

    public Bitmap GetImage(bool keepCanvas, Color[] importedPalette)
    {
        if (NoAccess)
            return null;

        if (!Initialized)
        {
            Initialize();
            InitGetHeader();
        }

        if (!IsValidHIP)
            return null;

        var readStream = GetReadStream();
        var reader = new EndiannessAwareBinaryReader(readStream, Encoding.Default, true, Endianness);
        try
        {
            reader.BaseStream.Seek(HIPFile.HeaderSize, SeekOrigin.Current);
            var isPaletteImage = HIPFile.PixelFormat == PixelFormat.Format8bppIndexed;
            if (isPaletteImage)
            {
                if (importedPalette == null)
                {
                    var colors = new Color[HIPFile.ColorRange];

                    if (HIPFile.ColorRange > 0)
                    {
                        for (var i = 0; i < HIPFile.ColorRange; i++)
                        {
                            var bytes = reader.ReadBytes(4);
                            colors[i] = Color.FromArgb(bytes[3], bytes[2], bytes[1], bytes[0]);
                        }
                    }
                    else if (HIPFile.MissingPalette)
                    {
                        colors = new Color[256];
                        colors[0] = Color.Transparent;
                        for (var i = 1; i < colors.Length; i++)
                            colors[i] = Color.Black;
                    }

                    if (colors.Length > 256)
                        Array.Resize(ref colors, 256);
                    Palette = colors;
                }
                else
                {
                    reader.BaseStream.Seek(HIPFile.ColorRange * 4, SeekOrigin.Current);
                    Palette = importedPalette;
                }
            }

            var savpos = reader.BaseStream.Position;

            if (reader.ReadInt32(ByteOrder.LittleEndian) == 0x73676573)
            {
                Endianness = ByteOrder.BigEndian;
                reader.BaseStream.Seek(-4, SeekOrigin.Current);
                reader.Close();
                reader.Dispose();
                reader = new EndiannessAwareBinaryReader(
                    SEGSCompression.DecompressStream(readStream, Endianness), Endianness);
            }
            else
            {
                reader.BaseStream.Seek(12, SeekOrigin.Current);
                if (reader.BaseStream.Position != reader.BaseStream.Length)
                {
                    if (reader.ReadInt32(ByteOrder.LittleEndian) == 0x73676573)
                    {
                        Endianness = ByteOrder.BigEndian;
                        reader.BaseStream.Seek(-4, SeekOrigin.Current);
                        reader.Close();
                        reader.Dispose();
                        reader = new EndiannessAwareBinaryReader(
                            SEGSCompression.DecompressStream(readStream, Endianness), Endianness);
                    }
                    else
                    {
                        reader.BaseStream.Seek(-20, SeekOrigin.Current);
                    }
                }
                else
                {
                    reader.BaseStream.Position = savpos;
                }
            }

            byte[] pixels = null;

            switch (HIPFile.PixelEncoding)
            {
                case HIP.Encoding.Key:
                    pixels = GetPixelsWithKey(reader);
                    break;
                case HIP.Encoding.RawSignRepeat:
                    pixels = GetPixelsRawSignRepeat(reader);
                    break;
                case (HIP.Encoding) 0x4:
                    return null;
                default:
                    pixels = GetPixelsFromRawColors(reader);
                    break;
            }

            var bitmapWidth = HIPFile.ImageWidth;
            var bitmapHeight = HIPFile.ImageHeight;

            if (HIPFile.PixelEncoding == HIP.Encoding.RawCanvas)
            {
                bitmapWidth = HIPFile.CanvasWidth;
                bitmapHeight = HIPFile.CanvasHeight;
            }

            if (isPaletteImage)
                if (HIPFile.Params.layeredImages != 0)
                    RoundCanvasSize();

            Bitmap bitmap;

            if (keepCanvas && HIPFile.PixelEncoding != HIP.Encoding.RawCanvas)
            {
                var Bpp = Image.GetPixelFormatSize(HIPFile.PixelFormat) >> 3;
                var newPixels = new byte[HIPFile.CanvasHeight * HIPFile.CanvasWidth * Bpp];
                var pIndex = 0;
                for (var i = 0; i < HIPFile.CanvasHeight; i++)
                for (var j = 0; j < HIPFile.CanvasWidth; j++)
                {
                    var withinOriginal = i >= HIPFile.OffsetY && j >= HIPFile.OffsetX &&
                                         i < HIPFile.OffsetY + HIPFile.ImageHeight &&
                                         j < HIPFile.OffsetX + HIPFile.ImageWidth;

                    Buffer.BlockCopy(pixels, withinOriginal
                        ? pIndex * Bpp
                        : 0, newPixels, (i * HIPFile.CanvasWidth + j) * Bpp, Bpp);

                    if (withinOriginal) pIndex++;
                }

                bitmap = CreateBitmap(HIPFile.CanvasWidth, HIPFile.CanvasHeight, HIPFile.CanvasWidth,
                    HIPFile.CanvasHeight, newPixels,
                    HIPFile.PixelFormat, Palette);
            }
            else
            {
                bitmap = CreateBitmap(bitmapWidth, bitmapHeight, HIPFile.ImageWidth, HIPFile.ImageHeight, pixels,
                    HIPFile.PixelFormat, Palette);
            }

            return bitmap;
        }
        catch
        {
            NoAccess = true;
            return null;
        }
        finally
        {
            reader.Close();
            reader.Dispose();
            readStream.Close();
            readStream.Dispose();
        }
    }

    private byte[] GetPixelsWithKey(EndiannessAwareBinaryReader reader)
    {
        reader.ChangeEndianness(ByteOrder.LittleEndian);
        var Bpp = Image.GetPixelFormatSize(HIPFile.PixelFormat) >> 3;
        var imageColorBytes = new byte[HIPFile.ImageWidth * HIPFile.ImageHeight * Bpp];
        var position = (ulong) reader.BaseStream.Position;
        var key = reader.ReadByte();
        //var colorSize = (int) reader.ReadByte();
        reader.BaseStream.Seek(1, SeekOrigin.Current);
        var colorSize = Bpp;
        var pos = 0;
        while ((ulong) reader.BaseStream.Position - position < FileLength - HIPFile.HeaderSize
               && reader.BaseStream.Position != reader.BaseStream.Length
               && pos < imageColorBytes.Length)
            try
            {
                var bytesRead = reader.ReadBytes(colorSize);
                if (bytesRead[0] == key)
                {
                    if (bytesRead[0] != bytesRead[1])
                    {
                        bytesRead[1] = bytesRead[1] == 0xFF ? key : bytesRead[1];
                        bytesRead[1]++;
                        for (var i = 0; i < bytesRead[2]; i++)
                        {
                            Buffer.BlockCopy(imageColorBytes, pos - bytesRead[1] * colorSize, imageColorBytes, pos,
                                colorSize);
                            pos += colorSize;
                        }

                        reader.BaseStream.Seek(-1, SeekOrigin.Current);
                    }
                    else
                    {
                        reader.BaseStream.Seek(-3, SeekOrigin.Current);
                        bytesRead = reader.ReadBytes(colorSize);
                        if (Endianness == ByteOrder.BigEndian)
                            Array.Reverse(bytesRead);
                        Buffer.BlockCopy(bytesRead, 0, imageColorBytes, pos, colorSize);
                        pos += colorSize;
                    }
                }
                else
                {
                    if (Endianness == ByteOrder.BigEndian)
                        Array.Reverse(bytesRead);
                    Buffer.BlockCopy(bytesRead, 0, imageColorBytes, pos, colorSize);
                    pos += colorSize;
                }
            }
            catch
            {
            }

        return imageColorBytes;
    }

    private byte[] GetPixelsFromRawColors(EndiannessAwareBinaryReader reader)
    {
        var renderCanvas = HIPFile.Params.extraParams == 0 || HIPFile.PixelEncoding == HIP.Encoding.RawCanvas;
        var width = renderCanvas ? HIPFile.CanvasWidth : HIPFile.ImageWidth;
        var height = renderCanvas ? HIPFile.CanvasHeight : HIPFile.ImageHeight;
        var Bpp = Image.GetPixelFormatSize(HIPFile.PixelFormat) >> 3;
        var imageColorBytes = new byte[width * height * Bpp];
        var index = 0;
        var position = (ulong) reader.BaseStream.Position;
        var repeat = HIPFile.PixelEncoding == HIP.Encoding.RawRepeat;
        while ((ulong) reader.BaseStream.Position - position < (ulong) imageColorBytes.Length &&
               reader.BaseStream.Position != reader.BaseStream.Length &&
               index < imageColorBytes.Length)
        {
            var bytes = reader.ReadBytes(Bpp);
            var repeatCount = repeat ? reader.ReadByte() : 1;
            for (var i = 0; i < repeatCount && index < imageColorBytes.Length; i++)
            {
                Buffer.BlockCopy(bytes, 0, imageColorBytes, index, Bpp);
                index += Bpp;
            }
        }

        /*if (PixelEncoding == HIPEncoding.RawCanvas && PixelFormat == PixelFormat.Format8bppIndexed)
            OffsetX = OffsetY = 0;*/

        return imageColorBytes;
    }

    private byte[] GetPixelsRawSignRepeat(BinaryReader reader)
    {
        var Bpp = Image.GetPixelFormatSize(HIPFile.PixelFormat) >> 3;
        var imageColorBytes = new byte[HIPFile.ImageWidth * HIPFile.ImageHeight * Bpp];
        var position = (ulong) reader.BaseStream.Position;
        var pos = 0;
        while ((ulong) reader.BaseStream.Position - position < FileLength - HIPFile.HeaderSize &&
               reader.BaseStream.Position != reader.BaseStream.Length)
        {
            var val = reader.ReadInt32();
            if (val < 0)
            {
                val &= 0x7FFFFFFF;
                for (var i = 0; i < val; i++)
                {
                    Buffer.BlockCopy(reader.ReadBytes(Bpp), 0, imageColorBytes, pos, Bpp);
                    pos += Bpp;
                }
            }
            else
            {
                var bytes = reader.ReadBytes(Bpp);
                for (var i = 0; i < val; i++)
                {
                    Buffer.BlockCopy(bytes, 0, imageColorBytes, pos, Bpp);
                    pos += Bpp;
                }
            }
        }

        return imageColorBytes;
    }

    /*private byte[] GetPixelsQuadBlock(BinaryReader reader)
    {
        var Bpp = Image.GetPixelFormatSize(PixelFormat) >> 3;
        var imageColorBytes = new byte[ImageWidth * ImageHeight * Bpp];
        var position = (ulong) reader.BaseStream.Position;
        var pos = 0;
        while ((ulong) reader.BaseStream.Position - position < FileLength - HeaderSize &&
               reader.BaseStream.Position != reader.BaseStream.Length)
        {
        }

        return imageColorBytes;
    }*/

    /*private static Bitmap CreateBitmap(int width, int height, byte[] rawData, PixelFormat format,
        Color[] palette = null)
    {
        return CreateBitmap(width, height, width, height, rawData, format, palette);
    }*/

    private static Bitmap CreateBitmap(int width, int height, int oWidth, int oHeight, byte[] rawData,
        PixelFormat format,
        Color[] palette = null)
    {
        var isGrayScale = format == PixelFormat.Format16bppGrayScale;
        var pixelFormat = isGrayScale ? PixelFormat.Format48bppRgb : format;

        var bitmap = new Bitmap(width, height, pixelFormat);

        if (isGrayScale)
            rawData = Convert16BitGrayScaleToRgb48(rawData, width, height);

        if (pixelFormat == PixelFormat.Format8bppIndexed && palette != null)
        {
            var cp = bitmap.Palette;
            for (var i = 0; i < palette.Length; i++) cp.Entries[i] = palette[i];

            bitmap.Palette = cp;
        }

        var data = bitmap.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.WriteOnly, pixelFormat);
        var scan = data.Scan0;

        var Bpp = Image.GetPixelFormatSize(pixelFormat) >> 3;

        for (var y = 0; y < height; y++)
            Marshal.Copy(rawData, y * width * Bpp, scan + data.Stride * y, width * Bpp);

        bitmap.UnlockBits(data);

        if (width != oWidth || height != oHeight)
        {
            var newBitmap = bitmap.Clone(new Rectangle(0, 0, oWidth, oHeight), format);
            bitmap.Dispose();
            bitmap = newBitmap;
        }

        return bitmap;
    }

    private static byte[] Convert16BitGrayScaleToRgb48(byte[] inBuffer, int width, int height)
    {
        var inBytesPerPixel = 2;
        var outBytesPerPixel = 6;

        var outBuffer = new byte[width * height * outBytesPerPixel];
        var inStride = width * inBytesPerPixel;
        var outStride = width * outBytesPerPixel;

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var inIndex = y * inStride + x * inBytesPerPixel;
            var outIndex = y * outStride + x * outBytesPerPixel;

            var hibyte = inBuffer[inIndex + 1];
            var lobyte = inBuffer[inIndex];

            //R
            outBuffer[outIndex] = lobyte;
            outBuffer[outIndex + 1] = hibyte;

            //G
            outBuffer[outIndex + 2] = lobyte;
            outBuffer[outIndex + 3] = hibyte;

            //B
            outBuffer[outIndex + 4] = lobyte;
            outBuffer[outIndex + 5] = hibyte;
        }

        return outBuffer;
    }

    public Color[] GetPalette()
    {
        if (NoAccess || !IsValidHIP)
            return null;

        if (!Initialized)
        {
            Initialize();
            InitGetHeader();
        }

        if (!(HIPFile.PixelFormat == PixelFormat.Format8bppIndexed))
            return null;

        using var reader = new EndiannessAwareBinaryReader(GetReadStream(), Endianness);
        reader.BaseStream.Seek(HIPFile.HeaderSize, SeekOrigin.Current);

        var colors = new Color[HIPFile.ColorRange];

        for (var i = 0; i < colors.Length; i++)
        {
            var bytes = reader.ReadBytes(4);
            colors[i] = Color.FromArgb(bytes[3], bytes[2], bytes[1], bytes[0]);
        }

        reader.Close();
        return colors;
    }

    private void RoundCanvasSize()
    {
        HIPFile.CanvasWidth = HIPFile.CanvasWidth + 1024 - HIPFile.CanvasWidth % 1024;

        HIPFile.CanvasHeight = HIPFile.CanvasHeight + 1024 - HIPFile.CanvasHeight % 1024;

        var w = HIPFile.OffsetX + HIPFile.ImageWidth - HIPFile.CanvasWidth;
        var h = HIPFile.OffsetY + HIPFile.ImageHeight - HIPFile.CanvasHeight;

        HIPFile.CanvasWidth += w + 1024 - w % 1024;

        HIPFile.CanvasHeight += h + 1024 - h % 1024;
    }

    private void CreateHIP(Bitmap bmp, HIP.Encoding hipEncoding = HIP.Encoding.Raw,
        bool layeredImage = false, int offsetX = 0, int offsetY = 0, int canvasWidth = 0, int canvasHeight = 0)
    {
        if (bmp == null)
            return;

        var pixels = new byte[0];

        using (bmp)
        {
            pixels = bmp.GetPixels();
            HIPFile.ImageWidth = bmp.Width;
            HIPFile.ImageHeight = bmp.Height;
            HIPFile.CanvasWidth = canvasWidth == 0 ? HIPFile.ImageWidth : canvasWidth;
            HIPFile.CanvasHeight = canvasHeight == 0 ? HIPFile.ImageHeight : canvasHeight;
            HIPFile.OffsetX = offsetX;
            HIPFile.OffsetY = offsetY;
            HIPFile.PixelFormat = bmp.PixelFormat;
            Palette ??= bmp.Palette.Entries;
            HIPFile.ColorRange = (uint) Palette.Length;
            HIPFile.PixelEncoding = hipEncoding;
        }

        var Bpp = Image.GetPixelFormatSize(HIPFile.PixelFormat) >> 3;

        if (layeredImage && (canvasWidth == 0 || canvasHeight == 0)) RoundCanvasSize();

        using var fileMemoryStream = new MemoryStream();
        using (var writer =
               new EndiannessAwareBinaryWriter(fileMemoryStream, Encoding.Default, true, Endianness))
        {
            writer.Write(HIP.MagicBytes, ByteOrder.LittleEndian);
            writer.Write(0x125);
            writer.Write(0);
            writer.Write(HIPFile.ColorRange);
            writer.Write(HIPFile.CanvasWidth);
            writer.Write(HIPFile.CanvasHeight);
            writer.Write((byte) (
                HIPFile.PixelFormat == PixelFormat.Format8bppIndexed ? 0x1 :
                HIPFile.PixelFormat == PixelFormat.Format16bppGrayScale ? 0x4 :
                HIPFile.PixelFormat == PixelFormat.Format16bppRgb565 ? 0x40 :
                0x10));
            writer.Write((byte) HIPFile.PixelEncoding);
            writer.Write(layeredImage);
            writer.Write((byte) (layeredImage ? 0x20 : 0x0));
            if (layeredImage)
            {
                writer.Write(0x20);
                writer.Write(HIPFile.ImageWidth);
                writer.Write(HIPFile.ImageHeight);
                writer.Write(HIPFile.OffsetX);
                writer.Write(HIPFile.OffsetY);
                writer.Write(new byte[16]);
            }
            else
            {
                writer.Write(0);
            }

            if (Palette.Length > 0)
                foreach (var color in Palette)
                    writer.Write(color.ToArgb());
            switch (HIPFile.PixelEncoding)
            {
                default: // Raw + RawRepeat
                    var repeat = HIPFile.PixelEncoding == HIP.Encoding.RawRepeat;
                    var repeatCount = 1;
                    for (var i = 0; i < pixels.Length; i += Bpp)
                    {
                        var colorBytes = new byte[Bpp];
                        Buffer.BlockCopy(pixels, i, colorBytes, 0, Bpp);
                        if (repeat)
                        {
                            var same = false;
                            if (i < pixels.Length - Bpp)
                            {
                                var nextColorBytes = new byte[Bpp];
                                Buffer.BlockCopy(pixels, i + Bpp, nextColorBytes, 0, Bpp);
                                same = nextColorBytes.SequenceEqual(colorBytes);
                                if (same)
                                {
                                    repeatCount++;
                                    if (repeatCount == 256)
                                    {
                                        repeatCount--;
                                        same = false;
                                    }
                                }
                            }

                            if (!same)
                            {
                                writer.Write(colorBytes);
                                writer.Write((byte) repeatCount);
                                repeatCount = 1;
                            }
                        }
                        else
                        {
                            writer.Write(colorBytes);
                        }
                    }

                    break;
            }

            writer.BaseStream.Position = 8;
            writer.Write((int) writer.BaseStream.Length);
            writer.BaseStream.Position = 0;
            writer.Close();
        }

        VFSIBytes = fileMemoryStream.ToArray();
        FileLength = HIPFile.FileLength = (uint) VFSIBytes.Length;
    }
}