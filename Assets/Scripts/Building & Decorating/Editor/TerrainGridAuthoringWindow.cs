using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Plain-English purpose:
/// Dedicated editor window for painting TerrainGridAuthoring cells.
/// Left panel: stats + controls (snap-to-full-tile toggle, fill/clear buttons).
/// Right panel: drag-to-paint subtile grid. Thicker rules at every full-tile boundary.
///
/// Cell colors use the same female palette as PlaceableGridAuthoringWindow so the
/// visual language stays consistent.
/// </summary>
public class TerrainGridAuthoringWindow : EditorWindow
{
    // ── Layout constants ──────────────────────────────────────────────────────────
    private const float CellButtonSize = 26f;
    private const float FullTileWorldSize = 1f;
    private const float LeftPanelWidth = 220f;

    // ── Cell colours (identical palette to PlaceableGridAuthoringWindow female) ──
    private static readonly Color FemalePrimaryLight = new Color(0.93f, 0.36f, 0.72f, 0.95f);
    private static readonly Color FemalePrimaryDark = new Color(0.99f, 0.0f, 0.45f, 0.95f);
    private static readonly Color FemaleSecondaryLight = new Color(1f, 0.79f, 0.46f, 0.95f);
    private static readonly Color FemaleSecondaryDark = new Color(1f, 0.63f, 0.0f, 0.95f);
    private static readonly Color InactiveCellColor = new Color(0.22f, 0.22f, 0.22f, 1f);
    private static readonly Color GridLineColor = new Color(0f, 0f, 0f, 0.35f);
    private static readonly Color FullTileBorderColor = new Color(0f, 0f, 0f, 0.7f);

    // ── State ─────────────────────────────────────────────────────────────────────
    private TerrainGridAuthoring targetAuthoring;
    private bool snapToFullTile = false;
    private Vector2 scrollPosition;

    // Drag-paint state
    private bool isDragPainting;
    private bool dragPaintValue;
    private Vector2Int lastPaintedCell = new Vector2Int(int.MinValue, int.MinValue);

    // ── Public entry point ────────────────────────────────────────────────────────

    [MenuItem("Window/Mini Toy Game/Terrain Grid Authoring")]
    public static void Open()
    {
        GetWindow<TerrainGridAuthoringWindow>("Terrain Grid");
    }

    public static void Open(TerrainGridAuthoring authoring)
    {
        var window = GetWindow<TerrainGridAuthoringWindow>("Terrain Grid");
        window.SetTarget(authoring);
    }

    // ── Unity messages ────────────────────────────────────────────────────────────

    private void OnSelectionChange()
    {
        if (Selection.activeGameObject == null) return;
        var authoring = Selection.activeGameObject.GetComponent<TerrainGridAuthoring>();
        if (authoring != null)
        {
            SetTarget(authoring);
        }
    }

    private void OnGUI()
    {
        DrawTargetHeader();

        if (targetAuthoring == null)
        {
            EditorGUILayout.HelpBox("Select a GameObject with a TerrainGridAuthoring component, or click below.", MessageType.Info);
            if (GUILayout.Button("Use Selected Object"))
            {
                TryPickFromSelection();
            }
            return;
        }

        EditorGUILayout.BeginHorizontal();
        DrawLeftPanel();
        DrawRightPanel();
        EditorGUILayout.EndHorizontal();
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private void SetTarget(TerrainGridAuthoring authoring)
    {
        if (targetAuthoring == authoring) return;
        StopDragPainting();
        targetAuthoring = authoring;
        Repaint();
    }

    private void TryPickFromSelection()
    {
        if (Selection.activeGameObject != null)
        {
            var authoring = Selection.activeGameObject.GetComponent<TerrainGridAuthoring>();
            if (authoring != null) SetTarget(authoring);
        }
    }

    // ── Header ────────────────────────────────────────────────────────────────────

    private void DrawTargetHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Use Selected", EditorStyles.toolbarButton, GUILayout.Width(90f)))
        {
            TryPickFromSelection();
        }

        string label = targetAuthoring != null ? targetAuthoring.name : "(none)";
        GUILayout.Label(label, EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        EditorGUILayout.EndHorizontal();
    }

    // ── Left panel ────────────────────────────────────────────────────────────────

    private void DrawLeftPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(LeftPanelWidth));

        EditorGUILayout.LabelField("Grid Summary", EditorStyles.boldLabel);
        Vector2Int cellSize = targetAuthoring.GridSizeInCells;
        EditorGUILayout.LabelField("Subtile cells", $"{cellSize.x} \u00d7 {cellSize.y}");
        EditorGUILayout.LabelField("Enabled cells", $"{targetAuthoring.GetEnabledCellCount()} / {cellSize.x * cellSize.y}");

