using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Up/Down + Enter navigation for the title menu, matching what the footer bar
/// advertises. Selection is pushed through the EventSystem rather than tracked
/// privately, so each Button's own SpriteSwap `selectedSprite` (the kit's
/// button_selected frame) does the highlighting — no sprite juggling here.
///
/// The pointer stays fully usable: hovering a button moves the selection to it,
/// so mouse and keyboard never disagree about what is highlighted.
/// </summary>
public class MenuKeyboardNav : MonoBehaviour
{
    [Header("Menu (assign in Inspector)")]
    [Tooltip("Only navigates while this panel is active.")]
    [SerializeField] private GameObject _menuPanel;

    [Tooltip("Buttons in top-to-bottom screen order.")]
    [SerializeField] private Button[] _entries;

    [Tooltip("Pointer sprite parked to the left of the selected button.")]
    [SerializeField] private RectTransform _cursor;

    [Header("Kit metrics")]
    [Tooltip("Gap between the button's left edge and the cursor.")]
    [SerializeField] private float _cursorGap = 40f;

    [Header("Kit label colours")]
    [SerializeField] private Color _labelNormal = new Color(0.647f, 0.949f, 0.925f);   // #A5F2EC
    [SerializeField] private Color _labelSelected = new Color(0.937f, 1f, 0.988f);     // #EFFFFC

    private int _index;
    private bool _wasPanelActive;

    void Start()
    {
        // Wire hover -> selection so the pointer and the keyboard agree.
        for (int i = 0; i < _entries.Length; i++)
        {
            if (_entries[i] == null) continue;
            int captured = i;
            var trigger = _entries[i].gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = _entries[i].gameObject.AddComponent<EventTrigger>();

            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entry.callback.AddListener(_ => Select(captured));
            trigger.triggers.Add(entry);
        }

        Select(0);
    }

    void Update()
    {
        bool active = _menuPanel != null && _menuPanel.activeInHierarchy;

        // Re-assert selection when the menu comes back (e.g. after Return to Menu),
        // otherwise the EventSystem keeps pointing at a now-hidden button.
        if (active && !_wasPanelActive)
            Select(0);
        _wasPanelActive = active;

        if (!active || _entries == null || _entries.Length == 0) return;

        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            Step(-1);
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            Step(1);
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                 || Input.GetKeyDown(KeyCode.Space))
            Confirm();
    }

    // Wraps around, and skips over any entry that is missing or disabled.
    private void Step(int delta)
    {
        int n = _entries.Length;
        for (int hop = 1; hop <= n; hop++)
        {
            int candidate = ((_index + delta * hop) % n + n) % n;
            var b = _entries[candidate];
            if (b != null && b.gameObject.activeInHierarchy && b.interactable)
            {
                Select(candidate);
                return;
            }
        }
    }

    private void Select(int index)
    {
        if (_entries == null || index < 0 || index >= _entries.Length) return;
        _index = index;

        var button = _entries[_index];
        if (button == null) return;

        // Driving the EventSystem is what makes the Button paint its own
        // `selectedSprite`; setting the sprite by hand here would be overwritten
        // by Selectable's own state machine on the next transition.
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(button.gameObject);

        for (int i = 0; i < _entries.Length; i++)
        {
            if (_entries[i] == null) continue;
            var label = _entries[i].GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.color = (i == _index) ? _labelSelected : _labelNormal;
        }

        ParkCursor(button);
    }

    private void ParkCursor(Button button)
    {
        if (_cursor == null) return;

        var brt = button.GetComponent<RectTransform>();
        if (brt == null) return;

        // Kit spec: cursor sits `_cursorGap` left of the button edge, centred.
        float x = brt.anchoredPosition.x - brt.rect.width * 0.5f
                  - _cursorGap - _cursor.rect.width * 0.5f;
        _cursor.anchoredPosition = new Vector2(x, brt.anchoredPosition.y);
    }

    private void Confirm()
    {
        if (_index < 0 || _index >= _entries.Length) return;
        var button = _entries[_index];
        if (button != null && button.interactable)
            button.onClick.Invoke();
    }
}
