using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ArcSysLib.Util;

public static class MD5Tools
{
    public static string CreateMD5(string input)
    {
        using var md5 = MD5.Create();
        var inputBytes = Encoding.ASCII.GetBytes(input.ToLower());
        var hashBytes = md5.ComputeHash(inputBytes);

        var sb = new StringBuilder();
        for (var i = 0; i < hashBytes.Length; i++) sb.Append(hashBytes[i].ToString("X2"));
        return sb.ToString().ToLower();
    }

    public static bool IsMD5(string input)
    {
        if (string.IsNullOrEmpty(input)) return false;

        return Regex.IsMatch(input, "^[0-9a-fA-F]{32}$", RegexOptions.Compiled);
    }
}