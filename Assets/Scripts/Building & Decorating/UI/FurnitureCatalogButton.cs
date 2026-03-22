using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plain-English purpose:
/// One button in the furniture catalog scroll list.
/// DecorateCatalogUI instantiates one of these per FurnitureDefinition and binds it to the definition.
///
/// Expects the prefab to have:
///   - An Image component on the same GameObject (or assigned to iconImage) for the furniture icon.
///   - A TMP_Text component assigned to nameLabel for the item name.
///   - A Button component on the same GameObject or assigned to button.
/// </summary>
[RequireComponent(typeof(Button))]
public class FurnitureCatalogButton : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameLabel;

    private FurnitureDefinition definition;
    private Action<FurnitureDefinition> onSelected;

    /// <summary>
    /// Binds this button to a definition. Call this immediately after instantiation.
    /// </summary>
    public void Initialise(FurnitureDefinition def, Action<FurnitureDefinition> selectionCallback)
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
    }

    private void HandleClick()
    {
        onSelected?.Invoke(definition);
    }
}
