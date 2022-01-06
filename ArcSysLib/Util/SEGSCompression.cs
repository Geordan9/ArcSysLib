using System.IO;
using System.IO.Compression;
using System.Text;
using ArcSysLib.Core;
using VFSILib.Common.Enum;
using VFSILib.Core.IO;

namespace ArcSysLib.Util;

public static class SEGSCompression
{
    public static Stream DecompressStream(Stream stream, ByteOrder endianness)
    {
        using var reader = new EndiannessAwareBinaryReader(stream, Encoding.Default, true, endianness);
        var segs = new SEGS();
        var beginPos = reader.BaseStream.Position;
        var magicBytes = reader.ReadChars(4);

        segs.Flags = reader.ReadInt16();
        segs.Chunks = new SEGS.Chunk[reader.ReadInt16()];
        segs.FullSize = reader.ReadUInt32();
        segs.CompressedSize = reader.ReadUInt32();

        var pos = beginPos + segs.Chunks.Length * (2 + 2 + 4);
        var workAround = 0;

        var decompressStream = new MemoryStream(new byte[segs.FullSize]);

        for (var i = 0; i < segs.Chunks.Length; i++)
        {
            segs.Chunks[i] = new SEGS.Chunk
            {
                ZSize = reader.ReadUInt16(),
                Size = reader.ReadUInt16(),
                Offset = reader.ReadUInt32() - 1
            };

            if (i == 0)
                if (segs.Chunks[i].Offset == 0)
                    workAround = 1;

            if (workAround != 0) segs.Chunks[i].Offset += pos;

            if (segs.Chunks[i].Size == 0) segs.Chunks[i].Size = 0x00010000;

            if (segs.Chunks[i].Size == segs.Chunks[i].ZSize)
            {
                var savPos = reader.BaseStream.Position;
                reader.BaseStream.Seek(segs.Chunks[i].Offset, SeekOrigin.Begin);
                decompressStream.Write(reader.ReadBytes(segs.Chunks[i].Size), 0, segs.Chunks[i].Size);
                reader.BaseStream.Seek(savPos, SeekOrigin.Begin);
            }
            else
            {
                var savPos = reader.BaseStream.Position;
                reader.BaseStream.Seek(beginPos + segs.Chunks[i].Offset, SeekOrigin.Begin);
                using (var decodeStream =
                       Decompress(new MemoryStream(reader.ReadBytes(segs.Chunks[i].ZSize,
                           ByteOrder.LittleEndian)))
                      )
                {
                    decodeStream.CopyTo(decompressStream);
                }

                reader.BaseStream.Seek(savPos, SeekOrigin.Begin);
            }
        }

        decompressStream.Position = 0;

        return decompressStream;
    }

    private static Stream Decompress(MemoryStream stream)
    {
        using Stream input = new DeflateStream(stream,
            CompressionMode.Decompress);
        using var output = new MemoryStream();
        input.CopyTo(output);
        return new MemoryStream(output.ToArray());
    }
}