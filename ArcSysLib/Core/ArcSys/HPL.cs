using System.Drawing;

namespace ArcSysLib.Core.ArcSys;

public class HPL
{
    public static byte[] MagicBytes { get; } = {0x48, 0x50, 0x41, 0x4C};

    public uint FileLength { get; set; }

    public uint ColorRange { get; set; }

    public Color[] Palette { get; set; }
}