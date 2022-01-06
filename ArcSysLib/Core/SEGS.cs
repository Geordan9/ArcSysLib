namespace ArcSysLib.Core;

public class SEGS
{
    public static byte[] MagicBytes { get; } = {0x73, 0x65, 0x67, 0x73};

    public short Flags { get; set; }

    public Chunk[] Chunks { get; set; }

    public uint FullSize { get; set; }

    public uint CompressedSize { get; set; }

    public struct Chunk
    {
        public int ZSize;
        public int Size;
        public long Offset;
    }
}