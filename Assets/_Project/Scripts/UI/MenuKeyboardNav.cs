using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Up/Down + Enter navigation for a column of menu entries. Selection is pushed
/// through the EventSystem rather than tracked privately, so a Button's own
/// transition (if it has one) stays authoritative — no sprite juggling here.
///
/// One instance drives one panel, so the title, the game-over panel and the
/// leaderboard each get their own. Panels that stack declare the ones that can
/// open on top of them in <see cref="_blockedBy"/>, which is what stops an Enter
/// meant for the leaderboard's Close from also firing the title entry behind it.
///
/// The pointer stays fully usable: hovering an entry moves the selection to it,
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

    [Tooltip("Panels that open on top of this one. While any is active this menu " +
             "ignores input, so a keypress only ever reaches the topmost panel.")]
    [SerializeField] private GameObject[] _blockedBy;

    [Tooltip("Optional. Invoked by Escape / Backspace — the 'get me out' entry.")]
    [SerializeField] private Button _cancelButton;

    [Header("Kit metrics")]
    [Tooltip("Gap between the button's left edge and the cursor.")]
    [SerializeField] private float _cursorGap = 40f;

    [Header("Kit label colours")]
    [Tooltip("Matches the gold the banner's own PRESS START is drawn in.")]
    [SerializeField] private Color _labelNormal = new Color(0.961f, 0.773f, 0.259f);   // #F5C542
    [SerializeField] private Color _labelSelected = new Color(1f, 0.953f, 0.769f);     // #FFF3C4

    private int _index;

    // "Was this menu actually taking input last frame?" — false while the panel is
    // hidden AND while something is stacked on top of it, so the selection is
    // re-asserted both when the panel returns and when the modal above it closes.
    private bool _wasActionable;

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
        bool actionable = _menuPanel != null && _menuPanel.activeInHierarchy && !IsBlocked();

        // Re-assert selection when this menu takes the keyboard back — after
        // Return to Menu, or after the panel stacked on top of it closes.
        // Otherwise the EventSystem keeps pointing at a now-hidden button.
        if (actionable && !_wasActionable)
            Select(0);
        _wasActionable = actionable;

        if (!actionable || _entries == null || _entries.Length == 0) return;

        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            Step(-1);
        else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            Step(1);
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
                 || Input.GetKeyDown(KeyCode.Space))
            Confirm();
        else if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))
            Cancel();
    }

    // A panel stacked on top owns the keyboard for as long as it is open.
    private bool IsBlocked()
    {
        if (_blockedBy == null) return false;

        for (int i = 0; i < _blockedBy.Length; i++)
            if (_blockedBy[i] != null && _blockedBy[i].activeInHierarchy)
                return true;

        return false;
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

    private void Cancel()
    {
        if (_cancelButton != null && _cancelButton.interactable)
            _cancelButton.onClick.Invoke();
    }
}
