using System.IO;
using System.Text;
using ArcSysLib.Common.Enum;
using ArcSysLib.Util.Extension;

namespace ArcSysLib.Util;

public static class ArcSysMD5CryptTools
{
    public static byte[] EncryptionKey = new byte[0x2B];

    private static int index;

    public static MemoryStream ArcSysMD5CryptStream(MemoryStream ms, string path, CryptMode mode,
        bool leaveOpen = false)
    {
        ms.Position = 0;

        var file = path;
        var filename = Path.GetFileName(file);

        if (mode == CryptMode.Decrypt)
        {
            index = MD5Tools.CreateMD5(filename).ToByteArray()[7] % EncryptionKey.Length;
        }
        else if (mode == CryptMode.Encrypt && filename.Length > 32 && MD5Tools.IsMD5(filename.Substring(0, 32)))
        {
            filename = filename.Substring(0, 32);
            index = MD5Tools.CreateMD5(filename).ToByteArray()[7] % EncryptionKey.Length;
        }
        else if (mode == CryptMode.Encrypt && file.LastIndexOf("data") >= 0)
        {
            var datapath = file.Substring(file.LastIndexOf("data"), file.Length - file.LastIndexOf("data"));
            var filenameMD5 = MD5Tools.CreateMD5(datapath.Replace("\\", "/"));
            index = MD5Tools.CreateMD5(filenameMD5).ToByteArray()[7] % 43;
        }
        else
        {
            return ms;
        }

        var count = (int) (ms.Length - ms.Position);
        var newStream = new MemoryStream();
        using (var reader = new BinaryReader(ms, Encoding.Default, leaveOpen))
        {
            for (var i = 0; i < count; i++)
            {
                newStream.WriteByte((byte) (EncryptionKey[index] ^ reader.ReadByte()));
                index = (index + 1) % EncryptionKey.Length;
            }
        }

        newStream.Position = 0;

        return newStream;
    }
}