using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Plain-English purpose:
/// Attach this to the player to control non-movement actions such as opening a player menu and entering build mode.
///
/// The intended setup is that the player has a world-space or overlay canvas attached to them. This script opens and
/// closes that menu, optionally disables player movement while the menu is open, and exposes public methods that UI
/// buttons can call, such as entering build mode.
/// </summary>
public class PlayerInteractionController : MonoBehaviour
{
    [Header("Menu")]
    [SerializeField] private GameObject playerMenuRoot;
    [SerializeField] private CanvasGroup playerMenuCanvasGroup;
    [SerializeField] private bool openMenuOnStart;

    [Header("Behavior")]
    [SerializeField] private bool unlockCursorWhileMenuOpen = true;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private string playerActionMapName = "Player";
    [SerializeField] private string menuToggleActionName = "Interact";
    [SerializeField] private Key fallbackMenuToggleKey = Key.Tab;

    [Header("Events")]
    [SerializeField] private UnityEvent onMenuOpened;
    [SerializeField] private UnityEvent onMenuClosed;
    [SerializeField] private UnityEvent onBuildModeRequested;

    private InputAction menuToggleAction;
    private bool menuOpen;
    private CursorLockMode cachedCursorLockMode = CursorLockMode.None;
    private bool cachedCursorVisible;
    private bool hasCachedCursorState;
    private Button cachedBuildModeButton;
    private RectTransform cachedBuildModeButtonRect;
    private Canvas cachedMenuCanvas;

    /// <summary>
    /// True when the player menu is currently open.
    /// </summary>
    public bool IsMenuOpen => menuOpen;

    private void Awake()
    {
        // Capture the current cursor state before any menu logic runs so startup does not accidentally hide the cursor.
        cachedCursorLockMode = Cursor.lockState;
        cachedCursorVisible = Cursor.visible;
        hasCachedCursorState = true;

        ResolveInputAction();
        ResolveMenuButton();
        ApplyMenuState(openMenuOnStart, true);
    }

    private void OnEnable()
    {
        menuToggleAction?.Enable();
    }

    private void OnDisable()
    {
        menuToggleAction?.Disable();
    }

    private void Update()
    {
        if (menuOpen && WasBuildModeButtonPressedThisFrame())
        {
            RequestEnterBuildMode();
            return;
        }

        if (!WasMenuTogglePressedThisFrame())
        {
            return;
        }

        ToggleMenu();
    }

    /// <summary>
    /// Toggle the player menu between open and closed states.
    /// </summary>
    public void ToggleMenu()
    {
        ApplyMenuState(!menuOpen, false);
    }

    /// <summary>
    /// Open the player menu.
    /// </summary>
    public void OpenMenu()
    {
        ApplyMenuState(true, false);
    }

    /// <summary>
    /// Close the player menu.
    /// </summary>
    public void CloseMenu()
    {
        ApplyMenuState(false, false);
    }

    /// <summary>
    /// Intended to be called by a UI button inside the player menu when the player chooses build mode.
    /// </summary>
    public void RequestEnterBuildMode()
    {
        if (!menuOpen)
        {
            return;
        }

        CloseMenu();
        onBuildModeRequested?.Invoke();
    }

    private void ResolveInputAction()
    {
        if (inputActions == null)
        {
            menuToggleAction = null;
            return;
        }

        InputActionMap playerMap = inputActions.FindActionMap(playerActionMapName, false);
        if (playerMap == null)
        {
            menuToggleAction = null;
            return;
        }

        menuToggleAction = playerMap.FindAction(menuToggleActionName, false);
    }

    private void ResolveMenuButton()
    {
        cachedBuildModeButton = null;
        cachedBuildModeButtonRect = null;
        cachedMenuCanvas = null;

        if (playerMenuRoot == null)
        {
            return;
        }

        cachedBuildModeButton = playerMenuRoot.GetComponentInChildren<Button>(true);
        if (cachedBuildModeButton == null)
        {
            return;
        }

        cachedBuildModeButtonRect = cachedBuildModeButton.GetComponent<RectTransform>();
        cachedMenuCanvas = cachedBuildModeButton.GetComponentInParent<Canvas>();
    }

    private bool WasBuildModeButtonPressedThisFrame()
    {
        if (cachedBuildModeButton == null || cachedBuildModeButtonRect == null)
        {
            ResolveMenuButton();
        }

        if (cachedBuildModeButton == null || cachedBuildModeButtonRect == null)
        {
            return false;
        }

        if (!cachedBuildModeButton.isActiveAndEnabled || !cachedBuildModeButton.interactable)
        {
            return false;
        }

        if (!WasPrimaryClickPressedThisFrame(out Vector2 screenPosition))
        {
            return false;
        }

        Camera eventCamera = null;
        if (cachedMenuCanvas != null && cachedMenuCanvas.renderMode == RenderMode.WorldSpace)
        {
            eventCamera = cachedMenuCanvas.worldCamera != null ? cachedMenuCanvas.worldCamera : Camera.main;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(cachedBuildModeButtonRect, screenPosition, eventCamera);
    }

    private bool WasPrimaryClickPressedThisFrame(out Vector2 screenPosition)
    {
        if (Mouse.current != null)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return Mouse.current.leftButton.wasPressedThisFrame;
        }

        if (Touchscreen.current != null)
        {
            screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
            return Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
        }

        screenPosition = Vector2.zero;
        return false;
    }

    private bool WasMenuTogglePressedThisFrame()
    {
        // Prefer the configured Input System action. If it is not assigned yet, allow a direct keyboard fallback.
        if (menuToggleAction != null)
        {
            bool wasPressed = menuToggleAction.WasPerformedThisFrame() || menuToggleAction.WasPressedThisFrame();
            if (wasPressed)
            {
                return true;
            }
        }

        return WasFallbackMenuTogglePressedThisFrame();
    }

    private bool WasFallbackMenuTogglePressedThisFrame()
    {
        if (Keyboard.current != null && Keyboard.current[fallbackMenuToggleKey].wasPressedThisFrame)
        {
            return true;
        }

        return Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
    }

    private void ApplyMenuState(bool shouldOpen, bool force)
    {
        if (!force && menuOpen == shouldOpen)
        {
            return;
        }

        menuOpen = shouldOpen;

        if (playerMenuRoot != null)
        {
            playerMenuRoot.SetActive(menuOpen);
        }

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
        {
            onMenuOpened?.Invoke();
        }
        else
        {
            onMenuClosed?.Invoke();
        }
    }
}