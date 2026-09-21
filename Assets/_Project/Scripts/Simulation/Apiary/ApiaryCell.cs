using HoneyComb.Simulation.Hive;
namespace HoneyComb.Simulation.Apiary
{
    // This is an apiary ground slot, not a honeycomb cell.
    public sealed class ApiaryCell
    {
        public int X { get; }
        public int Y { get; }
        public HiveState Hive { get; internal set; }
        internal ApiaryCell(int x,int y) { X=x; Y=y; }
    }
}
