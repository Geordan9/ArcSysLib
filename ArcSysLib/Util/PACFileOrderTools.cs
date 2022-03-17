using System.Collections.Generic;
using System.IO;
using System.Linq;
using ArcSysLib.Core.ArcSys.Custom;

namespace ArcSysLib.Util;

public static class PACFileOrderTools
{
    public static void WriteFileOrder(string savePath, PACFileOrder fileOrder)
    {
        using TextWriter textWriter =
            new StreamWriter(new FileStream(
                savePath, FileMode.Create,
                FileAccess.ReadWrite,
                FileShare.ReadWrite));
        var fileOrders = fileOrder.ChildOrders;

        foreach (var fo in fileOrders) WriteFileOrderRecursive(textWriter, fo);
        textWriter.Close();
    }

    private static void WriteFileOrderRecursive(TextWriter textWriter, PACFileOrder fileOrder, int indent = 0)
    {
        var spaces = indent * 4;
        textWriter.WriteLine(fileOrder.File.PadLeft(spaces + fileOrder.File.Length, ' '));
        if (fileOrder.ChildOrders.Length > 0)
        {
            textWriter.WriteLine("{".PadLeft(spaces + 1, ' '));
            foreach (var fo in fileOrder.ChildOrders)
                WriteFileOrderRecursive(textWriter, fo, indent + 1);
            textWriter.WriteLine("}".PadLeft(spaces + 1, ' '));
        }
    }

    public static PACFileOrder ReadFileOrder(string filePath)
    {
        var fileOrder = new PACFileOrder
        {
            File = ":root:"
        };

        using (var streamReader = new StreamReader(new FileStream(filePath, FileMode.Open, FileAccess.Read,
                   FileShare.ReadWrite)))
        {
            fileOrder.ChildOrders = ReadFileOrdersRecursive(streamReader);
            streamReader.Close();
        }

        return fileOrder;
    }

    private static PACFileOrder[] ReadFileOrdersRecursive(StreamReader streamReader, bool child = false)
    {
        var fileOrderList = new List<PACFileOrder>();
        while (!streamReader.EndOfStream)
        {
            var line = streamReader.ReadLine();
            if (line.Replace(" ", string.Empty) == "{")
                fileOrderList.Last().ChildOrders = ReadFileOrdersRecursive(streamReader, true);
            else if (line.Replace(" ", string.Empty) == "}" && child)
                break;
            else
                fileOrderList.Add(new PACFileOrder {File = line});
        }

        return fileOrderList.ToArray();
    }
}