        Vector2Int fullSize = targetAuthoring.FullTileGridSize;
        EditorGUILayout.LabelField("Full tiles", $"{fullSize.x} \u00d7 {fullSize.y}");
        EditorGUILayout.LabelField("Walkable tiles", $"{targetAuthoring.GetWalkableFullTileCount()} / {fullSize.x * fullSize.y}");

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Paint Options", EditorStyles.boldLabel);
        snapToFullTile = EditorGUILayout.Toggle(new GUIContent("Snap to full tile", "Paint entire 1\u00d71 full-tile blocks rather than individual subtile cells."), snapToFullTile);

        EditorGUILayout.Space(4);
        if (GUILayout.Button("Fill All"))
        {
            RecordChange("Fill Terrain Grid");
            targetAuthoring.FillAll(true);
            MarkDirty();
        }
        if (GUILayout.Button("Clear All"))
        {
            RecordChange("Clear Terrain Grid");
            targetAuthoring.FillAll(false);
            MarkDirty();
        }

        EditorGUILayout.Space(4);
        if (GUILayout.Button("Recalculate from Colliders"))
        {
            RecordChange("Recalculate Terrain Grid");
            targetAuthoring.RecalculateFromColliders();
            MarkDirty();
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "Pink/orange cells = enabled subtiles.\nLeft-click or drag to toggle cells.\nGreen overlay shows walkable 1\u00d71 tiles in the scene.",
            MessageType.None);

