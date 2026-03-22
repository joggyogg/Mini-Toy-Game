using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Plain-English purpose:
/// This editor window is the main UI for painting the male grid and managing the female-grid hierarchy.
/// Female layers now live inside ordered groups, and each group contributes its own spacing rule.
/// </summary>
public class PlaceableGridAuthoringWindow : EditorWindow
{
    private const float CellButtonSize = 26f;
    private const float ColumnWidth = 280f;
    private const float FullTileWorldSize = 1f;
    private static readonly Color FemalePrimaryLight = new Color(0.93f, 0.36f, 0.72f, 0.95f);
    private static readonly Color FemalePrimaryDark = new Color(0.99f, 0.0f, 0.45f, 0.95f);
    private static readonly Color FemaleSecondaryLight = new Color(1f, 0.79f, 0.46f, 0.95f);
    private static readonly Color FemaleSecondaryDark = new Color(1f, 0.63f, 0.0f, 0.95f);
    private static readonly Color MalePrimaryLight = new Color(0.39f, 0.79f, 0.89f, 0.95f);
    private static readonly Color MalePrimaryDark = new Color(0.12f, 0.72f, 0.88f, 0.95f);
    private static readonly Color MaleSecondaryLight = new Color(0.47f, 0.38f, 0.92f, 0.95f);
    private static readonly Color MaleSecondaryDark = new Color(0.26f, 0.0f, 0.95f, 0.95f);
    private static readonly Color InactiveCellColor = new Color(0.22f, 0.22f, 0.22f, 1f);
    private static readonly Color GridLineColor = new Color(0f, 0f, 0f, 0.35f);
    private static readonly Color SelectedListColor = new Color(0.25f, 0.45f, 0.7f, 0.28f);

    private enum GridEditTarget
    {
        Male,
        Female
    }

    private enum SelectionKind
    {
        None,
        Group,
        Layer
    }

    private struct HierarchySelection
    {
        public SelectionKind Kind;
        public FemaleGridGroup ParentGroup;
        public int IndexInParent;
        public FemaleGridGroup Group;
        public FemaleGridLayer Layer;

        public bool IsValid => Kind != SelectionKind.None;
    }

    private PlaceableGridAuthoring targetAuthoring;
    private Vector2 hierarchyScrollPosition;
    private Vector2 matrixScrollPosition;
    private GridEditTarget editTarget = GridEditTarget.Female;
    private HierarchySelection selection;
    private bool isDragPainting;
    private bool dragPaintValue;
    private Vector2Int lastPaintedCell = new Vector2Int(int.MinValue, int.MinValue);

    [MenuItem("Window/Mini Toy Game/Placeable Grid Authoring")]
    public static void OpenWindow()
    {
        Open(null);
    }

    public static void Open(PlaceableGridAuthoring authoring)
    {
        PlaceableGridAuthoringWindow window = GetWindow<PlaceableGridAuthoringWindow>("Placeable Grid Authoring");
        window.minSize = new Vector2(900f, 500f);
        window.SetTarget(authoring);
        window.Show();
    }

    private void OnEnable()
    {
        if (targetAuthoring == null)
        {
            SetTarget(Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<PlaceableGridAuthoring>()
                : null);
        }
    }

    private void OnSelectionChange()
    {
        PlaceableGridAuthoring selectedAuthoring = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponent<PlaceableGridAuthoring>()
            : null;

        if (selectedAuthoring != null)
        {
            SetTarget(selectedAuthoring);
            Repaint();
        }
    }

    private void SetTarget(PlaceableGridAuthoring authoring)
    {
        targetAuthoring = authoring;
        editTarget = GridEditTarget.Female;
        StopDragPainting();
        selection = default;

        if (targetAuthoring == null)
        {
            return;
        }

        targetAuthoring.EnsureValidData();
        EnsureSelectionStillValid();
        EnsureSomeFemaleSelection();
    }

