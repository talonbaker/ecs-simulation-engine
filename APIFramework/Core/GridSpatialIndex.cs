using System;
using System.Collections.Generic;
using APIFramework.Config;

namespace APIFramework.Core;

/// <summary>
/// Cell-based 2-D spatial index for O(cells touched + entities in cells) range queries.
///
/// COORDINATE SYSTEM
/// ------------------
/// All positions are (int x, int y) in tile units. The 2-D plane corresponds to
/// PositionComponent.(X, Z) converted to integer tile coordinates.
///
/// CELL LAYOUT
/// ------------
/// The world is divided into a grid of square cells, each <see cref="_cellSize"/> tiles wide.
/// Cells are indexed by a row-major key: key = cellY * cellCountX + cellX.
/// Cells outside world bounds clamp to the nearest border cell.
///
/// DETERMINISM
/// ------------
/// QueryRadius visits cells in row-major order (ascending cellY, then ascending cellX).
/// Entities within a cell are maintained in insertion order (List&lt;Entity&gt;).
/// QueryNearest sorts by (distanceSq, Entity.Id) — ties are broken by entity id ascending.
/// </summary>
public sealed class GridSpatialIndex : ISpatialIndex
{
    private readonly int _cellSize;
    private readonly int _worldWidth;
    private readonly int _worldHeight;
    private readonly int _cellCountX;
    private readonly int _cellCountY;

    // cell key → ordered list of entities in that cell (insertion order = deterministic)
    private readonly Dictionary<int, List<Entity>> _cells = new();

    // entity → (tileX, tileY) — kept for exact-distance filtering and QueryNearest
    private readonly Dictionary<Entity, (int x, int y)> _positions = new();

    /// <summary>
    /// Constructs a spatial index sized for a world of the given tile dimensions,
    /// using square cells of <paramref name="cellSizeTiles"/> tiles per side.
    /// The grid is sized to fully cover the world (rounded up).
    /// </summary>
    /// <param name="cellSizeTiles">
    /// Width/height of each cell in tile units. Values <c>&lt;= 0</c> fall back to a default of 4.
    /// </param>
    /// <param name="worldWidth">World width in tiles.</param>
    /// <param name="worldHeight">World height in tiles.</param>
    public GridSpatialIndex(int cellSizeTiles, int worldWidth, int worldHeight)
    {
        _cellSize   = cellSizeTiles > 0 ? cellSizeTiles : 4;
        _worldWidth = worldWidth;
        _worldHeight = worldHeight;
        _cellCountX = (_worldWidth  + _cellSize - 1) / _cellSize;
        _cellCountY = (_worldHeight + _cellSize - 1) / _cellSize;
    }

    /// <summary>Constructs from the Spatial section of SimConfig.</summary>
    public GridSpatialIndex(SpatialConfig cfg)
        : this(cfg.CellSizeTiles, cfg.WorldSize.Width, cfg.WorldSize.Height) { }

    // -- Key helpers -----------------------------------------------------------

    private int CellKey(int cellX, int cellY) => cellY * _cellCountX + cellX;

    private (int cx, int cy) TileToCell(int tileX, int tileY)
    {
        int cx = Math.Clamp(tileX / _cellSize, 0, _cellCountX - 1);
        int cy = Math.Clamp(tileY / _cellSize, 0, _cellCountY - 1);
        return (cx, cy);
    }

    // -- ISpatialIndex ---------------------------------------------------------

    /// <inheritdoc/>
    public void Register(Entity entity, int x, int y)
    {
        var (cx, cy) = TileToCell(x, y);
        int key = CellKey(cx, cy);
        GetOrCreateCell(key).Add(entity);
        _positions[entity] = (x, y);
    }

    /// <inheritdoc/>
    public void Unregister(Entity entity)
    {
        if (!_positions.TryGetValue(entity, out var pos)) return;

        var (cx, cy) = TileToCell(pos.x, pos.y);
        int key = CellKey(cx, cy);
        if (_cells.TryGetValue(key, out var list))
            list.Remove(entity);

        _positions.Remove(entity);
    }

    /// <inheritdoc/>
    public void Update(Entity entity, int newX, int newY)
    {
        if (!_positions.TryGetValue(entity, out var oldPos))
        {
            Register(entity, newX, newY);
            return;
        }

        var (oldCx, oldCy) = TileToCell(oldPos.x, oldPos.y);
        var (newCx, newCy) = TileToCell(newX, newY);
        int oldKey = CellKey(oldCx, oldCy);
        int newKey = CellKey(newCx, newCy);

        if (oldKey != newKey)
        {
            if (_cells.TryGetValue(oldKey, out var oldList))
                oldList.Remove(entity);

            GetOrCreateCell(newKey).Add(entity);
        }

        _positions[entity] = (newX, newY);
    }

    /// <inheritdoc/>
    public IReadOnlyList<Entity> QueryRadius(int x, int y, int radius)
    {
        if (radius <= 0) return Array.Empty<Entity>();

        int minCellX = Math.Max(0, (x - radius) / _cellSize);
        int maxCellX = Math.Min(_cellCountX - 1, (x + radius) / _cellSize);
        int minCellY = Math.Max(0, (y - radius) / _cellSize);
        int maxCellY = Math.Min(_cellCountY - 1, (y + radius) / _cellSize);

        long radiusSq = (long)radius * radius;
        var result = new List<Entity>();

        // Row-major iteration for determinism
        for (int cy = minCellY; cy <= maxCellY; cy++)
        {
            for (int cx = minCellX; cx <= maxCellX; cx++)
            {
                int key = CellKey(cx, cy);
                if (!_cells.TryGetValue(key, out var cellEntities)) continue;

                foreach (var entity in cellEntities)
                {
                    if (!_positions.TryGetValue(entity, out var ep)) continue;
                    long dx = ep.x - x, dy = ep.y - y;
                    if (dx * dx + dy * dy <= radiusSq)
                        result.Add(entity);
                }
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public IReadOnlyList<Entity> QueryNearest(int x, int y, int maxCount)
    {
        if (maxCount <= 0 || _positions.Count == 0) return Array.Empty<Entity>();

        // Collect all candidates with their squared distances
        var candidates = new List<(long distSq, Entity entity)>(_positions.Count);
        foreach (var kvp in _positions)
        {
            long dx = kvp.Value.x - x, dy = kvp.Value.y - y;
            candidates.Add((dx * dx + dy * dy, kvp.Key));
        }

        // Sort by distance ascending, ties broken by Entity.Id ascending (deterministic)
        candidates.Sort((a, b) =>
        {
            int cmp = a.distSq.CompareTo(b.distSq);
            return cmp != 0 ? cmp : a.entity.Id.CompareTo(b.entity.Id);
        });

        int take = Math.Min(maxCount, candidates.Count);
        var result = new List<Entity>(take);
        for (int i = 0; i < take; i++)
            result.Add(candidates[i].entity);

        return result;
    }

    // -- Helpers ---------------------------------------------------------------

    private List<Entity> GetOrCreateCell(int key)
    {
        if (!_cells.TryGetValue(key, out var list))
        {
            list = new List<Entity>();
            _cells[key] = list;
        }
        return list;
    }

    /// <summary>Number of entities currently registered in the index.</summary>
    public int Count => _positions.Count;
}
