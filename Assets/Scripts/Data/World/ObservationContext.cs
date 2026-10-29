public static class ObservationContext
{
    public static int GridX { get; set; } = -1;
    public static int GridY { get; set; } = -1;

    public static bool HasTarget =>
        GridX >= 0 && GridY >= 0;
}