    private void OnGUI()
    {
        DrawToolbar();

        if (targetAuthoring == null)
        {
            DrawEmptyState();
            return;
        }

        targetAuthoring.EnsureValidData();
        EnsureSelectionStillValid();

        EditorGUILayout.BeginHorizontal();
        DrawHierarchyPanel();
        DrawDetailsPanel();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Use Selected", EditorStyles.toolbarButton))
        {
            SetTarget(Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponent<PlaceableGridAuthoring>()
                : null);
        }

        GUILayout.FlexibleSpace();

        if (targetAuthoring != null)
        {
            GUILayout.Label(targetAuthoring.name, EditorStyles.miniBoldLabel);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawEmptyState()
    {
        EditorGUILayout.HelpBox(
            "Select a GameObject with a PlaceableGridAuthoring component to author its male grid and female-grid hierarchy.",
            MessageType.Info);

        if (Selection.activeGameObject != null && GUILayout.Button("Add PlaceableGridAuthoring To Selected Object"))
        {
            Undo.AddComponent<PlaceableGridAuthoring>(Selection.activeGameObject);
            SetTarget(Selection.activeGameObject.GetComponent<PlaceableGridAuthoring>());
        }
    }

    private void DrawHierarchyPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(ColumnWidth));
        EditorGUILayout.LabelField("Hierarchy", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Male Grid: {targetAuthoring.MaleGridSizeInCells.x} x {targetAuthoring.MaleGridSizeInCells.y} cells");
        EditorGUILayout.LabelField($"Enabled Male Tiles: {targetAuthoring.GetEnabledMaleCellCount()}");
        EditorGUILayout.LabelField($"Female Tile Size: {targetAuthoring.FemaleTileSize:0.##}");

        GUIStyle maleButtonStyle = editTarget == GridEditTarget.Male ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
        if (GUILayout.Button("Edit Male Grid", maleButtonStyle))
        {
            editTarget = GridEditTarget.Male;
            StopDragPainting();
        }

        EditorGUILayout.Space(4f);
        hierarchyScrollPosition = EditorGUILayout.BeginScrollView(hierarchyScrollPosition);
        DrawGroupRecursive(targetAuthoring.FemaleGridRootGroup, null, -1, 0);
        EditorGUILayout.EndScrollView();

        DrawHierarchyActions();
        EditorGUILayout.EndVertical();
    }

    private void DrawGroupRecursive(FemaleGridGroup group, FemaleGridGroup parentGroup, int indexInParent, int indent)
    {
        DrawHierarchyEntry(group, parentGroup, indexInParent, indent, true, null);

        for (int i = 0; i < group.Children.Count; i++)
        {
            FemaleGridHierarchyEntry child = group.Children[i];
            if (child.IsGroup)
            {
                DrawGroupRecursive(child.Group, group, i, indent + 1);
                continue;
            }

            DrawHierarchyEntry(null, group, i, indent + 1, false, child.Layer);
        }
    }

    private void DrawHierarchyEntry(FemaleGridGroup group, FemaleGridGroup parentGroup, int indexInParent, int indent, bool isGroup, FemaleGridLayer layer)
    {
        Rect rowRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight + 4f);
        Rect indentedRect = new Rect(rowRect.x + (indent * 14f), rowRect.y, rowRect.width - (indent * 14f), rowRect.height);

        if (IsEntrySelected(group, parentGroup, indexInParent, isGroup, layer))
        {
            EditorGUI.DrawRect(indentedRect, SelectedListColor);
        }

        Rect labelRect = new Rect(indentedRect.x + 2f, indentedRect.y + 2f, indentedRect.width - 90f, EditorGUIUtility.singleLineHeight);
        Rect upRect = new Rect(indentedRect.xMax - 84f, indentedRect.y + 2f, 26f, EditorGUIUtility.singleLineHeight);
        Rect downRect = new Rect(indentedRect.xMax - 56f, indentedRect.y + 2f, 26f, EditorGUIUtility.singleLineHeight);
        Rect intoRect = new Rect(indentedRect.xMax - 28f, indentedRect.y + 2f, 26f, EditorGUIUtility.singleLineHeight);

        string label = isGroup
            ? (parentGroup == null
                ? $"[G] {group.Name}  start={group.StartHeight:0.##}  spacing={group.Spacing:0.##}"
                : $"[G] {group.Name}  spacing={group.Spacing:0.##}")
            : $"[L] {layer.Name}  h={layer.LocalHeight:0.##}";

        if (GUI.Button(labelRect, label, EditorStyles.label))
        {
            SelectHierarchyEntry(group, parentGroup, indexInParent, isGroup, layer);
        }

        using (new EditorGUI.DisabledScope(parentGroup == null || indexInParent <= 0))
        {
            if (GUI.Button(upRect, "^"))
            {
                MoveSelection(parentGroup, indexInParent, indexInParent - 1);
            }
        }

        using (new EditorGUI.DisabledScope(parentGroup == null || indexInParent >= parentGroup.Children.Count - 1))
        {
            if (GUI.Button(downRect, "v"))
            {
                MoveSelection(parentGroup, indexInParent, indexInParent + 1);
            }
        }

        bool canNestIntoPreviousGroup = parentGroup != null && indexInParent > 0 && parentGroup.Children[indexInParent - 1].IsGroup;
        using (new EditorGUI.DisabledScope(!canNestIntoPreviousGroup))
        {
            if (GUI.Button(intoRect, ">"))
            {
                FemaleGridGroup targetGroup = parentGroup.Children[indexInParent - 1].Group;
                MoveSelectionIntoGroup(parentGroup, indexInParent, targetGroup);
            }
        }
    }

