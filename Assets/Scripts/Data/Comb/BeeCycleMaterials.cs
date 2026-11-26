using System;

internal sealed partial class CellMaterialStore
{
    internal void WithdrawCycle(int index,CellContentKind kind,double fraction,CycleCargo result,out double water)
    {
        float old=payloads[index].Mass;
        payloads[index].Mass=(float)(old*(1-fraction));
        water=0;
        if(kind==CellContentKind.Pollen){result.pollen+=(double)old-payloads[index].Mass;return;}
        water=(double)old-payloads[index].Mass;
        for(int n=payloads[index].Head;n!=0;n=sugars[n-1].Next)
        {
            var s=sugars[n-1];float before=s.Mass;s.Mass=(float)(before*(1-fraction));sugars[n-1]=s;
            result.Add(s.Source,(double)before-s.Mass);
        }
    }
}
public sealed partial class FrameSurface
{
    // Request is dry sugar or pollen mass. Co-withdrawn water is intentionally discarded by the MVP.
    public CycleCargo WithdrawCycle(int x,int y,double requested,bool pollen,out double discardedWater)
    {
        if(double.IsNaN(requested)||double.IsInfinity(requested)||requested<0)throw new ArgumentException(nameof(requested));
        discardedWater=0;var result=new CycleCargo();int i=GetIndex(x,y);
        var kind=pollen?CellContentKind.Pollen:CellContentKind.Nectar;
        if(materials==null||cells[i].Content!=kind||cells[i].IsCapped)return result;
        double available=pollen?materials.Mass(i):materials.TotalSugar(i);
        if(available<=0||requested==0)return result;
        materials.WithdrawCycle(i,kind,Math.Min(1,requested/available),result,out discardedWater);
        if(materials.Mass(i)==0&&(pollen||materials.TotalSugar(i)==0))
        {materials.Clear(i,kind);cells[i].TrySetContent(CellContentKind.Empty);}
        SyncPollenSeed(i);AdvanceRevision();return result;
    }
    // Return the real float32 increase, not the requested double amount.
    public double DepositCycle(int x,int y,int source,double requested,bool pollen)
    {
        source=NectarSourceIdentity.Stored(source);
        if(double.IsNaN(requested)||double.IsInfinity(requested)||requested<0)throw new ArgumentException(nameof(requested));
        if(requested==0)return 0;
        double before=pollen?GetPollenMassMg(x,y):GetSugarMassMg(x,y,source);
        float amount=(float)requested;
        // Round towards zero, so a float32 write never removes more cargo than exists.
        if(amount>requested)amount=PreviousPositive(amount);
        if(amount<=0)return 0;
        float next=(float)(before+amount);
        if((double)next-before>requested)amount=Math.Max(0,PreviousPositive(next)-(float)before);
        bool ok=pollen?TryAddPollen(x,y,amount):TryAddNectar(x,y,source,amount,0);
        return ok?(pollen?GetPollenMassMg(x,y):GetSugarMassMg(x,y,source))-before:0;
    }
    static float PreviousPositive(float value)
    {if(value<=0)return 0;return BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(value)-1);}
}
