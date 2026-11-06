namespace ShiftJam
{
    /// <summary>
    /// Told after Build Mode placed, moved, turned, raised, scaled, copied or re-posed the thing (undo, redo, a loaded
    /// layout, Start empty), so it takes its new pose as home (a soldier's post, a car's start) and re-measures anything
    /// that depends on its scale. Build Mode calls it on every IBuildMoved under the edited piece, once per edit.
    /// </summary>
    public interface IBuildMoved
    {
        void OnBuildMoved();
    }
}
