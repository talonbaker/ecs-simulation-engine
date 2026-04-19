using System.Linq;
using UnityEngine;
using APIFramework.Core;

/// <summary>
/// Draws a retro diagnostic panel on the RIGHT half of the screen using
/// Unity's immediate-mode GUI (OnGUI).  The 3D camera is constrained to the
/// LEFT half; this overlay fills the right half with one entity's biology data.
///
/// LAYOUT
/// ──────
///  Header    — entity name + index counter + Tab key hint
///  Drive     — dominant drive + movement destination
///  GI Column — fill bar + label for each organ (esophagus → bladder)
///  Vitals    — satiation, hydration, energy, sleepiness bars
///  Status    — fridge stock, clock, urgency values
///  Footer    — violation count
///
/// ENTITY CYCLING
/// ──────────────
/// Press Tab to step forward through the living entity list.
/// Press Shift+Tab to step backward.
/// Works in both single-Billy and 100-human stress-test mode.
///
/// No GameObjects or UGUI components are created — everything is drawn via
/// GUI.DrawTexture and GUI.Label each frame.  This keeps setup trivial and
/// avoids RectTransform anchor complexity.
/// </summary>
[DefaultExecutionOrder(100)]   // run after SimulationManager.Update()
public class BiologyOverlayUI : MonoBehaviour
{
    // ── Entity index (which human is displayed) ───────────────────────────────
    private int _entityIndex = 0;   // wraps around [0, LivingEntities.Count)

    // ── Cached 1x1 textures (each a solid colour) ────────────────────────────
    private Texture2D _texBg;           // dark warm-black panel
    private Texture2D _texBarBg;        // very dark bar background
    private Texture2D _texDivider;      // subtle divider line
    private Texture2D _texEso;
    private Texture2D _texStomach;
    private Texture2D _texSI;
    private Texture2D _texLI;
    private Texture2D _texColon;
    private Texture2D _texBladder;
    private Texture2D _texSatiation;
    private Texture2D _texHydration;
    private Texture2D _texEnergy;
    private Texture2D _texSleep;
    private Texture2D _texCritical;
    private Texture2D _texMoving;
    private Texture2D _texFridge;

    // ── GUI Styles ────────────────────────────────────────────────────────────
    private GUIStyle _header;
    private GUIStyle _subHeader;
    private GUIStyle _label;
    private GUIStyle _value;
    private GUIStyle _drive;
    private GUIStyle _small;

    private bool _stylesBuilt = false;

