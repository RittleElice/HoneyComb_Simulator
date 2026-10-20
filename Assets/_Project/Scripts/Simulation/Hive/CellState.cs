namespace HoneyComb.Simulation.Hive
{
    public enum CellContentType : byte { Empty, Brood, Honey, Pollen }
    // A value snapshot only; detailed brood/food/building state is intentionally deferred.
    public readonly struct CellState
    {
        public CellContentType Content { get; }
        public CellState(CellContentType content)
        {
            if(content < CellContentType.Empty || content > CellContentType.Pollen)
                throw new System.ArgumentOutOfRangeException(nameof(content));
            Content=content;
        }
    }
}
