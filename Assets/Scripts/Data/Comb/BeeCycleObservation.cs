public sealed partial class BeeCycleRuntime
{
    // Read-only, indexed lookup for the currently observed comb. No whole-colony scan.
    public bool TryGetObservationStage(long beeId, out CycleStage stage)
    {
        if (byId.TryGetValue(beeId, out var bee)) { stage = bee.stage; return true; }
        stage = default; return false;
    }
}
