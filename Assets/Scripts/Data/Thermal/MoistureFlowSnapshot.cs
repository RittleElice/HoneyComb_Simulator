using System.Collections.Generic;

public readonly struct MoistureFlowTransfer
{
    // Direction identifies the neighbour relative to the selected calculation cell: -X,+X,-Y,+Y,-Z,+Z.
    public readonly int Direction,SourceKind,DestinationKind,OtherIndex;
    public readonly bool Incoming;
    public readonly double MassKg;
    public readonly string OtherEquipment;
    public MoistureFlowTransfer(int direction,bool incoming,double massKg,int sourceKind,int destinationKind,string otherEquipment,int otherIndex)
    {
        Direction=direction;Incoming=incoming;MassKg=massKg;SourceKind=sourceKind;DestinationKind=destinationKind;
        OtherEquipment=otherEquipment;OtherIndex=otherIndex;
    }
}
public sealed class MoistureFlowSnapshot
{
    public long Minute;
    public bool Shared;
    public double EvaporationKg;
    public readonly List<MoistureFlowTransfer> Transfers=new List<MoistureFlowTransfer>();
}
