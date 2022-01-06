using System;
using System.IO;
using System.Linq;
using System.Text;
using ArcSysLib.Common.Enum;

namespace ArcSysLib.Util;

public static class BBTAGMD5CryptTools
{
    private static readonly byte[] EncryptionKey =
    {
        0xF5, 0x5C, 0x84, 0x2A, 0xAD, 0x61, 0x54, 0xE7, 0x0A, 0xFC, 0x99, 0x6B, 0xD5, 0xA4, 0xD3, 0xD8, 0x48, 0x26,
        0x69, 0xCB, 0x07, 0x42, 0x13, 0x5E, 0x10, 0x23, 0xD2, 0x6D, 0x36, 0xC7, 0xC1, 0x66, 0xDF, 0xA1, 0xAD, 0xF1,
        0x44, 0x44, 0x7E, 0xC9, 0x8E, 0x24, 0x99
    };

    private static int index;

    public static MemoryStream BBTAGMD5CryptStream(MemoryStream ms, string path, CryptMode mode,
        bool leaveOpen = false)
    {
        ms.Position = 0;

        var file = path;
        var filename = Path.GetFileName(file);

        if (mode == CryptMode.Decrypt)
        {
            index = StringToByteArray(MD5Tools.CreateMD5(filename))[7] % 43;
        }
        else if (mode == CryptMode.Encrypt && filename.Length > 32 && MD5Tools.IsMD5(filename.Substring(0, 32)))
        {
            filename = filename.Substring(0, 32);
            index = StringToByteArray(MD5Tools.CreateMD5(filename))[7] % 43;
        }
        else if (mode == CryptMode.Encrypt && file.LastIndexOf("data") >= 0)
        {
            var datapath = file.Substring(file.LastIndexOf("data"), file.Length - file.LastIndexOf("data"));
            var filenameMD5 = MD5Tools.CreateMD5(datapath.Replace("\\", "/"));
            index = StringToByteArray(MD5Tools.CreateMD5(filenameMD5))[7] % 43;
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
                index = (index + 1) % 43;
            }
        }

        newStream.Position = 0;

        return newStream;
    }

    public static byte[] StringToByteArray(string hex)
    {
        return Enumerable.Range(0, hex.Length)
            .Where(x => x % 2 == 0)
            .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
            .ToArray();
    }
}