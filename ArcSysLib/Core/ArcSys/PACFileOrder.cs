namespace ArcSysLib.Core.ArcSys;

public class PACFileOrder
{
    public string File { get; set; }

    public PACFileOrder[] ChildOrders { get; set; } = new PACFileOrder[0];
}