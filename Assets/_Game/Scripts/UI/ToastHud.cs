using System;
using System.Collections.Generic;
using System.Text;
using Febucci.TextAnimatorForUnity.TextMeshPro;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShiftJam
{
    /// <summary>
    /// Draws <see cref="Toasts"/> (D-0162): the "Toy Box" sticker cards from the HUD mockup (cream card, thick dark
    /// outline, a drop shadow, a coloured badge with the kind's icon, a small label over the line). They drop in from the
    /// top with a slight tilt, stay longer for longer lines, a refusal shakes, a repeat bumps its x-count, and the oldest
    /// leaves early when a fourth arrives. One for the whole game (kept across scene loads), hidden under menus.
    /// It also draws the BIG CARD (<see cref="Toasts.CardFor"/>, D-0203): one short line in big letters on a sticker
    /// card of the moment's own colour, a little over the middle of the screen (clear of a player standing in the
    /// middle), tilted. It pops in, its letters bounce (TextFx), it holds for a breath and swells away. One at a time:
    /// a new one takes the place of the last.
    /// And the SPEECH STICKER (<see cref="Toasts.Say"/>, D-0213): the toy itself talking. The same sticker card in a paler
    /// cream with no badge, a tail on its bottom edge pointing down at the toy, its words typed out letter by letter on
    /// real time (Text Animator shows each letter, each one growing in), one word wobbling, then held while it is read.
    /// It goes to the top of the stack, is never pushed out early by other toasts, and waits (does not run on) while the
    /// pause menu is open.
    /// </summary>
    public sealed class ToastHud : MonoBehaviour
    {
        private const string FontPath = "HUD/Fredoka SDF";
        private const int MaxShown = 3;
        private const float Top = 26f, Gap = 14f, MaxTextWidth = 640f, Badge = 52f, Pad = 16f, Outline = 4f, Shadow = 6f;
        private const float InSeconds = 0.35f, OutSeconds = 0.35f;
        // The big card, at 1920 by 1080: its letters, the card round them, where it stands (over the middle), its tilt,
        // and its life (pop in, hold, swell away).
        private const float CardLetters = 96f, CardSpacing = 4f, CardPadX = 56f, CardPadY = 20f, CardOutline = 7f, CardShadow = 10f, CardRound = 34f;
        private const float CardRise = 190f, CardTilt = -2.5f, CardWidest = 1500f;
        private const float CardIn = 0.3f, CardHold = 1.2f, CardOut = 0.35f, CardFrom = 0.45f, CardSwell = 0.18f;
        // The speech sticker, at 1920 by 1080: its letters, the padding round them, its tail (a square turned 45 degrees,
        // half of it below the card) and where along the bottom edge the tail sits (share of the card's width from the left).
        private const float SayLetters = 28f, SayPadX = 24f, SayTail = 26f, SayTailAt = 0.3f, SayTilt = -1f;
        private static readonly Color Ink = new Color32(0x1f, 0x1b, 0x2e, 0xff);

        private struct Look { public Color fill, badge, label; public string name; public UiAudio.Kind sound; public float seconds; }

        private static Look LookOf(ToastKind kind)
        {
            switch (kind)
            {
                case ToastKind.Got: return new Look { fill = Hex(0xfff7e0), badge = Hex(0xffd166), label = Hex(0x92400e), name = "GOT IT", sound = UiAudio.Kind.Confirm, seconds = 4f };
                case ToastKind.Refused: return new Look { fill = Hex(0xffe1dc), badge = Hex(0xf87171), label = Hex(0xb42318), name = "CAN'T", sound = UiAudio.Kind.Denied, seconds = 2.6f };
                case ToastKind.Warning: return new Look { fill = Hex(0xfef3c7), badge = Hex(0xfbbf24), label = Hex(0x92400e), name = "WATCH OUT", sound = UiAudio.Kind.Toast, seconds = 4f };
                case ToastKind.Core: return new Look { fill = Hex(0xede9fe), badge = Hex(0xc4b5fd), label = Hex(0x6d28d9), name = "CORE", sound = UiAudio.Kind.Win, seconds = 4f };
                case ToastKind.Say: return new Look { fill = Hex(0xfffdf6), badge = Hex(0xfffdf6), label = Hex(0x6d28d9), name = "", sound = UiAudio.Kind.Toast, seconds = 4.5f };   // its sound and hold come from its SayStyle
                default: return new Look { fill = Hex(0xe0f2fe), badge = Hex(0x8ecae6), label = Hex(0x0369a1), name = "TIP", sound = UiAudio.Kind.Toast, seconds = 5f };
            }
        }

        private sealed class Card
        {
            public RectTransform rt;
            public CanvasGroup group;
            public TextMeshProUGUI count;
            public string text, title;
            public ToastKind kind;
            public int times = 1;
            public float born, until, bumped = -1f, height, y, tilt;
            // A speech sticker's (Say) only: room its tail takes below the card, its letters' animator, when each letter
            // shows (seconds after typeFrom), how many show so far, and whether the typing is over.
            public float below, typeFrom;
            public TextAnimator_TMP anim;
            public float[] letterAt;
            public int shown;
            public bool typed;
        }

        /// <summary>A line the toy will say once its wait is over (SayStyle.delay).</summary>
        private sealed class Waiting
        {
            public string text, title;
            public SayStyle style;
            public float at;
        }

        /// <summary>The big card on screen now (one at a time).</summary>
        private sealed class BigCard
        {
            public RectTransform rt;
            public CanvasGroup group;
            public float born, until;
        }

        private static ToastHud _instance;
        private readonly List<Card> _cards = new List<Card>();
        private readonly List<Waiting> _waiting = new List<Waiting>();
        private BigCard _big;
        private RectTransform _root;
        private CanvasGroup _group;
        private TMP_FontAsset _font;
        private int _made;

        public static ToastHud Get()
        {
            if (_instance != null) return _instance;
            var go = new GameObject("ToastHud");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ToastHud>();
            _instance.Build();
            return _instance;
        }

        public void Add(string text, ToastKind kind, string title)
        {
            text ??= ""; title ??= "";
            float now = Time.unscaledTime;
            var look = LookOf(kind);
            foreach (var c in _cards)
                if (c.kind == kind && c.text == text && c.title == title && now < c.until - OutSeconds)
                {
                    c.times++;
                    c.count.text = "x" + c.times;
                    c.until = now + Life(look, text);
                    c.bumped = now;
                    UiAudio.Play(look.sound);
                    return;
                }
            if (_cards.Count >= MaxShown) LeaveEarly(now);
            var card = Make(text, kind, title, look);
            card.born = now; card.until = now + Life(look, text);
            card.tilt = (_made++ % 2 == 0 ? -1.4f : 1f);
            card.y = -1f;   // placed on its first frame (Update), then the drop-in comes from the offset
            _cards.Add(card);
            UiAudio.Play(look.sound);
        }

        private static float Life(Look look, string text) => look.seconds + Mathf.Clamp(text.Length * 0.03f, 0f, 3f);

        /// <summary>A fourth card is coming: the oldest card that is not the toy talking leaves early (a line being read
        /// is never pushed out).</summary>
        private void LeaveEarly(float now)
        {
            foreach (var c in _cards)
                if (c.kind != ToastKind.Say) { c.until = Mathf.Min(c.until, now + OutSeconds); return; }
        }

        /// <summary>The toy says <paramref name="text"/> (<see cref="Toasts.Say"/>): now, or after its style's delay.</summary>
        public void AddSay(string text, SayStyle style, string title)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (style.delay > 0f) { _waiting.Add(new Waiting { text = text, title = title ?? "", style = style, at = Time.unscaledTime + style.delay }); return; }
            MakeSay(text, style, title ?? "");
        }

        private void MakeSay(string text, SayStyle style, string title)
        {
            float now = Time.unscaledTime;
            // The same line still up (a second New Game straight after the first): that one goes, this one types afresh.
            for (int i = _cards.Count - 1; i >= 0; i--)
                if (_cards[i].kind == ToastKind.Say && _cards[i].text == text) { Destroy(_cards[i].rt.gameObject); _cards.RemoveAt(i); }
            if (_cards.Count >= MaxShown) LeaveEarly(now);
            var card = MakeSpeech(text, title, LookOf(ToastKind.Say), style);
            card.born = now;
            card.typeFrom = now + InSeconds * 0.5f;   // the letters start as the card lands
            float typing = card.letterAt.Length > 0 ? card.letterAt[card.letterAt.Length - 1] : 0f;
            card.until = card.typeFrom + typing + Mathf.Max(0f, style.holdSeconds) + OutSeconds;
            card.tilt = SayTilt;
            card.y = -1f;
            _cards.Insert(0, card);   // the toy's words sit at the top: other toasts stack under them
            UiAudio.Play(style.sound);
        }

        private void Update()
        {
            bool hidden = PauseMenu.IsOpen || TitleMenu.IsOpen;
            _group.alpha = hidden ? 0f : 1f;
            float now = Time.unscaledTime, dt = Time.unscaledDeltaTime;
            bool paused = PauseMenu.IsOpen;   // the toy's words wait for the pause to close (the title lets them run out)
            for (int i = 0; i < _waiting.Count; i++)
            {
                var w = _waiting[i];
                if (paused) { w.at += dt; continue; }
                if (now < w.at) continue;
                _waiting.RemoveAt(i); i--;
                MakeSay(w.text, w.style, w.title);
            }
            float y = Top;
            for (int i = 0; i < _cards.Count; i++)
            {
                var c = _cards[i];
                if (paused && c.kind == ToastKind.Say) { c.born += dt; c.until += dt; c.typeFrom += dt; }
                if (now >= c.until) { Destroy(c.rt.gameObject); _cards.RemoveAt(i); i--; continue; }
                if (c.kind == ToastKind.Say) Type(c, now);
                c.y = c.y < 0f ? y : Mathf.Lerp(c.y, y, 1f - Mathf.Exp(-14f * dt));   // slides up when one above leaves
                float tIn = Mathf.Clamp01((now - c.born) / InSeconds);
                float drop = (1f - EaseOutBack(tIn)) * (c.height + c.below + Top);
                float shake = c.kind == ToastKind.Refused && now - c.born < 0.4f ? Mathf.Sin((now - c.born) * 60f) * 10f * (1f - (now - c.born) / 0.4f) : 0f;
                float tOut = Mathf.Clamp01((c.until - now) / OutSeconds);
                c.group.alpha = Mathf.Min(tIn * 2f, 1f) * tOut;
                float bump = c.bumped >= 0f ? 1f + 0.12f * Mathf.Clamp01(1f - (now - c.bumped) / 0.25f) : 1f;
                c.rt.anchoredPosition = new Vector2(shake, -(c.y - drop) + (1f - tOut) * 20f);
                c.rt.localScale = Vector3.one * bump;
                c.rt.localRotation = Quaternion.Euler(0f, 0f, c.tilt);
                y += c.height + c.below + Gap + Shadow;
            }
            if (_big != null) ShowBig(now);
        }

        /// <summary>A big card: <paramref name="text"/> (one short line) on a sticker card of <paramref name="fill"/>.
        /// One still showing is taken away at once.</summary>
        public void AddCard(string text, Color fill)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (_big != null) { Destroy(_big.rt.gameObject); _big = null; }

            // Measure the plain words first (the bounce tags are not letters): the card fits them.
            var probe = Label(_root, "Probe", text, CardLetters, Ink, true);
            probe.characterSpacing = CardSpacing;
            probe.textWrappingMode = TextWrappingModes.NoWrap;
            Vector2 size = probe.GetPreferredValues(text, CardWidest, 0f);
            Destroy(probe.gameObject);
            float w = Mathf.Min(size.x, CardWidest) + CardPadX * 2f, h = size.y + CardPadY * 2f;

            var rt = Rect("BigCard", _root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, CardRise), new Vector2(w, h));
            var group = rt.gameObject.AddComponent<CanvasGroup>(); group.alpha = 0f;
            HudShapes.RoundedImage(Rect("Shadow", rt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, -CardShadow), Vector2.zero), Ink, CardRound);
            HudShapes.RoundedImage(Rect("Outline", rt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), Ink, CardRound);
            HudShapes.RoundedImage(Rect("Fill", rt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-CardOutline * 2f, -CardOutline * 2f)), fill, CardRound - CardOutline);
            var words = Label(rt, "Words", text, CardLetters, Ink, true);
            words.characterSpacing = CardSpacing;
            words.textWrappingMode = TextWrappingModes.NoWrap;
            words.alignment = TextAlignmentOptions.Center;
            Stretch(words.rectTransform);
            TextFx.Set(TextFx.On(words), words, TextFx.Bounced(text));

            float now = Time.unscaledTime;
            _big = new BigCard { rt = rt, group = group, born = now, until = now + CardIn + CardHold + CardOut };
            rt.localScale = Vector3.one * CardFrom;
            rt.localRotation = Quaternion.Euler(0f, 0f, CardTilt);
        }

        /// <summary>The big card's life: it pops in from small (overshooting a little), holds, then swells and fades.</summary>
        private void ShowBig(float now)
        {
            if (now >= _big.until) { Destroy(_big.rt.gameObject); _big = null; return; }
            float tIn = Mathf.Clamp01((now - _big.born) / CardIn);
            float tOut = Mathf.Clamp01((_big.until - now) / CardOut);   // 1 until it starts to leave, 0 gone
            _big.group.alpha = Mathf.Min(tIn * 3f, 1f) * tOut;
            float scale = Mathf.LerpUnclamped(CardFrom, 1f, EaseOutBack(tIn)) * (1f + CardSwell * (1f - tOut));
            _big.rt.localScale = Vector3.one * scale;
        }

        /// <summary>A speech sticker's typing: shows each letter whose time has come (Text Animator grows it in). At the
        /// end anything still hidden is shown at once, so the line always ends whole.</summary>
        private static void Type(Card c, float now)
        {
            if (c.typed || c.anim == null) return;
            float t = now - c.typeFrom;
            if (t < 0f) return;
            int due = c.shown;
            while (due < c.letterAt.Length && c.letterAt[due] <= t) due++;
            int count = c.anim.CharactersCount;
            int want = Mathf.Min(due, count);
            while (c.shown < want) { c.anim.SetVisibilityChar(c.shown, true); c.shown++; }
            if (due < c.letterAt.Length) return;
            if (count == 0 || c.shown < count) c.anim.SetVisibilityEntireText(true);
            c.typed = true;
        }

        private static float EaseOutBack(float t) { const float k = 1.70158f; t -= 1f; return 1f + (k + 1f) * t * t * t + k * t * t; }

        // ---------- building ----------

        private void Build()
        {
            _font = Resources.Load<TMP_FontAsset>(FontPath);
            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 60;
            var scaler = canvasGo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = BuildUiKit.HudMatch;
            _group = canvasGo.AddComponent<CanvasGroup>(); _group.blocksRaycasts = false; _group.interactable = false;
            _root = (RectTransform)canvasGo.transform;
        }

        private Card Make(string text, ToastKind kind, string title, Look look)
        {
            var card = new Card { text = text, title = title, kind = kind };
            string label = string.IsNullOrEmpty(title) ? look.name : title;
            bool bigTitle = !string.IsNullOrEmpty(title);
            string body = KeyCaps(text);

            // Measure first: the card fits its words, up to MaxTextWidth.
            var probe = Label(_root, "Probe", body, 26f, Ink, false);
            Vector2 bodySize = string.IsNullOrEmpty(text) ? Vector2.zero : probe.GetPreferredValues(body, MaxTextWidth, 0f);
            probe.text = label; probe.fontSize = bigTitle ? 26f : 18f; probe.fontStyle = FontStyles.Bold;
            Vector2 labelSize = probe.GetPreferredValues(label, MaxTextWidth, 0f);
            Destroy(probe.gameObject);
            float textW = Mathf.Min(Mathf.Max(bodySize.x, labelSize.x) + 2f, MaxTextWidth);
            float textH = labelSize.y + (bodySize.y > 0f ? 2f + bodySize.y : 0f);
            float w = Pad + Badge + 14f + textW + Pad + 40f;   // 40: room for the x-count
            float h = Mathf.Max(Badge + Pad * 2f, textH + Pad * 2f);
            card.height = h;

            var rt = Rect("Toast", _root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(w, h));
            card.rt = rt;
            card.group = rt.gameObject.AddComponent<CanvasGroup>(); card.group.alpha = 0f;

            HudShapes.RoundedImage(Rect("Shadow", rt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, -Shadow), Vector2.zero), Ink, 22f);
            HudShapes.RoundedImage(Rect("Outline", rt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), Ink, 22f);
            HudShapes.RoundedImage(Rect("Fill", rt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-Outline * 2f, -Outline * 2f)), look.fill, 22f - Outline);

            var badge = Rect("Badge", rt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(Pad, 0f), new Vector2(Badge, Badge));
            HudShapes.RoundedImage(badge, Ink, 14f);
            var badgeFill = Rect("Fill", badge, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-6f, -6f));
            HudShapes.RoundedImage(badgeFill, look.badge, 11f);
            Sprite icon = kind == ToastKind.Got ? HudShapes.Check : kind == ToastKind.Refused ? HudShapes.Cross : kind == ToastKind.Core ? HudShapes.Ring : null;
            if (icon != null)
            {
                var iconRt = Rect("Icon", badgeFill, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 34f));
                var img = iconRt.gameObject.AddComponent<Image>(); img.sprite = icon; img.color = Ink; img.raycastTarget = false;
            }
            else
            {
                var glyph = Label(badgeFill, "Glyph", kind == ToastKind.Warning ? "!" : "?", 32f, Ink, true);
                glyph.alignment = TextAlignmentOptions.Center;
                Stretch(glyph.rectTransform);
            }

            float textX = Pad + Badge + 14f;
            var textBox = Rect("Words", rt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(textX, 0f), new Vector2(textW, textH));
            var head = Label(textBox, "Label", label, bigTitle ? 26f : 18f, bigTitle ? Ink : look.label, true);
            head.characterSpacing = bigTitle ? 0f : 6f;
            Pin(head.rectTransform, 0f, labelSize.y);
            if (bodySize.y > 0f)
            {
                var line = Label(textBox, "Line", body, 26f, Ink, false);
                line.fontWeight = FontWeight.SemiBold;
                Pin(line.rectTransform, labelSize.y + 2f, bodySize.y);
            }

            card.count = Label(rt, "Count", "", 24f, Ink, true);
            var countRt = card.count.rectTransform;
            countRt.anchorMin = countRt.anchorMax = new Vector2(1f, 0.5f); countRt.pivot = new Vector2(1f, 0.5f);
            countRt.anchoredPosition = new Vector2(-Pad, 0f); countRt.sizeDelta = new Vector2(48f, 32f);
            card.count.alignment = TextAlignmentOptions.Right;
            return card;
        }

        /// <summary>The speech sticker: the cream card with its outline and drop shadow, a tail on the bottom edge, an
        /// optional small name, and the words, laid out whole from the start (hidden, then shown letter by letter) so
        /// nothing moves as they type.</summary>
        private Card MakeSpeech(string text, string title, Look look, SayStyle style)
        {
            var card = new Card { text = text, title = title, kind = ToastKind.Say };
            bool named = !string.IsNullOrEmpty(title);
            card.letterAt = LetterTimes(Plain(text), style);

            // Measure first, the way the line will be drawn: the card fits its words, up to MaxTextWidth.
            var probe = Label(_root, "Probe", text, SayLetters, Ink, false);
            probe.fontWeight = FontWeight.SemiBold;
            Vector2 bodySize = probe.GetPreferredValues(text, MaxTextWidth, 0f);
            Vector2 nameSize = Vector2.zero;
            if (named)
            {
                probe.fontSize = 18f; probe.fontWeight = FontWeight.Regular; probe.fontStyle = FontStyles.Bold;
                nameSize = probe.GetPreferredValues(title, MaxTextWidth, 0f);
            }
            Destroy(probe.gameObject);
            float textW = Mathf.Min(Mathf.Max(bodySize.x, nameSize.x) + 2f, MaxTextWidth);
            float textH = (named ? nameSize.y + 2f : 0f) + bodySize.y;
            float w = SayPadX + textW + SayPadX;
            float h = Mathf.Max(Badge + Pad * 2f, textH + Pad * 2f);
            card.height = h;
            float tailHalf = SayTail * 0.7071f;   // half the turned square's diagonal: how far the tail reaches below the card
            card.below = tailHalf;

            var rt = Rect("Speech", _root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(w, h));
            card.rt = rt;
            card.group = rt.gameObject.AddComponent<CanvasGroup>(); card.group.alpha = 0f;

            // Back to front: the card's shadow and the tail's, the outlines, the tail's fill (over the card's outline, so
            // the tail opens into the card), then the card's fill over the tail's top half.
            float tailX = w * SayTailAt;
            HudShapes.RoundedImage(Rect("Shadow", rt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), new Vector2(0f, -Shadow), Vector2.zero), Ink, 22f);
            Diamond(rt, "TailShadow", tailX, -Shadow, SayTail, Ink);
            HudShapes.RoundedImage(Rect("Outline", rt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero), Ink, 22f);
            Diamond(rt, "TailOutline", tailX, 0f, SayTail, Ink);
            Diamond(rt, "TailFill", tailX, 0f, SayTail - Outline * 2f, look.fill);
            HudShapes.RoundedImage(Rect("Fill", rt, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-Outline * 2f, -Outline * 2f)), look.fill, 22f - Outline);

            var textBox = Rect("Words", rt, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(SayPadX, 0f), new Vector2(textW, textH));
            float top = 0f;
            if (named)
            {
                var head = Label(textBox, "Label", title, 18f, look.label, true);
                head.characterSpacing = 6f;
                Pin(head.rectTransform, 0f, nameSize.y);
                top = nameSize.y + 2f;
            }
            var line = Label(textBox, "Line", "", SayLetters, Ink, false);
            line.fontWeight = FontWeight.SemiBold;
            Pin(line.rectTransform, top, bodySize.y);
            card.anim = TextFx.On(line);
            if (card.anim != null) card.anim.SetText(Spoken(text, style), true);   // laid out whole, every letter hidden
            else line.text = text;
            return card;
        }

        /// <summary>The line as Text Animator reads it: every letter grows in as it shows, and the style's wobble word
        /// (its first match, any case) wears the wobble effect's tag.</summary>
        private static string Spoken(string text, SayStyle style)
        {
            string word = style.wobbleWord != null ? style.wobbleWord.Trim() : "";
            string tag = style.wobbleTag != null ? style.wobbleTag.Trim().Trim('<', '>', '/').Trim() : "";
            int at = word.Length > 0 && tag.Length > 0 ? text.IndexOf(word, StringComparison.OrdinalIgnoreCase) : -1;
            if (at < 0) return TextFx.Popped(text);
            return TextFx.Popped(text.Substring(0, at) + "<" + tag + ">" + text.Substring(at, word.Length) + "</" + tag + ">" + text.Substring(at + word.Length));
        }

        /// <summary>When each letter of <paramref name="plain"/> shows, in seconds from the start: one every
        /// 1 / lettersPerSecond, and a beat (pauseSeconds) after a sentence's end (". ", "... ", "? ", "! ").</summary>
        private static float[] LetterTimes(string plain, SayStyle style)
        {
            var at = new float[plain.Length];
            float step = 1f / Mathf.Max(1f, style.lettersPerSecond), pause = Mathf.Max(0f, style.pauseSeconds), t = 0f;
            for (int i = 0; i < plain.Length; i++)
            {
                at[i] = t;
                t += step;
                char ch = plain[i];
                if ((ch == '.' || ch == '?' || ch == '!') && i + 1 < plain.Length && plain[i + 1] == ' ') t += pause;
            }
            return at;
        }

        /// <summary>The words without any &lt;tag&gt; (TextMeshPro's own, like &lt;b&gt;): the letters that are typed.</summary>
        private static string Plain(string text)
        {
            if (text.IndexOf('<') < 0) return text;
            var sb = new StringBuilder(text.Length);
            bool inTag = false;
            foreach (char ch in text)
            {
                if (ch == '<') { inTag = true; continue; }
                if (inTag) { if (ch == '>') inTag = false; continue; }
                sb.Append(ch);
            }
            return sb.ToString();
        }

        /// <summary>A square of <paramref name="size"/> turned 45 degrees, its middle on the card's bottom edge at
        /// <paramref name="x"/> (lifted by <paramref name="y"/>): the half below the card is the tail.</summary>
        private static void Diamond(RectTransform card, string name, float x, float y, float size, Color color)
        {
            var rt = Rect(name, card, Vector2.zero, Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(size, size));
            rt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        /// <summary>Bold words (the keys in "<b>F</b> shift") get a key-cap tint behind them.</summary>
        private static string KeyCaps(string text) =>
            string.IsNullOrEmpty(text) ? "" : text.Replace("<b>", "<mark=#1F1B2E24 padding=\"8,8,3,3\"><b>").Replace("</b>", "</b></mark>");

        private TextMeshProUGUI Label(Transform parent, string name, string text, float size, Color color, bool bold)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (_font != null) t.font = _font;
            t.text = text; t.fontSize = size; t.color = color; t.raycastTarget = false;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.alignment = TextAlignmentOptions.TopLeft;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            return t;
        }

        private static void Pin(RectTransform rt, float fromTop, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f); rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -fromTop); rt.sizeDelta = new Vector2(0f, height);
        }

        private static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.anchoredPosition = position; rt.sizeDelta = size;
            return rt;
        }

        private static Color Hex(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    }
}
