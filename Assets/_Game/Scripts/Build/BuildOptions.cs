namespace ShiftJam
{
    /// <summary>The build toggles every tool reads: grid snap and whether red (overlapping) placement is allowed.</summary>
    public sealed class BuildOptions
    {
        public bool snap = true;
        public bool allowOverlap;
    }
}
