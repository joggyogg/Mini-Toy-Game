using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plain-English purpose:
/// One button in the furniture catalog scroll list.
/// DecorateCatalogUI instantiates one of these per FurnitureDefinition and binds it to the definition.
///
/// Single-variant definitions: clicking immediately fires the selection callback.
/// Multi-variant definitions: clicking toggles a sub-panel of variant buttons. Clicking a variant
/// button fires the callback with that variant's index and closes the panel.
///
/// Expects the prefab to have:
///   - An Image component assigned to iconImage for the furniture icon.
///   - A TMP_Text component assigned to nameLabel for the item name.
///   - A Button component on this GameObject.
///   - (Optional) A child GameObject assigned to variantPanel used as the sub-button container.
///     Give it a HorizontalLayoutGroup or GridLayoutGroup and set it inactive by default.
///     If not assigned, multi-variant definitions will spawn with the first variant.
/// </summary>
[RequireComponent(typeof(Button))]
public class FurnitureCatalogButton : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameLabel;

    [Tooltip("Optional child container for variant sub-buttons. " +
             "Leave unassigned to always spawn the first variant without showing a sub-panel.")]
    [SerializeField] private Transform variantPanel;

    [Tooltip("Size in pixels of each dynamically created variant sub-button.")]
    [SerializeField] private float variantButtonSize = 48f;

    private FurnitureDefinition definition;
    private Action<FurnitureDefinition, int> onSelected;
    private bool variantPanelOpen;
    private readonly List<GameObject> variantButtons = new List<GameObject>();

    /// <summary>
    /// Binds this button to a definition. Call this immediately after instantiation.
    /// </summary>
    public void Initialise(FurnitureDefinition def, Action<FurnitureDefinition, int> selectionCallback)
    {
        definition = def;
        onSelected = selectionCallback;

        if (nameLabel != null) nameLabel.text = def.DisplayName;
        if (iconImage != null)
        {
            iconImage.sprite = def.Icon;
            iconImage.enabled = def.Icon != null;
        }

        GetComponent<Button>().onClick.AddListener(HandleClick);

        if (def.HasVariants && variantPanel != null)
        {
            BuildVariantSubButtons();
            variantPanel.gameObject.SetActive(false);
        }
    }

    private void HandleClick()
    {
        if (definition.HasVariants && variantPanel != null)
        {
            // Toggle the variant sub-panel.
            variantPanelOpen = !variantPanelOpen;
            variantPanel.gameObject.SetActive(variantPanelOpen);
        }
        else if (definition.HasVariants)
        {
            // No panel assigned — just spawn the first variant.
            onSelected?.Invoke(definition, 0);
        }
        else
        {
            // Single-prefab definition — fire directly.
            onSelected?.Invoke(definition, -1);
        }
    }

    private void BuildVariantSubButtons()
    {
        // Clear any previously built buttons.
        foreach (GameObject go in variantButtons)
            if (go != null) Destroy(go);
        variantButtons.Clear();

        IReadOnlyList<FurnitureVariant> variants = definition.Variants;
        for (int i = 0; i < variants.Count; i++)
        {
            int capturedIndex = i;
            FurnitureVariant variant = variants[i];

            // Create a simple button: RectTransform + Image (icon) + Button.
            var go = new GameObject($"Variant_{i}_{variant.variantName}", typeof(RectTransform));
            go.transform.SetParent(variantPanel, false);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(variantButtonSize, variantButtonSize);

            var img = go.AddComponent<Image>();
            img.sprite = variant.icon != null ? variant.icon : definition.Icon;
            img.preserveAspect = true;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            // Tooltip-style label underneath the icon (optional — only shown if variant has a name).
            if (!string.IsNullOrWhiteSpace(variant.variantName))
            {
                var labelGo = new GameObject("Label", typeof(RectTransform));
                labelGo.transform.SetParent(go.transform, false);
                var labelRt = labelGo.GetComponent<RectTransform>();
                labelRt.anchorMin = new Vector2(0f, 0f);
                labelRt.anchorMax = new Vector2(1f, 0.3f);
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;
                var tmp = labelGo.AddComponent<TextMeshProUGUI>();
                tmp.text = variant.variantName;
                tmp.fontSize = 8f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.raycastTarget = false;
            }

            btn.onClick.AddListener(() =>
            {
                variantPanelOpen = false;
                variantPanel.gameObject.SetActive(false);
                onSelected?.Invoke(definition, capturedIndex);
            });

            variantButtons.Add(go);
        }
    }
}
