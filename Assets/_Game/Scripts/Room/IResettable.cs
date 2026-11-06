namespace ShiftJam
{
    /// <summary>Something the Workshop's reset key puts back the way it started: targets, soldiers, cars, health.</summary>
    public interface IResettable
    {
        void ResetState();
    }
}
