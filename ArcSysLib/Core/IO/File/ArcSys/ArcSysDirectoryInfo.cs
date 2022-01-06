using System.IO;

namespace ArcSysLib.Core.IO.File.ArcSys;

public class ArcSysDirectoryInfo : ArcSysFileSystemInfo
{
    public ArcSysDirectoryInfo(string path, bool preCheck = true) : base(path, preCheck)
    {
    }

    public ArcSysDirectoryInfo(string path, ulong length, ulong offset, ArcSysDirectoryInfo parent,
        bool preCheck = true) : base(path,
        length, offset, parent, preCheck)
    {
    }

    public ArcSysDirectoryInfo(MemoryStream memstream, string name = "Memory", bool preCheck = true) : base(
        memstream, name, preCheck)
    {
    }

    public ArcSysDirectoryInfo(ArcSysFileSystemInfo[] files, string name = "Memory", bool preCheck = true) : this(
        new MemoryStream(new byte[0]), name, preCheck)
    {
        Files = files;
    }

    public ArcSysFileSystemInfo[] Files { get; protected set; }
}