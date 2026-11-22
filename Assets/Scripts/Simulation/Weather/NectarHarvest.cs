using System;

// All amounts are mg. The caller aggregates same-tick demand and distributes
// the actual withdrawal across returning bees.
public static class NectarHarvest
{
    public readonly struct Taken
    {
        public readonly double sugar,water,pollen;
        public Taken(double sugar,double water,double pollen)
        {this.sugar=sugar;this.water=water;this.pollen=pollen;}
    }
    static void Validate(double value,string name)
    {
        if(value<0 || double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(name);
    }
    public static Taken Withdraw(NectarSimulator nectar,int source,double requestedSugar,double requestedPollen)
    {
        if(nectar==null)throw new ArgumentNullException(nameof(nectar));
        if(source<0 || source>=nectar.Sources.Length)throw new ArgumentOutOfRangeException(nameof(source));
        var stock=nectar.Sources[source]??throw new InvalidOperationException("Source is not initialized.");
        Validate(requestedSugar,nameof(requestedSugar));Validate(requestedPollen,nameof(requestedPollen));
        Validate(stock.sugar,"source sugar");Validate(stock.water,"source water");Validate(stock.pollen,"source pollen");
        double nextSugar=stock.sugar-Math.Min(stock.sugar,requestedSugar);
        double actualSugar=stock.sugar-nextSugar;
        double fraction=stock.sugar>0?actualSugar/stock.sugar:0;
        double nextWater=stock.water-Math.Min(stock.water,stock.water*fraction);
        double nextPollen=stock.pollen-Math.Min(stock.pollen,requestedPollen);
        var taken=new Taken(actualSugar,stock.water-nextWater,stock.pollen-nextPollen);
        stock.sugar=nextSugar;stock.water=nextWater;stock.pollen=nextPollen;
        return taken;
    }
}
