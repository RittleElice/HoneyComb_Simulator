using System;
using System.Linq;

public static class EffectiveThermalTests
{
    private static void Check(bool ok, string label) { if (!ok) throw new Exception("Effective exchange: " + label); }
    public static void Run(ThermalMaterials materials)
    {
        var item = new ItemData(9801, "effective analytic", ItemKind.Lid);
        var state = item.ThermalState; state.Refresh(materials, 20);
        // Star graph exercises the sum over multiple faces, including unequal capacities.
        int small = Enumerable.Range(0,state.Count).OrderBy(i=>state.HeatCapacityJPerK[i]).First();
        var indices = Enumerable.Range(0,state.Count).Where(i=>i!=small).OrderByDescending(i=>state.HeatCapacityJPerK[i]).Take(7).Prepend(small).ToArray();
        var cells = indices.Select((i,n)=>new ThermalExchange.Cell(state,i,"analytic "+n)).ToArray();
        var faces = Enumerable.Range(1,6).Select(i=>new ThermalExchange.Face(0,i,10,.001,.001)).ToArray();
        Action reset = () => { for(int i=0;i<cells.Length;i++) state.RestoreEnergy(indices[i],state.HeatCapacityJPerK[indices[i]]*(i==0?-20:40)); };
        reset();
        var solver = new ThermalExchange(cells,faces,.1); solver.RefreshCoefficients();
        Check(solver.RawMinimumStepSeconds<60 && solver.MinimumStepSeconds>=600-1e-8,"strong network safely limited");
        for(int i=0;i<cells.Length;i++) Check(60*solver.SumConductance(i)/state.HeatCapacityJPerK[indices[i]]<=.1+1e-12,"all-neighbour budget");
        double before=state.EnergyJ[small]; solver.Step(); double slowChange=state.EnergyJ[small]-before;
        reset(); var faster=new ThermalExchange(cells,faces,.2); faster.Step();
        Check(Math.Abs((state.EnergyJ[small]-before)/slowChange-2)<1e-4,"adjustable rate doubles small-system transfer");
        reset(); var equal=new ThermalExchange(cells,faces,.5);
        double initial=indices.Sum(i=>(double)state.EnergyJ[i]),rounding=0;
        float isolated=state.EnergyJ[indices[7]];
        for(int n=0;n<1200;n++)
        {
            equal.Step(); rounding+=indices.Sum(i=>Math.Abs((double)state.EnergyJ[i]))*1.2e-7;
            foreach(int i in indices) Check(state.TemperatureC(i)>=-20.00001 && state.TemperatureC(i)<=40.00001,"no temperature overshoot");
        }
        Check(state.EnergyJ[indices[7]]==isolated,"disconnected cell unchanged");
        Check(Math.Abs(indices.Sum(i=>(double)state.EnergyJ[i])-initial)<=rounding+1e-6,"closed energy bounded by rounding");
        int builds=equal.CoefficientBuilds; state.MarkDirty(small);state.Refresh(materials,20);equal.Step();
        Check(equal.CoefficientBuilds==builds,"unchanged capacity revision preserves effective budget");
        foreach(double bad in new[]{0d,-1d,.50001,double.NaN,double.PositiveInfinity})
        {
            bool rejected=false;try {new ThermalExchange(cells,faces,bad);}catch(ArgumentOutOfRangeException){rejected=true;}
            Check(rejected,"invalid rate rejected");
        }
        reset(); var empty=new ThermalExchange(Array.Empty<ThermalExchange.Cell>(),Array.Empty<ThermalExchange.Face>());empty.Step();
        var weak=new ThermalExchange(cells,new[]{new ThermalExchange.Face(0,1,1e-15,.01,.01)},.1);weak.RefreshCoefficients();
        double k0=state.EffectiveConductivityWPerMK[indices[0]],k1=state.EffectiveConductivityWPerMK[indices[1]];
        double physical=ThermalExchange.Conductance(1e-15,.01,k0,.01,k1);
        Check(weak.SumConductance(0)==physical,"weak physical transfer preserved");
    }
}