    private void DrawHierarchyActions()
    {
        EditorGUILayout.Space();

        if (!selection.IsValid)
        {
            EditorGUILayout.HelpBox("Select a group or layer in the hierarchy to edit it or insert new items nearby.", MessageType.None);
            return;
        }

        if (selection.Kind == SelectionKind.Group)
        {
            if (GUILayout.Button("Add Layer To Selected Group"))
            {
                RecordChange("Add Female Layer To Group");
                FemaleGridLayer layer = targetAuthoring.AddFemaleGridLayer(selection.Group, selection.Group.Children.Count);
                SelectLayerInGroup(selection.Group, layer);
                editTarget = GridEditTarget.Female;
                MarkDirty();
            }

            if (GUILayout.Button("Add Child Group"))
            {
                RecordChange("Add Child Female Group");
                FemaleGridGroup group = targetAuthoring.AddFemaleGridGroup(selection.Group, selection.Group.Children.Count);
                SelectGroupBySearch(group);
                MarkDirty();
            }

            if (selection.ParentGroup != null && GUILayout.Button("Delete Selected Group"))
            {
                RecordChange("Delete Female Group");
                targetAuthoring.RemoveHierarchyEntry(selection.ParentGroup, selection.IndexInParent);
                EnsureSomeFemaleSelection();
                MarkDirty();
            }

            return;
        }

        if (GUILayout.Button("Duplicate Selected Layer"))
        {
            RecordChange("Duplicate Female Layer");
            FemaleGridLayer duplicate = targetAuthoring.DuplicateFemaleGridLayer(selection.ParentGroup, selection.IndexInParent);
            SelectLayerInGroup(selection.ParentGroup, duplicate);
            editTarget = GridEditTarget.Female;
            MarkDirty();
        }

        if (GUILayout.Button("Add Group After Selected Layer"))
        {
            RecordChange("Add Female Group After Layer");
            FemaleGridGroup group = targetAuthoring.AddFemaleGridGroup(selection.ParentGroup, selection.IndexInParent + 1);
            SelectGroupBySearch(group);
            MarkDirty();
        }

        if (GUILayout.Button("Delete Selected Layer"))
        {
            RecordChange("Delete Female Layer");
            targetAuthoring.RemoveHierarchyEntry(selection.ParentGroup, selection.IndexInParent);
            EnsureSomeFemaleSelection();
            MarkDirty();
        }
    }

