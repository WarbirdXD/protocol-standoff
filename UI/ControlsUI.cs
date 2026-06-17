using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls reference panel controller.
/// Assign the panel GameObject in the Inspector, then call Show() from a Button's OnClick.
/// In-game the Tab key can also toggle it (allowTabToggle = true).
/// </summary>
public class ControlsUI : MonoBehaviour
{
    [Header("Panel")]
    public GameObject controlsPanel;
    public Button closeButton;

    [Header("Toggle")]
    [Tooltip("Allow Tab key to open/close the panel")]
    public bool allowTabToggle = true;
    public KeyCode toggleKey = KeyCode.Tab;

    [Header("Cursor")]
    [Tooltip("Unlock cursor while panel is open (for in-game use)")]
    public bool unlockCursorWhenOpen = true;

    private bool _cursorWasVisible;
    private CursorLockMode _prevLockMode;

    private void Start()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        if (controlsPanel != null)
            controlsPanel.SetActive(false);
    }

    private void Update()
    {
        if (!allowTabToggle) return;
        if (Input.GetKeyDown(toggleKey))
            Toggle();
    }

    public void Show()
    {
        if (controlsPanel == null) return;
        controlsPanel.SetActive(true);
        if (unlockCursorWhenOpen)
        {
            _cursorWasVisible = Cursor.visible;
            _prevLockMode     = Cursor.lockState;
            Cursor.lockState  = CursorLockMode.None;
            Cursor.visible    = true;
        }
    }

    public void Hide()
    {
        if (controlsPanel == null) return;
        controlsPanel.SetActive(false);
        if (unlockCursorWhenOpen)
        {
            Cursor.visible   = _cursorWasVisible;
            Cursor.lockState = _prevLockMode;
        }
    }

    public void Toggle() { if (controlsPanel != null && controlsPanel.activeSelf) Hide(); else Show(); }
    public bool IsVisible => controlsPanel != null && controlsPanel.activeSelf;
}