        EditorGUILayout.EndVertical();
    }

    // ── Right panel (scrollable paint grid) ───────────────────────────────────────

    private void DrawRightPanel()
    {
        EditorGUILayout.BeginVertical();
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        Vector2Int gridSize = targetAuthoring.GridSizeInCells;
        float pixelW = gridSize.x * CellButtonSize;
        float pixelH = gridSize.y * CellButtonSize;

        Rect gridRect = GUILayoutUtility.GetRect(pixelW, pixelH, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));

        if (Event.current.type != EventType.Layout)
        {
            DrawGridCells(gridRect, gridSize);
            DrawFullTileLines(gridRect, gridSize);
            HandlePaintInput(gridRect, gridSize);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // ── Cell drawing ──────────────────────────────────────────────────────────────

    private void DrawGridCells(Rect gridRect, Vector2Int gridSize)
    {
        int s = targetAuthoring.SubtilesPerFullTile;

        for (int z = 0; z < gridSize.y; z++)
        {
            for (int x = 0; x < gridSize.x; x++)
            {
                Rect cellRect = GetCellRect(gridRect, gridSize, x, z);
                bool enabled = targetAuthoring.GetCell(x, z);

                Color fillColor = enabled ? GetCellColor(x, z, s) : InactiveCellColor;
                EditorGUI.DrawRect(cellRect, fillColor);

                // Thin grid line around every subtile cell.
                DrawCellBorder(cellRect, GridLineColor);
            }
        }
    }

    private void DrawFullTileLines(Rect gridRect, Vector2Int gridSize)
    {
        int s = targetAuthoring.SubtilesPerFullTile;
        if (s <= 1) return;

        // Draw thicker horizontal and vertical separators at every full-tile boundary.
        for (int x = s; x < gridSize.x; x += s)
        {
            float px = gridRect.xMin + x * CellButtonSize;
            Rect lineRect = new Rect(px - 1f, gridRect.yMin, 2f, gridSize.y * CellButtonSize);
            EditorGUI.DrawRect(lineRect, FullTileBorderColor);
        }
        for (int z = s; z < gridSize.y; z += s)
        {
            float py = gridRect.yMin + (gridSize.y - z) * CellButtonSize;
            Rect lineRect = new Rect(gridRect.xMin, py - 1f, gridSize.x * CellButtonSize, 2f);
            EditorGUI.DrawRect(lineRect, FullTileBorderColor);
        }
    }

    private static void DrawCellBorder(Rect r, Color color)
    {
        EditorGUI.DrawRect(new Rect(r.xMin, r.yMin, r.width, 1f), color);
        EditorGUI.DrawRect(new Rect(r.xMin, r.yMax - 1f, r.width, 1f), color);
        EditorGUI.DrawRect(new Rect(r.xMin, r.yMin, 1f, r.height), color);
        EditorGUI.DrawRect(new Rect(r.xMax - 1f, r.yMin, 1f, r.height), color);
    }

    private static Color GetCellColor(int x, int z, int subtilesPerFullTile)
    {
        int fullTileX = x / subtilesPerFullTile;
        int fullTileZ = z / subtilesPerFullTile;
        bool isPrimary = ((fullTileX + fullTileZ) & 1) == 0;

        int subtileX = x % subtilesPerFullTile;
        int subtileZ = z % subtilesPerFullTile;
        bool isLight = ((subtileX + subtileZ) & 1) == 0;

        if (isPrimary)
        {
            return isLight ? FemalePrimaryLight : FemalePrimaryDark;
        }
        else
        {
            return isLight ? FemaleSecondaryLight : FemaleSecondaryDark;
        }
    }

    // ── Paint input ───────────────────────────────────────────────────────────────

    private void HandlePaintInput(Rect gridRect, Vector2Int gridSize)
    {
        Event e = Event.current;
        int controlId = GUIUtility.GetControlID(FocusType.Passive);

        switch (e.GetTypeForControl(controlId))
        {
            case EventType.MouseDown:
                if (e.button == 0 && gridRect.Contains(e.mousePosition))
                {
                    if (TryGetCellAtPosition(e.mousePosition, gridRect, gridSize, out Vector2Int cell))
                    {
                        GUIUtility.hotControl = controlId;
                        RecordChange("Paint Terrain Grid");
                        bool newValue = !targetAuthoring.GetCell(cell.x, cell.y);
                        isDragPainting = true;
                        dragPaintValue = newValue;
                        lastPaintedCell = new Vector2Int(int.MinValue, int.MinValue);
                        ApplyPaint(cell);
                        MarkDirty();
                        e.Use();
                    }
                }
                break;

            case EventType.MouseDrag:
                if (isDragPainting && e.button == 0)
                {
                    if (TryGetCellAtPosition(e.mousePosition, gridRect, gridSize, out Vector2Int dragCell))
                    {
                        if (dragCell != lastPaintedCell)
                        {
                            ApplyPaint(dragCell);
                            MarkDirty();
                        }
                    }
                    e.Use();
                }
                break;

            case EventType.MouseUp:
                if (isDragPainting && e.button == 0)
                {
                    GUIUtility.hotControl = 0;
                    StopDragPainting();
                    e.Use();
                }
                break;
        }
    }

    private void ApplyPaint(Vector2Int cell)
    {
        if (snapToFullTile)
        {
            PaintFullTile(cell);
        }
        else
        {
            targetAuthoring.SetCell(cell.x, cell.y, dragPaintValue);
            lastPaintedCell = cell;
        }
    }

    private void PaintFullTile(Vector2Int cell)
    {
        int s = targetAuthoring.SubtilesPerFullTile;
        int ftX = (cell.x / s) * s;
        int ftZ = (cell.y / s) * s;

        // Mark the full tile as the last painted unit to prevent re-painting on drag within it.
        Vector2Int fullTileKey = new Vector2Int(ftX, ftZ);
        if (fullTileKey == lastPaintedCell) return;
        lastPaintedCell = fullTileKey;

        for (int sz = 0; sz < s; sz++)
        {
            for (int sx = 0; sx < s; sx++)
            {
                targetAuthoring.SetCell(ftX + sx, ftZ + sz, dragPaintValue);
            }
        }
    }

    // ── Geometry ──────────────────────────────────────────────────────────────────

    private static Rect GetCellRect(Rect gridRect, Vector2Int gridSize, int x, int z)
    {
        float px = gridRect.xMin + x * CellButtonSize;
        // Z is rendered bottom-up so z=0 is at the bottom of the grid.
        float py = gridRect.yMin + (gridSize.y - 1 - z) * CellButtonSize;
        return new Rect(px, py, CellButtonSize, CellButtonSize);
    }

    private static bool TryGetCellAtPosition(Vector2 mousePos, Rect gridRect, Vector2Int gridSize, out Vector2Int cell)
    {
        cell = default;
        if (!gridRect.Contains(mousePos)) return false;

        int xIndex = Mathf.FloorToInt((mousePos.x - gridRect.xMin) / CellButtonSize);
        int zIndex = gridSize.y - 1 - Mathf.FloorToInt((mousePos.y - gridRect.yMin) / CellButtonSize);

        if (xIndex < 0 || xIndex >= gridSize.x || zIndex < 0 || zIndex >= gridSize.y) return false;

        cell = new Vector2Int(xIndex, zIndex);
        return true;
    }

    // ── Undo / dirty ─────────────────────────────────────────────────────────────

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
}
