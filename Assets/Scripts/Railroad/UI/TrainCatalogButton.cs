using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One button in the train catalog scroll list.
/// RailDrawingController instantiates one per TrainDefinition and binds it.
/// Clicking fires the selection callback.
/// </summary>
[RequireComponent(typeof(Button))]
public class TrainCatalogButton : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameLabel;

    private TrainDefinition definition;
    private Action<TrainDefinition> onSelected;

    /// <summary>
    /// Binds this button to a definition. Call immediately after instantiation.
    /// </summary>
    public void Initialise(TrainDefinition def, Action<TrainDefinition> selectionCallback)
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
