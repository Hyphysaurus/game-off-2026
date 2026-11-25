using System.Collections.Generic;
using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// The party list on the title's menu model (D-0204): the same list as PartiesPanel (D-0122), as its page "Parties"
    /// (Made Controls on the model's prefab: copies of its own slabs). One long slab per open party (its name, then its
    /// world and how full it is): choosing it joins that party. As many show as the page has party slabs (Party_1,
    /// Party_2, ...); with more open parties than that, More steps through them a pageful at a time. Refresh looks
    /// again; Host New Party, Host House Party and Join With Code (the copied code) start or join as the panel's buttons
    /// do. A party slab with no party is left off the page. What it is doing ("Looking for parties...") is Status, which
    /// the title shows on its hint line (NetSession's own news goes first). Back is the owner's. Without its slabs it is
    /// not made, and the title opens the old panel.
    /// </summary>
    public sealed class MenuParties
    {
        public const string Page = "Parties";
        public const string Refresh_ = "Parties_Refresh", Host = "Parties_Host", House = "Parties_House", Code = "Parties_Code", More = "Parties_More", Back = "Parties_Back";
        private const string RowName = "Party_";     // Party_1, Party_2, ...
        private static readonly string[] Buttons = { Refresh_, Host, House, Code, More, Back };

        /// <summary>What this page adds to the hint line.</summary>
        public const string HintMore = "Join With Code reads a code you copied (a private party)";

        private readonly MenuModel _model;
        private readonly string[] _rows;            // the page's party slabs, top to bottom
        private readonly List<NetSession.PartyInfo> _found = new List<NetSession.PartyInfo>();
        private int _first;                         // the first party on show
        private bool _looking;

        /// <summary>What the list is doing, for the hint line ("" when there is nothing to say).</summary>
        public string Status { get; private set; } = "";
        /// <summary>Status changed.</summary>
        public event System.Action StatusChanged;

        private MenuParties(MenuModel model, string[] rows) { _model = model; _rows = rows; }

        /// <summary>Makes the Parties page on a model (its slabs once). Null when the model has no such page in its Made
        /// Controls, no party slab, or one of its buttons missing: the title opens the old panel.</summary>
        public static MenuParties Make(MenuModel model)
        {
            if (model == null) return null;
            if (!model.MakePage(Page, out string missing)) { Debug.LogWarning("MenuParties: the old party list shows (" + missing + ").", model); return null; }
            foreach (string name in Buttons)
                if (!model.TryGet(name, out _)) { Debug.LogWarning("MenuParties: the old party list shows (no " + name + " on the page " + Page + ").", model); return null; }
            var rows = new List<string>();
            for (int n = 1; model.TryGet(RowName + n, out MenuModel.Control row) && row.Page == Page; n++) rows.Add(row.Name);
            if (rows.Count == 0) { Debug.LogWarning("MenuParties: the old party list shows (no " + RowName + "1 on the page " + Page + ").", model); return null; }
            return new MenuParties(model, rows.ToArray());
        }

        /// <summary>One of this page's controls (Back among them).</summary>
        public bool Owns(string control)
        {
            foreach (string name in Buttons) if (name == control) return true;
            foreach (string name in _rows) if (name == control) return true;
            return false;
        }

        /// <summary>Before the page is shown: its buttons' words, and no party slabs yet (they drop in as the list comes).
        /// Search once it is up.</summary>
        public void Open()
        {
            _model.SetWords(Refresh_, "Refresh", "look again");
            _model.SetWords(Host, "Host New Party", "a world in the Stadium");
            _model.SetWords(House, "Host House Party", "a co-op sandbox in the house");
            _model.SetWords(Code, "Join With Code", "the code you copied");
            _model.SetWords(Back, "Back");
            _found.Clear(); _first = 0;
            ShowRows();
        }

        /// <summary>A control of the page was chosen. Back is left to the owner.</summary>
        public void Choose(string control)
        {
            switch (control)
            {
                case Refresh_: Search(); return;
                case Host: Say("Starting a party in the Stadium..."); NetSession.Host(); return;
                case House: Say("Starting a party in the house (co-op sandbox)..."); NetSession.HostHouse(); return;   // D-0125
                case Code: Say("Joining with the copied code..."); NetSession.Join(); return;
                case More:
                    if (_found.Count <= _rows.Length) return;
                    _first += _rows.Length;
                    if (_first >= _found.Count) _first = 0;
                    ShowRows();
                    return;
            }
            for (int k = 0; k < _rows.Length; k++)
            {
                if (_rows[k] != control) continue;
                int i = _first + k;
                if (i >= _found.Count) return;
                Say("Joining...");
                NetSession.JoinParty(_found[i].Id);
                return;
            }
        }

        /// <summary>Asks NetSession for the open parties; the slabs show them when the answer comes, if the page is still up
        /// (call it with the page up). A look already under way is not started twice.</summary>
        public async void Search()
        {
            if (_looking) return;
            _looking = true;
            Say("Looking for parties...");
            _found.Clear(); _first = 0;
            ShowRows();
            List<NetSession.PartyInfo> parties = await NetSession.FindParties();
            _looking = false;
            if (_model == null || _model.Page != Page) return;      // the page was left, or the menu is gone
            _found.AddRange(parties);
            ShowRows();
            Say(parties.Count == 0 ? "No open parties right now. Host one, or press Quick Play on the Online page." : "");
        }

        /// <summary>The party slabs for the parties from First on (one each; a slab with none is left off), and More.</summary>
        private void ShowRows()
        {
            for (int k = 0; k < _rows.Length; k++)
            {
                int i = _first + k;
                bool has = i < _found.Count;
                if (has)
                {
                    NetSession.PartyInfo p = _found[i];
                    _model.SetWords(_rows[k], Clip(p.Name), p.World + " · " + p.Players + "/" + p.Max);
                    _model.SetUsable(_rows[k], true);
                }
                _model.SetHidden(_rows[k], !has);
            }
            int pages = (_found.Count + _rows.Length - 1) / _rows.Length;
            _model.SetWords(More, "More", pages > 1 ? (_first / _rows.Length + 1) + " of " + pages : null);
            _model.SetUsable(More, pages > 1);
        }

        private void Say(string status)
        {
            if (status == Status) return;
            Status = status;
            StatusChanged?.Invoke();
        }

        private static string Clip(string s) => string.IsNullOrEmpty(s) ? "A party" : (s.Length > 28 ? s.Substring(0, 28) : s).Replace("<", "").Replace(">", "");
    }
}
