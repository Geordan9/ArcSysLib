using System;

namespace ArcSysLib.Core.ArcSys;

public class PAC
{
    [Flags]
    public enum Parameters : uint
    {
        FileHeaderEndPadding = 0x10,
        NoByteAlignment = 0x40000000,
        GenerateNameID = 0x80000000,
        GenerateExtendedNameID = 0xA0000000
    }

    public static byte[] MagicBytes { get; } = {0x46, 0x50, 0x41, 0x43};

    public uint HeaderSize { get; set; }

    public uint FileLength { get; set; }

    public uint FileCount { get; set; }

    public Parameters Params { get; set; }

    public int FileNameLength { get; set; }

    public File[] Files { get; set; }

    public struct File
    {
        public string name;
        public uint index;
        public uint offset;
        public uint length;
        public byte[] bytes;
    }
}