using System.Drawing;
using System.IO;
using System.Linq;
using ArcSysLib.Core.ArcSys;
using VFSILib.Common.Enum;
using VFSILib.Core.IO;

namespace ArcSysLib.Core.IO.File.ArcSys;

public class HPLFileInfo : ArcSysFileInfo
{
    public HPLFileInfo(string path, bool preCheck = true) : base(path, preCheck)
    {
        if (preCheck)
            InitGetHeader();
    }

    public HPLFileInfo(string path, ulong length, ulong offset, ArcSysDirectoryInfo parent, bool preCheck = true) :
        base(path, length,
            offset, parent, preCheck)
    {
        if (preCheck)
            InitGetHeader();
    }

    public HPLFileInfo(MemoryStream memstream, string name = "Memory", bool preCheck = true) : base(memstream, name,
        preCheck)
    {
        if (preCheck)
            InitGetHeader();
    }

    public HPLFileInfo(Color[] colors, ByteOrder endianness = ByteOrder.LittleEndian) : base(
        new MemoryStream(new byte[0]))
    {
        Endianness = endianness;
        CreateHPL(colors);
    }

    public HPL HPLFile { get; } = new();

    public Color[] Palette
    {
        get
        {
            if (HPLFile.Palette != null)
                return HPLFile.Palette;

            return HPLFile.Palette = GetPalette();
        }
        private set => HPLFile.Palette = value;
    }

    public bool IsValidHPL => MagicBytes.SequenceEqual(HPL.MagicBytes);

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
        MagicBytes = reader.ReadBytes(0x4, ByteOrder.LittleEndian);
        if (!IsValidHPL) return;

        reader.BaseStream.Seek(0x4, SeekOrigin.Current);
        FileLength = HPLFile.FileLength = reader.ReadUInt32();
        if (FileLength > ParentDefinedLength)
            FileLength = ParentDefinedLength;
        HPLFile.ColorRange = reader.ReadUInt32();
        reader.BaseStream.Seek(0x10, SeekOrigin.Current);
    }

    private Color[] GetPalette()
    {
        if (NoAccess || !IsValidHPL)
            return null;

        if (!Initialized)
        {
            Initialize();
            InitGetHeader();
        }

        using var reader = new EndiannessAwareBinaryReader(GetReadStream(), Endianness);
        reader.BaseStream.Seek(0x20, SeekOrigin.Current);

        var colors = new Color[HPLFile.ColorRange];

        for (var i = 0; i < colors.Length; i++)
        {
            var bytes = reader.ReadBytes(4);
            colors[i] = Color.FromArgb(bytes[3], bytes[2], bytes[1], bytes[0]);
        }

        reader.Close();
        return colors;
    }

    private void CreateHPL(Color[] colors)
    {
        if (colors == null || colors.Length == 0)
            return;

        Palette = colors;
        HPLFile.ColorRange = (uint) Palette.Length;


        FileLength = HPLFile.FileLength = HPLFile.ColorRange * 4 + 0x20;
        VFSIBytes = new byte[HPLFile.FileLength];
        using var writer = new EndiannessAwareBinaryWriter(new MemoryStream(VFSIBytes), Endianness);
        writer.Write(HPL.MagicBytes, ByteOrder.LittleEndian);
        writer.Write(0x125);
        writer.Write(HPLFile.FileLength);
        writer.Write(HPLFile.ColorRange);
        writer.Write(0);
        writer.Write(0);
        writer.Write(0x10000001);
        writer.Write(0);
        foreach (var color in Palette) writer.Write(color.ToArgb());
    }
}