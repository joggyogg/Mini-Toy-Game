using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Attach this to the player to control non-movement actions such as opening a player menu
/// and entering terraform or decorate mode via two buttons.
///
/// The intended setup is that the player has a world-space or overlay canvas attached to them.
/// This script opens and closes that menu, and exposes three UnityEvents that UI buttons fire:
/// onTerraformRequested, onDecorateRequested, and onConductorRequested. Wire those in the
/// Inspector to GameModeManager.EnterHumanTerraformMode / EnterHumanDecorateMode / EnterHumanConductorMode.
/// </summary>
public class PlayerInteractionController : MonoBehaviour
{
    [Header("Menu")]
    [SerializeField] private GameObject playerMenuRoot;
    [SerializeField] private CanvasGroup playerMenuCanvasGroup;
    [SerializeField] private bool openMenuOnStart;

    [Header("Buttons")]
    [SerializeField] private Button terraformButton;
    [SerializeField] private Button decorateButton;
    [SerializeField] private Button conductorButton;

    [Header("Behavior")]
    [SerializeField] private bool unlockCursorWhileMenuOpen = true;

    [Header("Input")]
    [SerializeField] private Key menuToggleKey = Key.Tab;

    [Header("Events")]
    [SerializeField] private UnityEvent onMenuOpened;
    [SerializeField] private UnityEvent onMenuClosed;
    [SerializeField] private UnityEvent onTerraformRequested;
    [SerializeField] private UnityEvent onDecorateRequested;
    [SerializeField] private UnityEvent onConductorRequested;

    private bool menuOpen;
    private CursorLockMode cachedCursorLockMode = CursorLockMode.None;
    private bool cachedCursorVisible;
    private bool hasCachedCursorState;

    /// <summary>
    /// True when the player menu is currently open.
    /// </summary>
    public bool IsMenuOpen => menuOpen;

    private void Awake()
    {
        cachedCursorLockMode = Cursor.lockState;
        cachedCursorVisible = Cursor.visible;
        hasCachedCursorState = true;

        ApplyMenuState(openMenuOnStart, true);
    }

    private void OnEnable()
    {
        if (terraformButton != null) terraformButton.onClick.AddListener(OnTerraformClicked);
        if (decorateButton != null) decorateButton.onClick.AddListener(OnDecorateClicked);
        if (conductorButton != null) conductorButton.onClick.AddListener(OnConductorClicked);
    }

    private void OnDisable()
    {
        if (terraformButton != null) terraformButton.onClick.RemoveListener(OnTerraformClicked);
        if (decorateButton != null) decorateButton.onClick.RemoveListener(OnDecorateClicked);
        if (conductorButton != null) conductorButton.onClick.RemoveListener(OnConductorClicked);
    }

    private void Update()
    {
        if (!WasMenuTogglePressedThisFrame()) return;
        ToggleMenu();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void ToggleMenu()
    {
        ApplyMenuState(!menuOpen, false);
    }

    public void OpenMenu()
    {
        ApplyMenuState(true, false);
    }

    public void CloseMenu()
    {
        ApplyMenuState(false, false);
    }

    // ── Button handlers ───────────────────────────────────────────────────────

    private void OnTerraformClicked()
    {
        if (!menuOpen) return;
        CloseMenu();
        onTerraformRequested?.Invoke();
    }

    private void OnDecorateClicked()
    {
        if (!menuOpen) return;
        CloseMenu();
        onDecorateRequested?.Invoke();
    }

    private void OnConductorClicked()
    {
        if (!menuOpen) return;
        CloseMenu();
        onConductorRequested?.Invoke();
    }

    // ── Input helpers ─────────────────────────────────────────────────────────

    private bool WasMenuTogglePressedThisFrame()
    {
        if (Keyboard.current != null && Keyboard.current[menuToggleKey].wasPressedThisFrame)
            return true;
        return Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
    }

    private void ApplyMenuState(bool shouldOpen, bool force)
    {
        if (!force && menuOpen == shouldOpen) return;

        menuOpen = shouldOpen;

        if (playerMenuRoot != null)
            playerMenuRoot.SetActive(menuOpen);

        if (playerMenuCanvasGroup != null)
        {
            playerMenuCanvasGroup.alpha = menuOpen ? 1f : 0f;
            playerMenuCanvasGroup.interactable = menuOpen;
            playerMenuCanvasGroup.blocksRaycasts = menuOpen;
        }

        if (unlockCursorWhileMenuOpen)
        {
            if (menuOpen)
            {
                cachedCursorLockMode = Cursor.lockState;
                cachedCursorVisible = Cursor.visible;
                hasCachedCursorState = true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (hasCachedCursorState)
            {
                Cursor.lockState = cachedCursorLockMode;
                Cursor.visible = cachedCursorVisible;
            }
        }

        if (menuOpen)
            onMenuOpened?.Invoke();
        else
            onMenuClosed?.Invoke();
    }
}