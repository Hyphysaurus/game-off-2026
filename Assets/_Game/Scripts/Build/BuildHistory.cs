using System;
using System.Collections.Generic;

namespace ShiftJam
{
    /// <summary>
    /// Undo and redo for Build Mode. Every edit is the same kind of step: the before and after states of the things it
    /// touched (pose, scale, shown, shape). Creating is "hidden before, shown after"; deleting is the reverse.
    /// Usage: Begin(things) → change them → Commit(). A step that changed nothing is dropped.
    /// </summary>
    public sealed class BuildHistory
    {
        private const int Limit = 200;

        private sealed class Step
        {
            public Buildable[] who;
            public BuildState[] before, after;
        }

        private readonly List<Step> _undo = new List<Step>();
        private readonly List<Step> _redo = new List<Step>();
        private Buildable[] _pending;
        private BuildState[] _pendingBefore;

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;
        /// <summary>Raised after anything here changes (a new step, an undo, a redo, a clear).</summary>
        public event Action Changed;

        public void Begin(IEnumerable<Buildable> things)
        {
            var list = new List<Buildable>();
            foreach (var b in things) if (b != null && !list.Contains(b)) list.Add(b);
            _pending = list.ToArray();
            _pendingBefore = new BuildState[_pending.Length];
            for (int i = 0; i < _pending.Length; i++) _pendingBefore[i] = _pending[i].Capture();
        }

        public void Commit()
        {
            if (_pending == null) return;
            var step = new Step { who = _pending, before = _pendingBefore, after = new BuildState[_pending.Length] };
            _pending = null; _pendingBefore = null;
            bool changed = false;
            for (int i = 0; i < step.who.Length; i++)
            {
                if (step.who[i] == null) continue;
                step.after[i] = step.who[i].Capture();
                if (!step.after[i].SameAs(step.before[i])) changed = true;
            }
            if (changed) Push(step);
        }

        /// <summary>Things just created: undo hides them, redo shows them again.</summary>
        public void RecordCreated(IList<Buildable> made)
        {
            var step = new Step { who = new Buildable[made.Count], before = new BuildState[made.Count], after = new BuildState[made.Count] };
            for (int i = 0; i < made.Count; i++)
            {
                step.who[i] = made[i];
                step.after[i] = made[i].Capture();
                step.before[i] = step.after[i];
                step.before[i].active = false;
            }
            if (made.Count > 0) Push(step);
        }

        public void Undo()
        {
            if (_undo.Count == 0) return;
            var step = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            for (int i = step.who.Length - 1; i >= 0; i--) if (step.who[i] != null) step.who[i].Apply(step.before[i]);
            _redo.Add(step);
            Changed?.Invoke();
        }

        public void Redo()
        {
            if (_redo.Count == 0) return;
            var step = _redo[_redo.Count - 1];
            _redo.RemoveAt(_redo.Count - 1);
            for (int i = 0; i < step.who.Length; i++) if (step.who[i] != null) step.who[i].Apply(step.after[i]);
            _undo.Add(step);
            Changed?.Invoke();
        }

        public void Clear()
        {
            _undo.Clear(); _redo.Clear(); _pending = null; _pendingBefore = null;
            Changed?.Invoke();
        }

        private void Push(Step step)
        {
            _undo.Add(step);
            if (_undo.Count > Limit) _undo.RemoveAt(0);
            _redo.Clear();
            Changed?.Invoke();
        }
    }
}
