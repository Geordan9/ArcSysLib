using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using ArcSysLib.Common.Enum;
using ArcSysLib.Core.ArcSys;
using ArcSysLib.Util;
using VFSILib.Core.IO;

namespace ArcSysLib.Core.IO.File.ArcSys;

public class ArcSysFileSystemInfo : VirtualFileSystemInfo
{
    [Flags]
    public enum FileObfuscation
    {
        None = 0x0,
        FPACEncryption = 0x1,
        FPACDeflation = 0x2,
        ArcSysMD5Encryption = 0x4,
        SwitchCompression = 0x8
    }

    public ArcSysFileSystemInfo(string path, bool preCheck = true) : base(path, preCheck)
    {
    }

    public ArcSysFileSystemInfo(FileSystemInfo fi, bool preCheck = true) : base(fi, preCheck)
    {
    }

    public ArcSysFileSystemInfo(string path, ulong length, ulong offset, ArcSysDirectoryInfo parent,
        bool preCheck = true) : base(path, length, offset, parent, preCheck)
    {
        if (parent != null) Obfuscation = parent.Obfuscation;
    }

    public ArcSysFileSystemInfo(MemoryStream memstream, string name = "Memory", bool preCheck = true) : this(memstream,
        null, name, preCheck)
    {
    }

    public ArcSysFileSystemInfo(MemoryStream memstream, ArcSysDirectoryInfo parent, string name = "Memory",
        bool preCheck = true) : base(memstream, parent, name, preCheck)
    {
        if (parent != null && !parent.Files.Contains(this))
            for (var i = 0; i < parent.Files.Length; i++)
                if (parent.Files[i] == null)
                {
                    parent.Files[i] = this;
                    break;
                }
    }

    // Properties

    public FileObfuscation Obfuscation { get; set; }

    // Methods

    protected FileObfuscation GetFileObfuscation()
    {
        if (MagicBytes.Take(3).SequenceEqual(Util.MagicBytes.GZIP))
            return FileObfuscation.SwitchCompression;

        if (Extension.Contains("pac") && !MagicBytes.SequenceEqual(Util.MagicBytes.BCSM))
        {
            if (MagicBytes.SequenceEqual(BBObfuscatorTools.DeflateArcSystemMagicBytes))
                return Obfuscation | FileObfuscation.FPACDeflation;
            if (!MagicBytes.SequenceEqual(PAC.MagicBytes))
                return Obfuscation | FileObfuscation.FPACEncryption;
        }

        if (string.IsNullOrEmpty(Extension) && MD5Tools.IsMD5(Name))
            return FileObfuscation.ArcSysMD5Encryption;


        return FileObfuscation.None;
    }

    protected void UpdateMagicAndObfuscation(MemoryStream ms)
    {
        UpdateMagicBytes(ms);
        var obf = GetFileObfuscation();
        if (obf != FileObfuscation.None) Obfuscation = obf;
    }

    protected override void Initialize(bool force = false)
    {
        if (Initialized && !force)
            return;
        Initialized = true;
        using var s = GetReadStream();
        if (s == null)
            return;
        OffsetFileStream(this, s);
        s.Read(MagicBytes, 0, 4);
        Obfuscation = GetFileObfuscation();
        s.Close();
    }

    protected override Stream GetReadStream()
    {
        return GetReadStream(false);
    }

    protected Stream GetReadStream(bool onlyHeader)
    {
        try
        {
            if (VFSIBytes == null && GetVirtualRootBytes() == null)
            {
                var fs = new FileStream(GetPrimaryPath(FullName), FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite);
                try
                {
                    if (Obfuscation != FileObfuscation.None)
                    {
                        var stream = new MemoryStream();
                        try
                        {
                            var path = fs.Name;
                            fs.CopyTo(stream);
                            fs.Close();
                            fs.Dispose();

                            stream.Position = 0;

                            if (Obfuscation.HasFlag(FileObfuscation.FPACEncryption))
                            {
                                var output =
                                    BBObfuscatorTools.FPACCryptStream(stream, path, CryptMode.Decrypt, onlyHeader);
                                stream.Close();
                                stream.Dispose();
                                stream = output;

                                UpdateMagicAndObfuscation(stream);
                            }

                            if (Obfuscation.HasFlag(FileObfuscation.FPACDeflation))
                            {
                                var output = BBObfuscatorTools.DFASFPACInflateStream(stream);
                                stream.Close();
                                stream.Dispose();
                                stream = output;

                                UpdateMagicAndObfuscation(stream);
                            }

                            if (Obfuscation.HasFlag(FileObfuscation.SwitchCompression))
                            {
                                using (Stream input = new GZipStream(new MemoryStream(stream.GetBuffer()),
                                           CompressionMode.Decompress, true))
                                {
                                    using var output = new MemoryStream();
                                    input.CopyTo(output);
                                    stream = new MemoryStream(output.ToArray());
                                }

                                UpdateMagicAndObfuscation(stream);
                            }

                            if (Obfuscation.HasFlag(FileObfuscation.ArcSysMD5Encryption))
                            {
                                var output = ArcSysMD5CryptTools.ArcSysMD5CryptStream(
                                    stream, path,
                                    CryptMode.Decrypt, true);
                                stream.Close();
                                stream.Dispose();
                                stream = output;

                                UpdateMagicAndObfuscation(stream);
                            }

                            if (VirtualRoot.Active)
                                VFSIBytes = stream.ToArray();

                            stream.Position = 0;
                            if (Offset > 0) OffsetFileStream(this, stream);
                            return stream;
                        }
                        catch
                        {
                            stream.Close();
                            stream.Dispose();
                            fs.Close();
                            fs.Dispose();
                            return null;
                        }
                    }

                    if (Offset > 0) OffsetFileStream(this, fs);
                    return fs;
                }
                catch
                {
                    fs.Close();
                    fs.Dispose();
                    return null;
                }
            }

            var memStream = new MemoryStream(VFSIBytes ?? GetVirtualRootBytes());
            if (Offset > 0) OffsetFileStream(this, memStream);
            return memStream;
        }
        catch
        {
            NoAccess = true;
            return null;
        }
    }
}