    // ── Colours (matching EcsColors where possible) ──────────────────────────
    private static readonly Color ColBg        = new Color(0.10f, 0.09f, 0.07f, 0.97f);
    private static readonly Color ColBarBg     = new Color(0.06f, 0.05f, 0.04f, 1.00f);
    private static readonly Color ColDivider   = new Color(0.30f, 0.25f, 0.15f, 1.00f);
    private static readonly Color ColText      = new Color(0.88f, 0.82f, 0.68f, 1.00f);
    private static readonly Color ColDim       = new Color(0.55f, 0.50f, 0.38f, 1.00f);
    private static readonly Color ColAccent    = new Color(0.95f, 0.40f, 0.15f, 1.00f);  // orange
    private static readonly Color ColCritical  = new Color(1.00f, 0.20f, 0.10f, 1.00f);
    private static readonly Color ColMoving    = new Color(0.35f, 0.75f, 1.00f, 1.00f);

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        BuildTextures();
    }

    void Update()
    {
        // Tab / Shift+Tab cycle through entities — reads keyboard outside OnGUI
        // so we can use Input.GetKeyDown which is unavailable inside OnGUI.
        var snap = SimulationManager.Snapshot;
        if (snap == null || snap.LivingEntities.Count == 0) return;

        int total = snap.LivingEntities.Count;
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            bool backward = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            _entityIndex = backward
                ? (_entityIndex - 1 + total) % total
                : (_entityIndex + 1) % total;
        }

        // Clamp in case entities are removed at runtime
        _entityIndex = Mathf.Clamp(_entityIndex, 0, total - 1);
    }

    void OnGUI()
    {
        if (!_stylesBuilt) BuildStyles();

        var snap = SimulationManager.Snapshot;
        if (snap == null) { DrawWaiting(); return; }

        int total = snap.LivingEntities.Count;
        if (total == 0) { DrawWaiting(); return; }

        _entityIndex = Mathf.Clamp(_entityIndex, 0, total - 1);
        var billy = snap.LivingEntities[_entityIndex];

        var fridge = snap.WorldObjects.FirstOrDefault(o => o.IsFridge);

        // Panel occupies the right half of the screen.
        int px = Screen.width / 2;
        int pw = Screen.width - px;
        int ph = Screen.height;
        int pad = Mathf.RoundToInt(pw * 0.04f);
        int iw  = pw - pad * 2;    // inner width

        // Background
        GUI.DrawTexture(new Rect(px, 0, pw, ph), _texBg);

        int y = pad;

        // ── Header ────────────────────────────────────────────────────────────
        // Left: panel title.  Right: entity counter + Tab hint.
        string counterLabel = total > 1
            ? $"[TAB]  {_entityIndex + 1} / {total}"
            : "FIG. 01  —  DIAGNOSTIC";
        GUI.Label(new Rect(px + pad, y, iw, 22), "FIG. 01  —  DIAGNOSTIC", _subHeader);
        if (total > 1)
        {
            _small.normal.textColor = ColDim;
            GUI.Label(new Rect(px + pad, y, iw, 22), counterLabel, _value);  // right-aligned via _value style
        }
        y += 24;
        GUI.Label(new Rect(px + pad, y, iw, 34), billy.Name.ToUpper(), _header);
        y += 36;

        string driveText = billy.IsSleeping
            ? "SLEEPING  ZZZ"
            : billy.Dominant.ToString().ToUpper();
        Color driveCol = billy.IsSleeping ? ColDim : ColAccent;
        _drive.normal.textColor = driveCol;
        GUI.Label(new Rect(px + pad, y, iw, 22), $"DRIVE : {driveText}", _drive);
        y += 22;

        if (billy.IsMoving && billy.MoveTarget.Length > 0)
        {
            _small.normal.textColor = ColMoving;
            GUI.Label(new Rect(px + pad, y, iw, 18), $"  -> {billy.MoveTarget.ToUpper()}", _small);
        }
        y += 22;

        DrawDivider(px, y, pw); y += 10;

        // ── GI Tract ──────────────────────────────────────────────────────────
        GUI.Label(new Rect(px + pad, y, iw, 18), "GI TRACT", _subHeader);
        y += 20;

        int barH  = Mathf.RoundToInt(ph * 0.042f);
        int barGap = 4;

        // Transit indicator: how many boluses currently in esophagus
        float esoFill = snap.TransitItems.Any(t => t.TargetEntityId == billy.Id) ? 0.6f : 0f;

        DrawOrganBar(px + pad, y, iw, barH, "ESOPH  ", esoFill,
            false, _texEso);                                            y += barH + barGap;
        DrawOrganBar(px + pad, y, iw, barH, "STOMACH", billy.Satiation / 100f,
            false, _texStomach);                                        y += barH + barGap;
        DrawOrganBar(px + pad, y, iw, barH, "S.INT  ", billy.SiFill,
            false, _texSI);                                             y += barH + barGap;
        DrawOrganBar(px + pad, y, iw, barH, "L.INT  ", billy.LiFill,
            false, _texLI);                                             y += barH + barGap;
        DrawOrganBar(px + pad, y, iw, barH, "COLON  ", billy.ColonFill,
            billy.ColonIsCritical, _texColon);                          y += barH + barGap;
        DrawOrganBar(px + pad, y, iw, barH, "BLADDER", billy.BladderFill,
            billy.BladderIsCritical, _texBladder);

        y += barH + 10;
        DrawDivider(px, y, pw); y += 10;

        // ── Vitals ────────────────────────────────────────────────────────────
        GUI.Label(new Rect(px + pad, y, iw, 18), "VITALS", _subHeader);
        y += 20;

        DrawVitalBar(px + pad, y, iw, barH, "SATIATION", billy.Satiation / 100f, _texSatiation);
        y += barH + barGap;
        DrawVitalBar(px + pad, y, iw, barH, "HYDRATION", billy.Hydration / 100f, _texHydration);
        y += barH + barGap;
        DrawVitalBar(px + pad, y, iw, barH, "ENERGY   ", billy.Energy    / 100f, _texEnergy);
        y += barH + barGap;
        DrawVitalBar(px + pad, y, iw, barH, "SLEEPINES", billy.Sleepiness / 100f, _texSleep);

        y += barH + 10;
        DrawDivider(px, y, pw); y += 10;

        // ── Status ────────────────────────────────────────────────────────────
        GUI.Label(new Rect(px + pad, y, iw, 18), "STATUS", _subHeader);
        y += 22;

        int fridgeStock = fridge != null ? fridge.StockCount : 0;
        Color fridgeCol = fridgeStock > 0 ? ColText : ColCritical;
        _label.normal.textColor = fridgeCol;
        string fridgeLabel = fridgeStock > 0
            ? $"FRIDGE    {fridgeStock} banana{(fridgeStock == 1 ? "" : "s")}"
            : "FRIDGE    EMPTY  (starvation)";
        GUI.Label(new Rect(px + pad, y, iw, 20), fridgeLabel, _label);
        y += 22;

        _label.normal.textColor = ColText;
        GUI.Label(new Rect(px + pad, y, iw, 20),
            $"CLOCK     {snap.Clock.TimeDisplay}  (Day {snap.Clock.DayNumber})", _label);
        y += 22;
        GUI.Label(new Rect(px + pad, y, iw, 20),
            $"EAT URG   {billy.EatUrgency:F1}  /  DRINK URG  {billy.DrinkUrgency:F1}", _label);
        y += 22;

        if (snap.ViolationCount > 0)
        {
            _label.normal.textColor = ColCritical;
            GUI.Label(new Rect(px + pad, y, iw, 20),
                $"VIOLATIONS  {snap.ViolationCount}", _label);
            _label.normal.textColor = ColText;
        }
    }

    // ── Drawing helpers ───────────────────────────────────────────────────────

    private void DrawDivider(int x, int y, int w)
        => GUI.DrawTexture(new Rect(x, y, w, 1), _texDivider);

    private void DrawOrganBar(int x, int y, int w, int h,
                               string name, float fill, bool critical, Texture2D fillTex)
    {
        int labelW = Mathf.RoundToInt(w * 0.22f);
        int pctW   = Mathf.RoundToInt(w * 0.10f);
        int barX   = x + labelW;
        int barW   = w - labelW - pctW - 4;

        // Label
        _label.normal.textColor = ColDim;
        GUI.Label(new Rect(x, y + 1, labelW, h), name, _label);

        // Bar background
        GUI.DrawTexture(new Rect(barX, y, barW, h), _texBarBg);

        // Fill
        float clamped = Mathf.Clamp01(fill);
        if (clamped > 0.001f)
        {
            Texture2D tex = critical ? _texCritical : fillTex;
            if (critical)
            {
                float blink = Mathf.PingPong(Time.time * 3f, 1f);
                GUI.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.5f, 1f, blink));
            }
            GUI.DrawTexture(new Rect(barX, y, Mathf.RoundToInt(barW * clamped), h), tex);
            GUI.color = Color.white;
        }

        // Percentage
        _value.normal.textColor = critical ? ColCritical : ColText;
        GUI.Label(new Rect(barX + barW + 4, y + 1, pctW, h),
            $"{Mathf.RoundToInt(clamped * 100f)}%", _value);
    }

    private void DrawVitalBar(int x, int y, int w, int h,
                               string name, float fill, Texture2D fillTex)
        => DrawOrganBar(x, y, w, h, name, fill, false, fillTex);

    private void DrawWaiting()
    {
        if (!_stylesBuilt) return;
        int px = Screen.width / 2;
        GUI.DrawTexture(new Rect(px, 0, Screen.width - px, Screen.height), _texBg);
        _label.normal.textColor = ColDim;
        GUI.Label(new Rect(px + 20, Screen.height / 2 - 10, 300, 30), "WAITING FOR SIMULATION...", _label);
    }

    // ── Texture / style construction ──────────────────────────────────────────

    private void BuildTextures()
    {
        _texBg       = Tex(ColBg);
        _texBarBg    = Tex(ColBarBg);
        _texDivider  = Tex(ColDivider);
        _texEso      = Tex(new Color(0.45f, 0.45f, 0.50f));
        _texStomach  = Tex(new Color(1.00f, 0.60f, 0.10f));
        _texSI       = Tex(new Color(0.85f, 0.65f, 0.35f));
        _texLI       = Tex(new Color(0.55f, 0.35f, 0.12f));
        _texColon    = Tex(new Color(0.35f, 0.16f, 0.03f));
        _texBladder  = Tex(new Color(0.90f, 0.82f, 0.08f));
        _texSatiation= Tex(new Color(1.00f, 0.55f, 0.05f));
        _texHydration= Tex(new Color(0.15f, 0.65f, 1.00f));
        _texEnergy   = Tex(new Color(0.30f, 0.90f, 0.40f));
        _texSleep    = Tex(new Color(0.50f, 0.40f, 0.80f));
        _texCritical = Tex(ColCritical);
        _texMoving   = Tex(ColMoving);
        _texFridge   = Tex(new Color(0.30f, 0.60f, 0.40f));
    }

    private void BuildStyles()
    {
        _header = new GUIStyle(GUI.skin.label)
        {
            fontSize  = Mathf.RoundToInt(Screen.height * 0.030f),
            fontStyle = FontStyle.Bold,
        };
        _header.normal.textColor = ColText;

        _subHeader = new GUIStyle(GUI.skin.label)
        {
            fontSize  = Mathf.RoundToInt(Screen.height * 0.016f),
            fontStyle = FontStyle.Bold,
        };
        _subHeader.normal.textColor = ColDim;

        _label = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(Screen.height * 0.016f),
        };
        _label.normal.textColor = ColText;

        _value = new GUIStyle(GUI.skin.label)
        {
            fontSize  = Mathf.RoundToInt(Screen.height * 0.015f),
            alignment = TextAnchor.MiddleRight,
        };
        _value.normal.textColor = ColText;

        _drive = new GUIStyle(GUI.skin.label)
        {
            fontSize  = Mathf.RoundToInt(Screen.height * 0.018f),
            fontStyle = FontStyle.Bold,
        };
        _drive.normal.textColor = ColAccent;

        _small = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(Screen.height * 0.014f),
        };
        _small.normal.textColor = ColMoving;

        _stylesBuilt = true;
    }

    private static Texture2D Tex(Color col)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, col);
        t.Apply();
        return t;
    }
}
