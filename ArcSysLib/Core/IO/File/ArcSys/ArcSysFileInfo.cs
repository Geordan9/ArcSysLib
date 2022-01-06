using System.IO;

namespace ArcSysLib.Core.IO.File.ArcSys;

public class ArcSysFileInfo : ArcSysFileSystemInfo
{
    public ArcSysFileInfo(string path, bool preCheck = true) : base(path, preCheck)
    {
    }

    public ArcSysFileInfo(string path, ulong length, ulong offset, ArcSysDirectoryInfo parent,
        bool preCheck = true) : base(path,
        length, offset, parent, preCheck)
    {
    }

    public ArcSysFileInfo(MemoryStream memstream, string name = "Memory", bool preCheck = true) : base(memstream,
        name, preCheck)
    {
    }
}