    private void DrawDetailsPanel()
    {
        EditorGUILayout.BeginVertical();

        if (editTarget == GridEditTarget.Male)
        {
            DrawMaleGridEditor();
            EditorGUILayout.EndVertical();
            return;
        }

        if (!selection.IsValid)
        {
            EditorGUILayout.HelpBox("Select a group or layer from the left panel.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        if (selection.Kind == SelectionKind.Group)
        {
            DrawGroupEditor(selection.Group);
            EditorGUILayout.EndVertical();
            return;
        }

        DrawLayerEditor(selection.Layer);
        EditorGUILayout.EndVertical();
    }

    private void DrawGroupEditor(FemaleGridGroup group)
    {
        EditorGUILayout.LabelField("Selected Group", EditorStyles.boldLabel);

        bool isRootGroup = group == targetAuthoring.FemaleGridRootGroup;

        if (isRootGroup)
        {
            EditorGUILayout.HelpBox(
                "First Layer Height sets how high above the male grid the first layer sits. " +
                "Group Spacing controls the vertical gap between each subsequent layer in this group.",
                MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "A group's spacing controls the vertical gap between each child entry in that group. Nested groups apply their own spacing inside the parent flow.",
                MessageType.None);
        }

        string updatedName = EditorGUILayout.TextField("Group Name", group.Name);
        if (updatedName != group.Name)
        {
            RecordChange("Rename Female Group");
            group.Name = updatedName;
            MarkDirty();
        }

        if (isRootGroup)
        {
            float updatedStartHeight = EditorGUILayout.FloatField("First Layer Height", group.StartHeight);
            if (!Mathf.Approximately(updatedStartHeight, group.StartHeight))
            {
                RecordChange("Change Group First Layer Height");
                group.StartHeight = updatedStartHeight;
                MarkDirty();
            }
        }

        float updatedSpacing = EditorGUILayout.FloatField("Group Spacing", group.Spacing);
        if (!Mathf.Approximately(updatedSpacing, group.Spacing))
        {
            RecordChange("Change Group Spacing");
            group.Spacing = updatedSpacing;
            MarkDirty();
        }

        EditorGUILayout.LabelField("Child Count", group.Children.Count.ToString());
    }

    private void DrawLayerEditor(FemaleGridLayer layer)
    {
        EditorGUILayout.LabelField("Selected Layer", EditorStyles.boldLabel);
        string updatedName = EditorGUILayout.TextField("Layer Name", layer.Name);
        if (updatedName != layer.Name)
        {
            RecordChange("Rename Female Layer");
            layer.Name = updatedName;
            MarkDirty();
        }

        EditorGUILayout.HelpBox(
            "This layer's height is computed from the spacing rules of its parent groups. Reorder the hierarchy or change group spacing to move it vertically.",
            MessageType.None);
        EditorGUILayout.LabelField("Computed Height Above Male Grid", layer.LocalHeight.ToString("0.##"));

        bool useMaleGridSize = EditorGUILayout.Toggle("Use Male Grid Size", layer.UseMaleGridSize);
        if (useMaleGridSize != layer.UseMaleGridSize)
        {
            RecordChange("Toggle Female Layer Size Mode");
            layer.UseMaleGridSize = useMaleGridSize;
            layer.Validate(targetAuthoring.MaleGridSizeInCells);
            MarkDirty();
        }

        if (!layer.UseMaleGridSize)
        {
            Vector2Int updatedGridSize = EditorGUILayout.Vector2IntField("Grid Size In Cells", layer.GridSizeInCells);
            if (updatedGridSize != layer.GridSizeInCells)
            {
                RecordChange("Resize Female Layer");
                layer.GridSizeInCells = updatedGridSize;
                layer.Validate(targetAuthoring.MaleGridSizeInCells);
                MarkDirty();
            }
        }
        else
        {
            EditorGUILayout.LabelField("Grid Size In Cells", $"{layer.GridSizeInCells.x} x {layer.GridSizeInCells.y}");
        }

        bool openAbove = EditorGUILayout.Toggle("Open Above", layer.OpenAbove);
        if (openAbove != layer.OpenAbove)
        {
            RecordChange("Toggle Layer Clearance Mode");
            layer.OpenAbove = openAbove;
            MarkDirty();
        }

        using (new EditorGUI.DisabledScope(layer.OpenAbove))
        {
            float updatedClearance = EditorGUILayout.FloatField("Clearance Above", layer.ClearanceAbove);
            if (!Mathf.Approximately(updatedClearance, layer.ClearanceAbove))
            {
                RecordChange("Change Layer Clearance");
                layer.ClearanceAbove = updatedClearance;
                MarkDirty();
            }
        }

        EditorGUILayout.Space();
        DrawFemaleMatrixToolbar(layer);
        DrawPaintMatrix(
            layer.GridSizeInCells,
            layer.GetCell,
            layer.SetCell,
            false,
            "Pink and orange cells are active female tiles, matching the gizmo view. Gray cells are blocked. Drag with the mouse to paint multiple cells.");
    }

    private void DrawMaleGridEditor()
    {
        EditorGUILayout.LabelField("Male Grid", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "The male grid is the underside snap footprint of the object. Disable cells here to author L-shapes, couches, and other irregular bottom footprints.",
            MessageType.None);
        EditorGUILayout.LabelField("Grid Size In Cells", $"{targetAuthoring.MaleGridSizeInCells.x} x {targetAuthoring.MaleGridSizeInCells.y}");
        EditorGUILayout.LabelField("Enabled Male Tiles", targetAuthoring.GetEnabledMaleCellCount().ToString());

        EditorGUILayout.Space();
        DrawMaleMatrixToolbar();
        DrawPaintMatrix(
            targetAuthoring.MaleGridSizeInCells,
            targetAuthoring.GetMaleCell,
            targetAuthoring.SetMaleCell,
            true,
            "Blue and purple cells are active male tiles on the underside of the object, matching the gizmo view. Drag with the mouse to paint multiple cells.");
    }

    private void DrawFemaleMatrixToolbar(FemaleGridLayer layer)
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Fill All"))
        {
            RecordChange("Fill Female Layer");
            layer.Fill(true);
            MarkDirty();
        }

        if (GUILayout.Button("Clear All"))
        {
            RecordChange("Clear Female Layer");
            layer.Fill(false);
            MarkDirty();
        }

        if (GUILayout.Button("Match Male Grid"))
        {
            RecordChange("Match Female Layer To Male Grid");
            layer.UseMaleGridSize = true;
            layer.Validate(targetAuthoring.MaleGridSizeInCells);
            MarkDirty();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawMaleMatrixToolbar()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Fill All"))
        {
            RecordChange("Fill Male Grid");
            targetAuthoring.FillMaleGrid(true);
            MarkDirty();
        }

        if (GUILayout.Button("Clear All"))
        {
            RecordChange("Clear Male Grid");
            targetAuthoring.FillMaleGrid(false);
            MarkDirty();
        }

        if (GUILayout.Button("Reset From Collider Size"))
        {
            RecordChange("Reset Male Grid From Colliders");
            targetAuthoring.RecalculateMaleGridFromColliders();
            MarkDirty();
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawPaintMatrix(
        Vector2Int gridSize,
        Func<int, int, bool> getCell,
        Action<int, int, bool> setCell,
        bool isMaleGrid,
        string helpText)
    {
        matrixScrollPosition = EditorGUILayout.BeginScrollView(matrixScrollPosition);

        float gridWidth = gridSize.x * CellButtonSize;
        float gridHeight = gridSize.y * CellButtonSize;
        Rect gridRect = GUILayoutUtility.GetRect(gridWidth, gridHeight, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
        DrawGridCells(gridRect, gridSize, getCell, isMaleGrid);
        HandlePaintInput(gridRect, gridSize, getCell, setCell);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.HelpBox(helpText, MessageType.None);
    }

    private void DrawGridCells(Rect gridRect, Vector2Int gridSize, Func<int, int, bool> getCell, bool isMaleGrid)
    {
        EditorGUI.DrawRect(gridRect, InactiveCellColor * 0.85f);

        for (int z = 0; z < gridSize.y; z++)
        {
            for (int x = 0; x < gridSize.x; x++)
            {
                Rect cellRect = GetCellRect(gridRect, gridSize, x, z);
                EditorGUI.DrawRect(cellRect, getCell(x, z) ? GetGridCellColor(x, z, isMaleGrid) : InactiveCellColor);
                Handles.color = GridLineColor;
                Handles.DrawAAPolyLine(1.25f,
                    new Vector3(cellRect.xMin, cellRect.yMin),
                    new Vector3(cellRect.xMax, cellRect.yMin),
                    new Vector3(cellRect.xMax, cellRect.yMax),
                    new Vector3(cellRect.xMin, cellRect.yMax),
                    new Vector3(cellRect.xMin, cellRect.yMin));
            }
        }
    }

    private Color GetGridCellColor(int xIndex, int zIndex, bool isMaleGrid)
    {
        int subtilesPerFullTile = Mathf.Max(1, Mathf.RoundToInt(FullTileWorldSize / targetAuthoring.FemaleTileSize));
        int tileX = xIndex / subtilesPerFullTile;
        int tileZ = zIndex / subtilesPerFullTile;
        int subtileX = xIndex % subtilesPerFullTile;
        int subtileZ = zIndex % subtilesPerFullTile;

        bool usePrimaryPalette = ((tileX + tileZ) & 1) == 0;
        bool useLightVariant = ((subtileX + subtileZ) & 1) == 0;

        if (isMaleGrid)
        {
            return usePrimaryPalette
                ? (useLightVariant ? MalePrimaryLight : MalePrimaryDark)
                : (useLightVariant ? MaleSecondaryLight : MaleSecondaryDark);
        }

        return usePrimaryPalette
            ? (useLightVariant ? FemalePrimaryLight : FemalePrimaryDark)
            : (useLightVariant ? FemaleSecondaryLight : FemaleSecondaryDark);
    }

    private void HandlePaintInput(Rect gridRect, Vector2Int gridSize, Func<int, int, bool> getCell, Action<int, int, bool> setCell)
    {
        Event currentEvent = Event.current;
        int controlId = GUIUtility.GetControlID(FocusType.Passive);

        switch (currentEvent.GetTypeForControl(controlId))
        {
            case EventType.MouseDown:
                if (currentEvent.button != 0 || !gridRect.Contains(currentEvent.mousePosition))
                {
                    break;
                }

                if (!TryGetCellAtPosition(gridRect, gridSize, currentEvent.mousePosition, out Vector2Int pressedCell))
                {
                    break;
                }

                bool newValue = !getCell(pressedCell.x, pressedCell.y);
                RecordChange("Paint Grid Cells");
                setCell(pressedCell.x, pressedCell.y, newValue);
                dragPaintValue = newValue;
                isDragPainting = true;
                lastPaintedCell = pressedCell;
                GUIUtility.hotControl = controlId;
                currentEvent.Use();
                MarkDirty();
                break;

            case EventType.MouseDrag:
                if (!isDragPainting || GUIUtility.hotControl != controlId)
                {
                    break;
                }

                if (!TryGetCellAtPosition(gridRect, gridSize, currentEvent.mousePosition, out Vector2Int draggedCell))
                {
                    currentEvent.Use();
                    break;
                }

                if (draggedCell == lastPaintedCell)
                {
                    currentEvent.Use();
                    break;
                }

                setCell(draggedCell.x, draggedCell.y, dragPaintValue);
                lastPaintedCell = draggedCell;
                currentEvent.Use();
                MarkDirty();
                break;

            case EventType.MouseUp:
                if (!isDragPainting || GUIUtility.hotControl != controlId)
                {
                    break;
                }

                StopDragPainting();
                GUIUtility.hotControl = 0;
                currentEvent.Use();
                break;
        }
    }

    private static Rect GetCellRect(Rect gridRect, Vector2Int gridSize, int xIndex, int zIndex)
    {
        float x = gridRect.xMin + (xIndex * CellButtonSize);
        float y = gridRect.yMin + ((gridSize.y - 1 - zIndex) * CellButtonSize);
        return new Rect(x, y, CellButtonSize - 1f, CellButtonSize - 1f);
    }

    private static bool TryGetCellAtPosition(Rect gridRect, Vector2Int gridSize, Vector2 mousePosition, out Vector2Int cell)
    {
        cell = default;

        if (!gridRect.Contains(mousePosition))
        {
            return false;
        }

        float localX = mousePosition.x - gridRect.xMin;
        float localY = mousePosition.y - gridRect.yMin;
        int xIndex = Mathf.FloorToInt(localX / CellButtonSize);
        int zIndexFromTop = Mathf.FloorToInt(localY / CellButtonSize);
        int zIndex = gridSize.y - 1 - zIndexFromTop;

        if (xIndex < 0 || xIndex >= gridSize.x || zIndex < 0 || zIndex >= gridSize.y)
        {
            return false;
        }

        cell = new Vector2Int(xIndex, zIndex);
        return true;
    }

    private void StopDragPainting()
    {
        isDragPainting = false;
        lastPaintedCell = new Vector2Int(int.MinValue, int.MinValue);
    }

    private void RecordChange(string actionName)
    {
        Undo.RecordObject(targetAuthoring, actionName);
    }

    private void MarkDirty()
    {
        targetAuthoring.EnsureValidData();
        EditorUtility.SetDirty(targetAuthoring);
        Repaint();
    }

    private void MoveSelection(FemaleGridGroup parentGroup, int currentIndex, int targetIndex)
    {
        RecordChange("Reorder Female Entry");
        targetAuthoring.MoveHierarchyEntry(parentGroup, currentIndex, parentGroup, targetIndex);
        EnsureSelectionStillValid();
        MarkDirty();
    }

    private void MoveSelectionIntoGroup(FemaleGridGroup sourceParent, int sourceIndex, FemaleGridGroup targetGroup)
    {
        RecordChange("Nest Female Entry");
        targetAuthoring.MoveHierarchyEntry(sourceParent, sourceIndex, targetGroup, targetGroup.Children.Count);
        EnsureSelectionStillValid();
        MarkDirty();
    }

    private bool IsEntrySelected(FemaleGridGroup group, FemaleGridGroup parentGroup, int indexInParent, bool isGroup, FemaleGridLayer layer)
    {
        if (!selection.IsValid)
        {
            return false;
        }

        if (isGroup)
        {
            return selection.Kind == SelectionKind.Group && selection.Group == group && selection.ParentGroup == parentGroup && selection.IndexInParent == indexInParent;
        }

        return selection.Kind == SelectionKind.Layer && selection.Layer == layer && selection.ParentGroup == parentGroup && selection.IndexInParent == indexInParent;
    }

    private void SelectHierarchyEntry(FemaleGridGroup group, FemaleGridGroup parentGroup, int indexInParent, bool isGroup, FemaleGridLayer layer)
    {
        StopDragPainting();
        editTarget = GridEditTarget.Female;
        selection = new HierarchySelection
        {
            Kind = isGroup ? SelectionKind.Group : SelectionKind.Layer,
            ParentGroup = parentGroup,
            IndexInParent = indexInParent,
            Group = group,
            Layer = layer
        };
    }

    private void SelectLayerInGroup(FemaleGridGroup parentGroup, FemaleGridLayer layer)
    {
        if (parentGroup == null || layer == null)
        {
            return;
        }

        for (int i = 0; i < parentGroup.Children.Count; i++)
        {
            FemaleGridHierarchyEntry child = parentGroup.Children[i];
            if (child.IsLayer && child.Layer == layer)
            {
                SelectHierarchyEntry(null, parentGroup, i, false, layer);
                return;
            }
        }
    }

    private void SelectGroupBySearch(FemaleGridGroup group)
    {
        if (group == null)
        {
            return;
        }

        if (TryFindGroup(targetAuthoring.FemaleGridRootGroup, null, group, out FemaleGridGroup parentGroup, out int indexInParent))
        {
            SelectHierarchyEntry(group, parentGroup, indexInParent, true, null);
        }
    }

    private bool TryFindGroup(FemaleGridGroup currentGroup, FemaleGridGroup parentGroup, FemaleGridGroup targetGroup, out FemaleGridGroup foundParent, out int foundIndex)
    {
        if (currentGroup == targetGroup)
        {
            foundParent = parentGroup;
            foundIndex = -1;
            return true;
        }

        for (int i = 0; i < currentGroup.Children.Count; i++)
        {
            FemaleGridHierarchyEntry child = currentGroup.Children[i];
            if (!child.IsGroup)
            {
                continue;
            }

            if (child.Group == targetGroup)
            {
                foundParent = currentGroup;
                foundIndex = i;
                return true;
            }

            if (TryFindGroup(child.Group, currentGroup, targetGroup, out foundParent, out foundIndex))
            {
                return true;
            }
        }

        foundParent = null;
        foundIndex = -1;
        return false;
    }

    private void EnsureSelectionStillValid()
    {
        if (!selection.IsValid)
        {
            return;
        }

        if (selection.Kind == SelectionKind.Group)
        {
            if (selection.Group == null)
            {
                selection = default;
                return;
            }

            if (selection.Group == targetAuthoring.FemaleGridRootGroup)
            {
                selection.ParentGroup = null;
                selection.IndexInParent = -1;
                return;
            }

            if (!TryFindGroup(targetAuthoring.FemaleGridRootGroup, null, selection.Group, out FemaleGridGroup parentGroup, out int indexInParent))
            {
                selection = default;
                return;
            }

            selection.ParentGroup = parentGroup;
            selection.IndexInParent = indexInParent;
            return;
        }

        if (!TryFindLayer(targetAuthoring.FemaleGridRootGroup, selection.Layer, out FemaleGridGroup layerParent, out int layerIndex))
        {
            selection = default;
            return;
        }

        selection.ParentGroup = layerParent;
        selection.IndexInParent = layerIndex;
    }

    private bool TryFindLayer(FemaleGridGroup currentGroup, FemaleGridLayer targetLayer, out FemaleGridGroup parentGroup, out int indexInParent)
    {
        for (int i = 0; i < currentGroup.Children.Count; i++)
        {
            FemaleGridHierarchyEntry child = currentGroup.Children[i];
            if (child.IsLayer && child.Layer == targetLayer)
            {
                parentGroup = currentGroup;
                indexInParent = i;
                return true;
            }

            if (child.IsGroup && TryFindLayer(child.Group, targetLayer, out parentGroup, out indexInParent))
            {
                return true;
            }
        }

        parentGroup = null;
        indexInParent = -1;
        return false;
    }

    private void EnsureSomeFemaleSelection()
    {
        if (selection.IsValid)
        {
            return;
        }

        if (TryFindFirstLayer(targetAuthoring.FemaleGridRootGroup, out FemaleGridGroup parentGroup, out int indexInParent, out FemaleGridLayer layer))
        {
            selection = new HierarchySelection
            {
                Kind = SelectionKind.Layer,
                ParentGroup = parentGroup,
                IndexInParent = indexInParent,
                Layer = layer
            };
            return;
        }

        selection = new HierarchySelection
        {
            Kind = SelectionKind.Group,
            Group = targetAuthoring.FemaleGridRootGroup,
            ParentGroup = null,
            IndexInParent = -1
        };
    }

    private bool TryFindFirstLayer(FemaleGridGroup currentGroup, out FemaleGridGroup parentGroup, out int indexInParent, out FemaleGridLayer layer)
    {
        for (int i = 0; i < currentGroup.Children.Count; i++)
        {
            FemaleGridHierarchyEntry child = currentGroup.Children[i];
            if (child.IsLayer)
            {
                parentGroup = currentGroup;
                indexInParent = i;
                layer = child.Layer;
                return true;
            }

            if (child.IsGroup && TryFindFirstLayer(child.Group, out parentGroup, out indexInParent, out layer))
            {
                return true;
            }
        }

        parentGroup = null;
        indexInParent = -1;
        layer = null;
        return false;
    }
}
