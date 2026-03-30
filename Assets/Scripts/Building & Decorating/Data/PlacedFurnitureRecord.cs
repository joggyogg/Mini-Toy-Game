/// <summary>
/// Plain-English purpose:
/// Lightweight record kept by FurnitureSpawner for each piece of furniture that
/// has been placed on the terrain grid during this decorate session.
/// PlacementSolver reads these to avoid placing new furniture on occupied cells.
/// </summary>
public sealed class PlacedFurnitureRecord
{
    /// <summary>The live scene instance's PlaceableGridAuthoring component.</summary>
    public readonly PlaceableGridAuthoring Instance;

    /// <summary>The definitional asset this instance was spawned from.</summary>
    public readonly FurnitureDefinition Definition;

    /// <summary>
    /// The terrain subtile cell that aligns with the furniture's male-grid (0,0) corner.
    /// Used for fast occupancy lookup without re-projecting world positions every frame.
    /// </summary>
    public readonly PlacementCandidate Placement;

    public PlacedFurnitureRecord(PlaceableGridAuthoring instance, FurnitureDefinition definition, PlacementCandidate placement)
    {
        Instance = instance;
        Definition = definition;
        Placement = placement;
    }
}
