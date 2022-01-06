using System;
using System.IO;
using System.Linq;
using System.Text;
using ArcSysLib.Core.ArcSys;
using VFSILib.Common.Enum;
using VFSILib.Core.IO;
using VFSILib.Util.Extension;
using static ArcSysLib.Core.ArcSys.PAC;

namespace ArcSysLib.Core.IO.File.ArcSys;

public class PACFileInfo : ArcSysDirectoryInfo
{
    public PACFileInfo(string path, int MinNameLength = 24, bool preCheck = true) : base(path, preCheck)
    {
        if (!System.IO.File.GetAttributes(path).HasFlag(FileAttributes.Directory))
        {
            if (preCheck)
                InitGetHeader(true);
        }
        else
        {
            PACFile.Params = (Parameters) 0x1;
            CreatePACDataFromFolder(MinNameLength, null);
        }
    }

    public PACFileInfo(string folderPath, Parameters parameters, PACFileOrder pfo = null, int MinNameLength = 24,
        ByteOrder endianness = ByteOrder.LittleEndian) :
        base(folderPath)
    {
        if (!System.IO.File.GetAttributes(folderPath).HasFlag(FileAttributes.Directory))
            throw new Exception("Folder path must be a folder.");

        PACFile.Params = parameters | (Parameters) 0x1 /*Default*/;
        Endianness = endianness;
        CreatePACDataFromFolder(MinNameLength, pfo);
    }

    public PACFileInfo(string path, ulong length, ulong offset, ArcSysDirectoryInfo parent, bool preCheck = true) :
        base(path, length,
            offset, parent, preCheck)
    {
        if (preCheck)
            InitGetHeader();
    }

    public PACFileInfo(MemoryStream memstream, string name = "Memory", bool preCheck = true) : base(memstream, name,
        preCheck)
    {
        if (preCheck)
            InitGetHeader(true);
    }

    public PACFileInfo(ArcSysFileSystemInfo[] virtualFiles, Parameters parameters, PACFileOrder pfo = null,
        int MinNameLength = 24, ByteOrder endianness = ByteOrder.LittleEndian, string name = "Memory") :
        base(new MemoryStream(new byte[0]), name, false)
    {
        PACFile.Params = parameters | (Parameters) 0x1 /*Default*/;
        Endianness = endianness;
        RebuildPACData(virtualFiles, parameters, MinNameLength, pfo);
    }

    public PAC PACFile { get; } = new();

    public bool IsValidPAC => MagicBytes.SequenceEqual(PAC.MagicBytes);

