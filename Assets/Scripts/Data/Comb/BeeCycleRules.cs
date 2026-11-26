using System;
using System.Collections.Generic;

[Serializable] public sealed class CycleFormula
{
    public string name,expression;
    public CycleFormula(){}
    public CycleFormula(string n,string e){name=n;expression=e;}
}
public struct CycleInputs
{
    public double ratio,need,time,available,amount,distance,height,temperature,elapsed,actionRate;
}
public sealed class BeeCycleRules
{
    readonly Dictionary<string,BeeFormula> formulas=new Dictionary<string,BeeFormula>();
    readonly Dictionary<string,double> values=new Dictionary<string,double>();
    CycleInputs input;
    readonly Func<string,double> lookup;
    public BeeCycleRules(BeeCycleSettings settings)
    {
        settings.Validate();lookup=Lookup;
        foreach(var f in typeof(BeeCycleSettings).GetFields())if(f.FieldType==typeof(double)||f.FieldType==typeof(int))values.Add(f.Name,Convert.ToDouble(f.GetValue(settings)));
        foreach(var f in settings.formulas)
        {
            if(formulas.ContainsKey(f.name))throw new ArgumentException("Duplicate formula "+f.name);
            var parsed=new BeeFormula(f.expression,BindVariable);
            foreach(var v in parsed.Variables)if(!values.ContainsKey(v)&&Array.IndexOf(Inputs,v)<0)throw new ArgumentException("Unknown variable "+v);
            formulas.Add(f.name,parsed);
        }
        foreach(var f in Defaults())if(!formulas.ContainsKey(f.name))throw new ArgumentException("Missing formula "+f.name);
        foreach(var name in formulas.Keys)Eval(name,new CycleInputs{ratio=0.5,need=0.5,time=1,available=1,amount=1,distance=100,height=0.5,temperature=34,elapsed=1,actionRate=0.001});
    }
    static readonly string[] Inputs={"ratio","need","time","available","amount","distance","height","temperature","elapsed","actionRate"};
    Func<double> BindVariable(string name)
    {
        switch(name){case "ratio":return ()=>input.ratio;case "need":return ()=>input.need;case "time":return ()=>input.time;case "available":return ()=>input.available;case "amount":return ()=>input.amount;case "distance":return ()=>input.distance;case "height":return ()=>input.height;case "temperature":return ()=>input.temperature;case "elapsed":return ()=>input.elapsed;case "actionRate":return ()=>input.actionRate;default:if(values.TryGetValue(name,out var constant))return ()=>constant;return null;}
    }
    double Lookup(string n)
    {
        switch(n){case "ratio":return input.ratio;case "need":return input.need;case "time":return input.time;case "available":return input.available;case "amount":return input.amount;case "distance":return input.distance;case "height":return input.height;case "temperature":return input.temperature;case "elapsed":return input.elapsed;case "actionRate":return input.actionRate;default:return values[n];}
    }
    public double Eval(string name,CycleInputs data=default)
    {input=data;double v=formulas[name].Evaluate(lookup);if(v<0)throw new InvalidOperationException("Negative formula result: "+name);return v;}
    public static List<CycleFormula> Defaults()=>new List<CycleFormula>{
        new CycleFormula("deficit","clamp(1-amount/max(available,0.000000001),0,1)"),
        new CycleFormula("commandChance","1"),
        new CycleFormula("relocateTemperatureDesire","relocateTemperatureWeight*clamp(ratio,0,1)"),
        new CycleFormula("relocateCrowdingDesire","relocateCrowdingWeight*clamp(ratio,0,1)"),
        new CycleFormula("relocateDestination","amount/(1+distance/relocateDistanceMm)"),
        new CycleFormula("feedDesire","feedWeight * pow(clamp(ratio,0,1),2)"),
        new CycleFormula("restDesire","restWeight * pow(clamp(ratio,0,1),2)"),
        new CycleFormula("recoverDesire","recoverWeight * pow(clamp(ratio,0,1),2)"),
        new CycleFormula("storeDesire","storeWeight * pow(clamp(ratio,0,1),2)"),
        new CycleFormula("buildCellWeight","1+amount"),
        new CycleFormula("layCellWeight","band(ratio,0.5,2,1/(1+distance),1+amount,1+amount)"),
        new CycleFormula("larvaDevelopmentFactor","ratio*min(available,amount)"),
        new CycleFormula("nurseNeed","clamp(amount/max(available,0.000000001),0,1)"),
        new CycleFormula("heatNeed","clamp(amount/heatDeficitScale,0,1)"),
        new CycleFormula("commandStimulus","commandMaximum * clamp(need,0,1)"),
        new CycleFormula("desireNext","max(0,amount+available-naturalDecay)"),
        new CycleFormula("interruptChance","clamp(elapsed*interruptPerElapsedMinute,0,1)"),
        new CycleFormula("storagePreference","available*(1+storageHeightBias*height)/(1+distance/storageDistanceMm)"),
        new CycleFormula("buildPreference","0.6+0.4*clamp(1-amount/20,0,1)"),
        new CycleFormula("development","actionRate*time"),
        new CycleFormula("broodTemperature","clamp(min((temperature-broodMinTemperature)/(broodIdealTemperature-broodMinTemperature),(broodMaxTemperature-temperature)/(broodMaxTemperature-broodIdealTemperature)),0,1)"),
        new CycleFormula("broodDevelopment","broodTick*ratio*time"),
        new CycleFormula("basalHeat","basalHeatJ*time"),
        new CycleFormula("broodBasalHeat","amount*time"),
        new CycleFormula("additionalHeat","extraHeatJ*time"),
        new CycleFormula("basalSugarCost","basalSugar*time"),
        new CycleFormula("basalEnergyCost","basalEnergy*time"),
        new CycleFormula("movementEnergyCost","movementEnergy*time"),
        new CycleFormula("moveDistance","speed*60*time"),
        new CycleFormula("feedAmount","min(feedRate*time,amount)"),
        new CycleFormula("restAmount","min(restRate*time,amount)"),
        new CycleFormula("recoverAmount","min(recoveryRate*time,amount)"),
        new CycleFormula("acquireAmount","min(acquireRate*time,amount)"),
        new CycleFormula("storeAmount","min(storeRate*time,amount)"),
        new CycleFormula("buildAmount","buildPoints*time"),
        new CycleFormula("forageCollectSugar","forageSugarRate*time"),
        new CycleFormula("forageCollectPollen","foragePollenRate*time"),
        new CycleFormula("forageShortageReturnChance","clamp(ratio,0,1)*forageShortageReturnRate"),
        new CycleFormula("forageReturnChance","pow(clamp(ratio,0,1),forageReturnExponent)"),
        new CycleFormula("forageDepartureWeight","pow(clamp(1-ratio,0,1),forageDepartureExponent)"),
        new CycleFormula("forageSourceWeight","available"),
        new CycleFormula("forageDuration","forageMinutes"),
        new CycleFormula("forageSugarAmount","forageSugar"),
        new CycleFormula("foragePollenAmount","foragePollen"),
        new CycleFormula("forageWaterAmount","forageWater"),
        new CycleFormula("nurseAmount","nurseRate*time"),
        new CycleFormula("starvation","starvationDamage*time"),
        new CycleFormula("layChance","clamp(queenLayPerMinute*time,0,1)")
    };
}
