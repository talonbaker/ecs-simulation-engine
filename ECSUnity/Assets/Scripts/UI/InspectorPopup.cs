using UnityEngine;
using UnityEngine.UI;

public sealed class InspectorPopup : MonoBehaviour
{
    [Tooltip("Root canvas of the popup. Must be Screen Space - Overlay.")]
    [SerializeField] private Canvas _canvas;

    [Tooltip("UI Text component for the Name line.")]
    [SerializeField] private Text _nameText;

    [Tooltip("UI Text component for the Drives line.")]
    [SerializeField] private Text _drivesText;

    [Tooltip("UI Text component for the Mood line.")]
    [SerializeField] private Text _moodText;

    [Tooltip("Pixel offset from cursor (positive x = right, positive y = downward).")]
    [SerializeField] private Vector2 _cursorOffset = new Vector2(20f, 20f);

    [Tooltip("Padding inside the popup panel in pixels.")]
    [Range(0f, 50f)]
    [SerializeField] private float _panelPadding = 12f;

    private bool _isVisible;
    private RectTransform _panelRect;

    private void Awake()
    {
        if (_canvas != null)
            _canvas.enabled = false;

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

    private void Start()
    {
        if (_canvas == null) return;

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

    public void Show(InspectorPopupData data)
    {
        if (_nameText   != null) _nameText.text   = "Name: "   + data.Name;
        if (_drivesText != null) _drivesText.text = "Drives: " + data.Drives;
        if (_moodText   != null) _moodText.text   = "Mood: "   + data.Mood;

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
