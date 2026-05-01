using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class InspectorPopup : MonoBehaviour
{
    [Tooltip("Root canvas of the popup. Must be Screen Space - Overlay.")]
    [SerializeField] private Canvas _canvas;

    // ── Surface tier (live data) ──────────────────────────────────────────────
    [Tooltip("Header label for the Surface tier.")]
    [SerializeField] private TMP_Text _surfaceHeaderText;

    [Tooltip("Body text for the Surface tier (Name + current action).")]
    [SerializeField] private TMP_Text _surfaceBodyText;

    // ── Behaviour tier (stubbed — future packet) ──────────────────────────────
    [Tooltip("Header label for the Behaviour tier (rendered at 50% alpha until live).")]
    [SerializeField] private TMP_Text _behaviourHeaderText;

    [Tooltip("Body text for the Behaviour tier.")]
    [SerializeField] private TMP_Text _behaviourBodyText;

    // ── Internal tier (stubbed — future packet) ───────────────────────────────
    [Tooltip("Header label for the Internal tier (rendered at 50% alpha until live).")]
    [SerializeField] private TMP_Text _internalHeaderText;

    [Tooltip("Body text for the Internal tier.")]
    [SerializeField] private TMP_Text _internalBodyText;

    [Tooltip("Pixel offset from cursor (positive x = right, positive y = downward).")]
    [SerializeField] private Vector2 _cursorOffset = new Vector2(20f, 20f);

    [Tooltip("Padding inside the popup panel in pixels.")]
    [Range(0f, 50f)]
    [SerializeField] private float _panelPadding = 12f;

    private bool _isVisible;
    private bool _externallyDriven;
    private RectTransform _panelRect;

    // Called by WorldStateInspectorBinder.Awake() before this component's
    // Start() fires, suppressing the self-managed SelectionManager subscription.
    public void SetExternallyDriven(bool value) => _externallyDriven = value;

    private void Awake()
    {
        if (_canvas != null)
            _canvas.enabled = false;
    }

    private void Start()
    {
        if (_canvas != null)
        {
            var panelTransform = _canvas.transform.Find("PopupPanel");
            if (panelTransform != null)
                _panelRect = panelTransform as RectTransform;

            var blocker = _canvas.transform.Find("FullscreenBlocker");
            if (blocker != null)
            {
                var btn = blocker.GetComponent<Button>();
                if (btn != null)
                    btn.onClick.AddListener(Hide);
            }
        }

        if (_externallyDriven) return;

        // Fallback: self-manage selection in scenes without a WorldStateInspectorBinder
        // (e.g. the inspector-popup sandbox scene).
        var sm = FindObjectOfType<SelectionManager>();
        if (sm != null)
        {
            sm.OnSelectionChanged += HandleSelectionChanged;
        }
        else
        {
            var fallback = gameObject.AddComponent<PopupClickHandler>();
            fallback.Activate();
            fallback.OnClick += HandlePopupClick;
        }
    }

    public void Show(InspectorPopupData data)
    {
        if (_surfaceHeaderText  != null) _surfaceHeaderText.text  = "— Surface —";
        if (_surfaceBodyText    != null) _surfaceBodyText.text    = data.Surface.Name + " | " + data.Surface.CurrentAction;

        if (_behaviourHeaderText != null) _behaviourHeaderText.text = "— Behaviour —";
        if (_behaviourBodyText   != null) _behaviourBodyText.text   = "(coming soon)";

        if (_internalHeaderText != null) _internalHeaderText.text = "— Internal —";
        if (_internalBodyText   != null) _internalBodyText.text   = "(coming soon)";

        if (_canvas != null) _canvas.enabled = true;
        _isVisible = true;
        UpdatePosition();
    }

    public void Hide()
    {
        if (_canvas != null) _canvas.enabled = false;
        _isVisible = false;
    }

    private void Update()
    {
        if (_isVisible)
            UpdatePosition();
    }

    private void UpdatePosition()
    {
        if (_panelRect == null) return;

        Vector2 mouse  = Input.mousePosition;
        Vector2 offset = _cursorOffset;
        Vector2 size   = _panelRect.rect.size;

        float x = mouse.x + offset.x;
        float y = mouse.y - offset.y;

        // Flip horizontal when popup would extend past the right edge.
        if (x + size.x > Screen.width)
            x = mouse.x - offset.x - size.x;

        // Flip vertical when popup would extend below the bottom edge.
        if (y - size.y < 0f)
            y = mouse.y + offset.y + size.y;

        _panelRect.position = new Vector3(x, y, 0f);
    }

    private void HandleSelectionChanged(Selectable sel)
    {
        if (sel == null) { Hide(); return; }

        var target = sel.GetComponent<InspectorTarget>();
        if (target != null)
            Show(target.Data);
        else
            Hide();
    }

    private void HandlePopupClick(InspectorTarget target)
    {
        if (target != null)
            Show(target.Data);
        else
            Hide();
    }
}