    private void InitGetHeader(bool onlyHeader = false)
    {
        var stream = GetReadStream(onlyHeader);
        if (stream == null)
            return;
        using (stream)
        {
            if (FileLength < 32)
                return;

            try
            {
                using var reader = new EndiannessAwareBinaryReader(stream, Endianness);
                MagicBytes = reader.ReadBytes(4, ByteOrder.LittleEndian);
                if (MagicBytes.SequenceEqual(Util.MagicBytes.BCSM))
                {
                    reader.BaseStream.Seek(12, SeekOrigin.Current);
                    Offset += 16;
                }
                else
                {
                    reader.BaseStream.Seek(-4, SeekOrigin.Current);
                }

                if (Endianness == ByteOrder.LittleEndian && !EndiannessChecked)
                {
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    var headersize = reader.ReadUInt32();
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    var filecount = reader.ReadUInt32();
                    reader.BaseStream.Seek(4, SeekOrigin.Current);
                    var fileentrysize = reader.ReadUInt32() + 12;
                    var remainder = fileentrysize % 16;
                    fileentrysize += remainder == 0 ? remainder : 16 - remainder;
                    var calcheadersize = fileentrysize * filecount + 0x20;
                    var calcheadersize2 = (fileentrysize + 16) * filecount + 0x20;
                    if (calcheadersize != headersize && calcheadersize2 != headersize)
                    {
                        Endianness = ByteOrder.BigEndian;
                        reader.ChangeEndianness(Endianness);
                    }

                    stream.Seek(-24, SeekOrigin.Current);

                    EndiannessChecked = true;
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
        if (!IsValidPAC) return;

        PACFile.HeaderSize = reader.ReadUInt32();
        FileLength = PACFile.FileLength = reader.ReadUInt32();
        PACFile.FileCount = reader.ReadUInt32();

        PACFile.Params = (Parameters) reader.ReadInt32();

        PACFile.FileNameLength = reader.ReadInt32();

        reader.BaseStream.Seek(8, SeekOrigin.Current);

        PACFile.Files = new PAC.File[PACFile.FileCount];

        for (var i = 0; i < PACFile.FileCount; i++)
        {
            PACFile.Files[i] = new PAC.File
            {
                name = Encoding.ASCII
                    .GetString(reader.ReadBytes(PACFile.FileNameLength, ByteOrder.LittleEndian))
                    .Replace("\0", string.Empty),
                index = reader.ReadUInt32(),
                offset = reader.ReadUInt32(),
                length = reader.ReadUInt32()
            };
            var seeklength = (PACFile.FileNameLength + 12) % 16;
            reader.BaseStream.Seek(seeklength == 0 ? seeklength : 16 - seeklength, SeekOrigin.Current);
            if (reader.ReadByte() == 0x0 && seeklength == 0)
                reader.BaseStream.Seek(16, SeekOrigin.Current);
            reader.BaseStream.Seek(-1, SeekOrigin.Current);
        }
    }

    public ArcSysFileSystemInfo[] GetFiles(bool recheck = false)
    {
        if (!Initialized)
        {
            Initialize();
            InitGetHeader();
        }

        try
        {
            if (PACFile.FileCount <= 0)
                return new ArcSysFileSystemInfo[0];

            if (Files != null && !recheck)
                return Files;

            using var reader = new EndiannessAwareBinaryReader(GetReadStream(), Endianness);
            var virtualFiles = new ArcSysFileSystemInfo[PACFile.FileCount];

            for (var i = 0; i < PACFile.FileCount; i++)
            {
                var file = PACFile.Files[i];
                var ext = Path.GetExtension(file.name);
                virtualFiles[i] = ext switch
                {
                    ".pac" or ".paccs" or ".pacgz" or ".fontpac" => new PACFileInfo(
                        FullName + ':' + file.name,
                        file.length,
                        file.offset + PACFile.HeaderSize,
                        this),
                    ".hip" => new HIPFileInfo(
                        FullName + ':' + file.name,
                        file.length,
                        file.offset + PACFile.HeaderSize,
                        this),
                    ".hpl" => new HPLFileInfo(
                        FullName + ':' + file.name,
                        file.length,
                        file.offset + PACFile.HeaderSize,
                        this),
                    ".dds" => new DDSFileInfo(
                        FullName + ':' + file.name,
                        file.length,
                        file.offset + PACFile.HeaderSize,
                        this),
                    _ => new ArcSysFileSystemInfo(
                        FullName + ':' + file.name,
                        file.length,
                        file.offset + PACFile.HeaderSize,
                        this)
                };
            }

            reader.Close();

            Files = virtualFiles;
            return Files;
        }
        catch
        {
            Files = new ArcSysFileSystemInfo[0];
            return Files;
        }
    }

    private void CreatePACDataFromFolder(int MinNameLength, PACFileOrder pfo)
    {
        using var memstream = ProcessFolder(FullName, MinNameLength, pfo);
        if (memstream == null)
            return;

        VFSIBytes = memstream.ToArray();
    }

    private void RebuildPACData(ArcSysFileSystemInfo[] files, Parameters parameters, int MinNameLength,
        PACFileOrder pfo)
    {
        CreatePAC(
            files.Select(f => new Tuple<FileSystemInfo, MemoryStream>(f, new MemoryStream(f.GetBytes()))).ToArray(),
            MinNameLength, pfo);

        var pfi = (PACFileInfo) Parent;
        if (pfi != null)
        {
            pfi.RebuildPACData(GetFiles(), parameters, MinNameLength, pfo);
        }
        else
        {
            System.IO.File.WriteAllBytes(FullName, VFSIBytes);
            GetFiles(true);
        }
    }

    private MemoryStream ProcessFolder(string path, int MinNameLength, PACFileOrder pfo)
    {
        var folders = Directory.GetDirectories(path).Select(d => new DirectoryInfo(d)).ToArray();
        var files = Directory.GetFiles(path).Select(f => new FileInfo(f)).ToArray();

        var fsia = new FileSystemInfo[files.Length + folders.Length];
        if (folders.Length > 0)
            Array.Copy(folders, 0, fsia, 0, folders.Length);

        if (files.Length > 0)
            Array.Copy(files, 0, fsia, folders.Length, files.Length);

        var fsiMemArray = new Tuple<FileSystemInfo, MemoryStream>[fsia.Length];

        for (var i = 0; i < fsiMemArray.Length; i++)
            fsiMemArray[i] = new Tuple<FileSystemInfo, MemoryStream>(fsia[i],
                fsia[i].Attributes.HasFlag(FileAttributes.Directory)
                    ? ProcessFolder(fsia[i].FullName, MinNameLength,
                        pfo.ChildOrders.Where(co =>
                            Path.GetExtension(co.File).ToLower() == ".pac" &&
                            Path.GetFileNameWithoutExtension(co.File) == fsia[i].Name).FirstOrDefault())
                    : new FileStream(fsia[i].FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                        .ToMemoryStream());

        return CreatePACStream(fsiMemArray, MinNameLength, pfo);
    }

    public void RemoveItem(ArcSysFileSystemInfo vfsi, PACFileOrder pfo)
    {
        var filesList = GetFiles().ToList();
        filesList.Remove(vfsi);
        RebuildPACData(filesList.ToArray(), PACFile.Params, PACFile.FileNameLength, pfo);
    }

    private MemoryStream CreatePACStream(Tuple<FileSystemInfo, MemoryStream>[] fsiMemArray, int MinNameLength,
        PACFileOrder pfo)
    {
        var createExtNameID = PACFile.Params.HasFlag(Parameters.GenerateExtendedNameID);

        var createNameID = PACFile.Params.HasFlag(Parameters.GenerateNameID) ||
                           createExtNameID;

        if (pfo != null)
        {
            var pfoNames = pfo.ChildOrders.Select(co => co.File).ToArray();
            fsiMemArray = fsiMemArray.OrderBy(fsiMem => Array.IndexOf(pfoNames,
                fsiMem.Item1.Attributes.HasFlag(FileAttributes.Directory)
                    ? $"{fsiMem.Item1.Name}.pac"
                    : fsiMem.Item1.Name)).ToArray();
        }

        var names = fsiMemArray.Select(fsiMem =>
        {
            if (fsiMem.Item1 is not ArcSysFileSystemInfo vfsi)
                if (fsiMem.Item1.Attributes.HasFlag(FileAttributes.Directory))
                    return fsiMem.Item1.Name + ".pac";
            return fsiMem.Item1.Name;
        }).ToArray();

        var longestFileName = GetMaxNameLength(names);
        if (longestFileName < MinNameLength)
            longestFileName = MinNameLength;

        var fileMemoryStream = new MemoryStream();

        var fileLength = 0;

        using (var writer = new EndiannessAwareBinaryWriter(fileMemoryStream, Encoding.Default, true, Endianness))
        {
            writer.Write(PAC.MagicBytes, ByteOrder.LittleEndian);
            writer.Write(0);
            writer.Write(0);
            writer.Write(names.Length);
            writer.Write((uint) PACFile.Params);
            writer.Write(longestFileName);
            writer.Write(0);
            writer.Write(0);
            for (var i = 0; i < names.Length; i++)
            {
                var bytes = new byte[longestFileName];
                var fileName = Path.GetFileName(names[i]);
                if (fileName.Length >= longestFileName)
                {
                    var fn = Path.GetFileNameWithoutExtension(fileName);
                    var fe = Path.GetExtension(fileName);
                    fn = fn.Remove(longestFileName - fe.Length - 1);
                    fileName = fn + fe;
                }

                var nameBytes = Encoding.ASCII.GetBytes(fileName);
                Buffer.BlockCopy(nameBytes, 0, bytes, 0, nameBytes.Length);
                writer.Write(bytes, ByteOrder.LittleEndian);
                writer.Write(i);
                writer.Write(fileLength);
                var length = (int) fsiMemArray[i].Item2.Length % 16;
                length = length == 0 ? length : 16 - length;
                length += (int) fsiMemArray[i].Item2.Length;
                fileLength += length;
                writer.Write((int) fsiMemArray[i].Item2.Length);
                if (createNameID)
                {
                    var nameID = 0;
                    foreach (var c in fileName.ToLower())
                    {
                        nameID *= 0x89;
                        nameID += c;
                    }

                    writer.Write(nameID);
                }

                var padLength = (longestFileName + 12 + (createNameID ? 4 : 0)) % 16;
                padLength = padLength == 0 ? createNameID ? 0 : 16 : 16 - padLength;
                writer.Write(new byte[padLength]);
            }

            var headersize = (int) writer.BaseStream.Position;

            foreach (var fsiMem in fsiMemArray)
                using (fsiMem.Item2)
                {
                    writer.Write(fsiMem.Item2.ToArray(), ByteOrder.LittleEndian);
                    var padLength = (int) fsiMem.Item2.Length % 16;
                    padLength = padLength == 0 ? padLength : 16 - padLength;
                    writer.Write(new byte[padLength]);
                }

            writer.BaseStream.Position = 4;
            writer.Write(headersize);
            writer.Write((int) writer.BaseStream.Length);
            writer.BaseStream.Position = 0;
            writer.Close();
        }

        return fileMemoryStream;
    }

    private void CreatePAC(Tuple<FileSystemInfo, MemoryStream>[] fsiMemArray, int MinNameLength, PACFileOrder pfo)
    {
        using var memstream = CreatePACStream(fsiMemArray, MinNameLength, pfo);
        if (memstream == null)
            return;

        VFSIBytes = memstream.ToArray();
    }

    public PACFileOrder GetPACFileOrder()
    {
        return new PACFileOrder
        {
            File = Name,
            ChildOrders = GetPACFileOrdersRecursive(GetFiles())
        };
    }

    private PACFileOrder[] GetPACFileOrdersRecursive(ArcSysFileSystemInfo[] vfsia)
    {
        var pacFileOrders = new PACFileOrder[vfsia.Length];
        for (var i = 0; i < vfsia.Length; i++)
        {
            pacFileOrders[i] = new PACFileOrder
            {
                File = vfsia[i].Name
            };
            if (vfsia[i] is PACFileInfo pacFileInfo)
                pacFileOrders[i].ChildOrders = GetPACFileOrdersRecursive(pacFileInfo.GetFiles());
        }

        return pacFileOrders;
    }

    private int GetMaxNameLength(string[] fileNames)
    {
        var createExtNameID = PACFile.Params.HasFlag(Parameters.GenerateExtendedNameID);

        var createNameID = PACFile.Params.HasFlag(Parameters.GenerateNameID) ||
                           createExtNameID;

        var minNameLength = fileNames.Length > 0 ? createNameID ? createExtNameID ? 64 : 32 : 1 : 0;
        var longestFileName = minNameLength;
        if (fileNames.Length > 0)
        {
            longestFileName = fileNames.OrderByDescending(p => p.Length).FirstOrDefault().Length;
            var namelength = longestFileName % 4;
            longestFileName += namelength == 0 ? 4 : 4 - namelength;

            if (createNameID)
            {
                if (longestFileName >= minNameLength)
                {
                    var errmesg = "Due to packing parameters, file name cannot equal or exceed " +
                                  $"{(createExtNameID ? 64 : 32)} characters.";
                    throw new Exception(errmesg);
                }

                longestFileName = minNameLength;
            }
            else
            {
                longestFileName = longestFileName < minNameLength ? minNameLength : longestFileName;
            }
        }

        return longestFileName;
    }
}