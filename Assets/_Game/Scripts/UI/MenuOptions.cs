using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The Options screen on a menu model (D-0204): the same options as OptionsPanel (D-0044), each a slab of its page
    /// "Options" (Made Controls on the model's prefab: copies of its own slabs). Look speed, volume and the body are
    /// VALUES: left and right step them (the MenuDriver), a click or the choosing dart on a slab's left half steps it
    /// down and on its right half up, the second line shows the value and a bar on the face shows how far it is. Invert
    /// Y, fullscreen and graphics are TOGGLES: choosing one flips it, and while it is on its frame shows the "active"
    /// colour. Every change is saved into PlayerOptions at once, as the panel does. Back is the owner's (it knows where
    /// Options was opened from). Made by whoever owns the model (the title, the pause menu); with its slabs missing it
    /// is not made, and the owner opens the old panel.
    /// </summary>
    public sealed class MenuOptions
    {
        public const string Page = "Options";
        public const string Look = "Opt_Look", Invert = "Opt_Invert", Volume = "Opt_Volume", Fullscreen = "Opt_Fullscreen",
            Graphics = "Opt_Graphics", Body = "Opt_Body", Back = "Opt_Back";
        private static readonly string[] Mine = { Look, Invert, Volume, Fullscreen, Graphics, Body, Back };

        /// <summary>What this page adds to the hint line.</summary>
        public const string HintMore = "left / right or a click on a side changes a value";

        private readonly MenuModel _model;
        private bool _fullscreenWanted;     // Screen.fullScreen changes at the end of the frame: the slab says what was asked for

        private MenuOptions(MenuModel model) { _model = model; }

        /// <summary>Makes the Options page on a model (its slabs once; asked again it makes nothing new). Null when the
        /// model has no such page in its Made Controls, or a slab of it cannot be made: the owner opens the old panel.</summary>
        public static MenuOptions Make(MenuModel model)
        {
            if (model == null) return null;
            if (!model.MakePage(Page, out string missing)) { Debug.LogWarning("MenuOptions: the old Options panel shows (" + missing + ").", model); return null; }
            foreach (string name in Mine)
                if (!model.TryGet(name, out _)) { Debug.LogWarning("MenuOptions: the old Options panel shows (no " + name + " on the page " + Page + ").", model); return null; }
            model.SetSteps(Look, true); model.SetSteps(Volume, true); model.SetSteps(Body, true);
            return new MenuOptions(model);
        }

        /// <summary>One of this page's controls (Back among them).</summary>
        public static bool Owns(string control)
        {
            foreach (string name in Mine) if (name == control) return true;
            return false;
        }

        /// <summary>Before the page is shown: the options as they are now.</summary>
        public void Open()
        {
            _fullscreenWanted = PlayerOptions.Fullscreen;
            Refresh();
        }

        /// <summary>A control of the page was chosen: a toggle flips; a value steps up. Back is left to the owner.</summary>
        public void Choose(string control)
        {
            switch (control)
            {
                case Invert: PlayerOptions.InvertY = !PlayerOptions.InvertY; break;
                case Fullscreen: _fullscreenWanted = !_fullscreenWanted; PlayerOptions.Fullscreen = _fullscreenWanted; break;
                case Graphics: PlayerOptions.HighGraphics = !PlayerOptions.HighGraphics; break;   // D-0119
                case Look: case Volume: case Body: Step(control, 1); return;
                default: return;
            }
            Refresh();
        }

        /// <summary>A value stepped down (-1) or up (+1). Look speed and volume stop at their ends; the body goes round.</summary>
        public void Step(string control, int way)
        {
            switch (control)
            {
                case Look: PlayerOptions.LookScale += way * PlayerOptions.LookStep; break;
                case Volume: PlayerOptions.Volume += way * PlayerOptions.VolumeStep; break;
                case Body:
                    if (PlayerBody.Count < 2) return;
                    PlayerOptions.Body = ((Mathf.Clamp(PlayerOptions.Body, 0, PlayerBody.Count - 1) + way) % PlayerBody.Count + PlayerBody.Count) % PlayerBody.Count;
                    break;
                default: return;
            }
            UiAudio.Play(UiAudio.Kind.Select);
            Refresh();
        }

        /// <summary>What every slab of the page reads, its bar, and which toggles are on.</summary>
        public void Refresh()
        {
            string editorOnly = Application.isEditor ? "  (built game only)" : "";
            float look = PlayerOptions.LookScale;
            _model.SetWords(Look, "Look speed", look.ToString("0.00") + "×");
            _model.SetFill(Look, Mathf.InverseLerp(PlayerOptions.MinLook, PlayerOptions.MaxLook, look));
            _model.SetWords(Invert, "Invert Y", PlayerOptions.InvertY ? "On" : "Off");
            _model.SetActive(Invert, PlayerOptions.InvertY);
            _model.SetWords(Volume, "Volume", Mathf.RoundToInt(PlayerOptions.Volume * 100f) + "%");
            _model.SetFill(Volume, PlayerOptions.Volume);
            _model.SetWords(Fullscreen, "Fullscreen", (_fullscreenWanted ? "On" : "Off") + editorOnly);
            _model.SetActive(Fullscreen, _fullscreenWanted);
            _model.SetWords(Graphics, "Graphics", (PlayerOptions.HighGraphics ? "High" : "Low") + editorOnly);
            _model.SetActive(Graphics, PlayerOptions.HighGraphics);
            // The body: a list to step through where there is more than one (after the player port's builder has run).
            int count = PlayerBody.Count;
            if (count > 1)
            {
                int body = Mathf.Clamp(PlayerOptions.Body, 0, count - 1);
                var worn = Object.FindFirstObjectByType<PlayerBody>();       // swapped when a scene loads: said while the one picked is not the one on screen
                bool waits = worn != null && worn.Index != body;
                _model.SetWords(Body, "Body", PlayerBody.NameOf(PlayerOptions.Body) + (waits ? "  (from the next load)" : ""));
                _model.SetFill(Body, (float)body / (count - 1));
                _model.SetUsable(Body, true);
            }
            else
            {
                _model.SetWords(Body, "Body", "only one to wear");
                _model.SetFill(Body, -1f);
                _model.SetUsable(Body, false);
            }
            _model.SetWords(Back, "Back");
        }
    }
}
