using System.IO;
using System.IO.Compression;
using System.Linq;
using ArcSysLib.Core.IO.File.ArcSys;
using VFSILib.Common.Enum;
using VFSILib.Core.IO;

namespace ArcSysLib.Core.IO.File;

public class SEGSFileInfo : ArcSysFileSystemInfo
{
    public SEGSFileInfo(string path, bool preCheck = true) : base(path, preCheck)
    {
        if (preCheck)
            InitGetHeader();
    }

    public SEGSFileInfo(string path, ulong length, ulong offset, ArcSysDirectoryInfo parent,
        bool preCheck = true) :
        base(path, length,
            offset, parent, preCheck)
    {
        if (preCheck)
            InitGetHeader();
    }

    public SEGSFileInfo(MemoryStream memstream, string name = "Memory", bool preCheck = true) : base(memstream,
        name,
        preCheck)
    {
        if (preCheck)
            InitGetHeader();
    }

    public SEGS SEGSFile { get; } = new();

    public bool IsValidSEGS => MagicBytes.Take(4).SequenceEqual(SEGS.MagicBytes);

    private void InitGetHeader(bool onlyHeader = false)
    {
        var stream = GetReadStream(onlyHeader);
        if (stream == null)
            return;
        using (stream)
        {
            if (FileLength < 12)
                return;

            try
            {
                using var reader = new EndiannessAwareBinaryReader(stream, Endianness);
                var origPos = reader.BaseStream.Position;
                reader.BaseStream.Seek(6, SeekOrigin.Current);

                if (!EndiannessChecked)
                {
                    var chunks = reader.ReadInt16();
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    var fileLength = reader.ReadUInt32();
                    if (fileLength <= FileLength && chunks > 0)
                    {
                        reader.BaseStream.Seek(-8, SeekOrigin.Current);
                        uint chunkSizeTotal = 0;
                        uint lastChunkTotal = 0;
                        reader.BaseStream.Seek(2, SeekOrigin.Current);
                        for (var i = 0; i < chunks; i++)
                        {
                            reader.BaseStream.Seek(6, SeekOrigin.Current);
                            var chunkZSize = reader.ReadUInt16();
                            chunkSizeTotal += chunkZSize;

                            if (i == chunks - 1)
                            {
                                reader.BaseStream.Seek(2, SeekOrigin.Current);
                                lastChunkTotal = chunkZSize + reader.ReadUInt32();
                            }
                        }

                        if (!(chunkSizeTotal < fileLength && lastChunkTotal < fileLength))
                        {
                            Endianness = ByteOrder.BigEndian;
                            reader.ChangeEndianness(Endianness);
                        }
                    }
                    else
                    {
                        Endianness = ByteOrder.BigEndian;
                        reader.ChangeEndianness(Endianness);
                    }

                    EndiannessChecked = true;
                }

                reader.BaseStream.Position = origPos;

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
        if (!IsValidSEGS) return;

        SEGSFile.Flags = reader.ReadInt16();
        SEGSFile.Chunks = new SEGS.Chunk[reader.ReadInt16()];
        SEGSFile.FullSize = reader.ReadUInt32();
        SEGSFile.CompressedSize = reader.ReadUInt32();

        for (var i = 0; i < SEGSFile.Chunks.Length; i++)
            SEGSFile.Chunks[i] = new SEGS.Chunk
            {
                ZSize = reader.ReadUInt16(),
                Size = reader.ReadUInt16(),
                Offset = reader.ReadUInt32() - 1
            };
    }

    public MemoryStream Decompress()
    {
        if (NoAccess)
            return null;

        if (!Initialized)
        {
            Initialize();
            InitGetHeader();
        }

        if (!IsValidSEGS)
            return null;

        try
        {
            using var reader = new EndiannessAwareBinaryReader(GetReadStream(), Endianness);
            var beginPos = reader.BaseStream.Position;
            reader.BaseStream.Seek(0x10 + SEGSFile.Chunks.Length * 8, SeekOrigin.Current);
            var pos = reader.BaseStream.Position;
            var workAround = 0;

            var decompressStream = new MemoryStream(new byte[SEGSFile.FullSize]);
            for (var i = 0; i < SEGSFile.Chunks.Length; i++)
            {
                if (i == 0)
                    if (SEGSFile.Chunks[i].Offset == 0)
                        workAround = 1;

                if (workAround != 0) SEGSFile.Chunks[i].Offset += pos;

                if (SEGSFile.Chunks[i].Size == 0) SEGSFile.Chunks[i].Size = 0x00010000;

                if (SEGSFile.Chunks[i].Size == SEGSFile.Chunks[i].ZSize)
                {
                    var savPos = reader.BaseStream.Position;
                    reader.BaseStream.Seek(SEGSFile.Chunks[i].Offset, SeekOrigin.Begin);
                    decompressStream.Write(reader.ReadBytes(SEGSFile.Chunks[i].Size), 0,
                        SEGSFile.Chunks[i].Size);
                    reader.BaseStream.Seek(savPos, SeekOrigin.Begin);
                }
                else
                {
                    var savPos = reader.BaseStream.Position;
                    reader.BaseStream.Seek(beginPos + SEGSFile.Chunks[i].Offset, SeekOrigin.Begin);
                    using (var decodeStream =
                           Inflate(new MemoryStream(reader.ReadBytes(SEGSFile.Chunks[i].ZSize,
                               ByteOrder.LittleEndian))))
                    {
                        decodeStream.CopyTo(decompressStream);
                    }

                    reader.BaseStream.Seek(savPos, SeekOrigin.Begin);
                }
            }

            reader.Close();

            return decompressStream;
        }
        catch
        {
            return null;
        }
    }

    private static Stream Inflate(MemoryStream stream)
    {
        using Stream input = new DeflateStream(stream,
            CompressionMode.Decompress);
        using var output = new MemoryStream();
        input.CopyTo(output);
        return new MemoryStream(output.ToArray());
    }
}