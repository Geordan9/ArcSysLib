using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace ArcSysLib.Util.Extension;

public static class StringExtension
{
    public static bool OnlyHex(this string test)
    {
        return Regex.IsMatch(test, @"\A\b[0-9a-fA-F]+\b\Z") || Regex.IsMatch(test, @"\A\b(0[xX])?[0-9a-fA-F]+\b\Z");
    }

    public static byte[] ToByteArray(this string hex)
    {
        return Enumerable.Range(0, hex.Length)
            .Where(x => x % 2 == 0)
            .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
            .ToArray();
    }
}