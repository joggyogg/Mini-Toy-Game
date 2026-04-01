using UnityEngine;

/// <summary>
/// Describes one train option that can appear in the conductor catalog.
/// Each definition points at a prefab with a Locomotive component.
/// </summary>
[CreateAssetMenu(menuName = "Mini Toy Game/Railroad/Train Definition", fileName = "TrainDefinition")]
public class TrainDefinition : ScriptableObject
{
    [Header("Display")]
    [SerializeField] private string displayName = "New Train";
    [SerializeField] private Sprite icon;

    [Header("Prefab")]
    [Tooltip("Prefab with a Locomotive component at its root.")]
    [SerializeField] private Locomotive locomotivePrefab;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public Sprite Icon => icon;
    public Locomotive LocomotivePrefab => locomotivePrefab;
}
