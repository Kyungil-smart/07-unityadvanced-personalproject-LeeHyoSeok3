using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Tiles/MultiLayer Rule Tile")]
public class MultiLayerRuleTile : RuleTile<MultiLayerRuleTile.Neighbor>
{
    [Header("조건으로 쓸 다른 레이어 타일")] 
    public TileBase stairTile;
    public TileBase flatGroundTile;
    public TileBase elevatedGroundTile1;
    public TileBase elevatedGroundTile2;
    public TileBase cliffTile;


    public class Neighbor : RuleTile.TilingRule.Neighbor
    {
        public const int IsStair = 3;
        public const int IsFlatGround = 4;
        public const int IsElevatedGround1 = 5;
        public const int IsElevatedGround2 = 6;
        public const int IsCliff = 7;
        public const int IsWater = 8;
    }

    // -------------------------------------------------------
    // Tilemap 목록 캐시 (매번 씬 탐색 방지)
    // -------------------------------------------------------
    private List<Tilemap> _cachedTilemaps = null;
    private Tilemap _lastTilemap = null;

    private List<Tilemap> GetCachedTilemaps(ITilemap currentTilemap)
    {
        var tilemapComponent = currentTilemap.GetComponent<Tilemap>();
        if (tilemapComponent == null) return null;

        // 현재 Tilemap이 바뀌었거나 캐시가 없으면 갱신
        if (_cachedTilemaps == null || _lastTilemap != tilemapComponent)
        {
            _lastTilemap = tilemapComponent;
            _cachedTilemaps = new List<Tilemap>();

            var grid = tilemapComponent.transform.parent;
            if (grid == null) return _cachedTilemaps;

            foreach (var tm in grid.GetComponentsInChildren<Tilemap>())
                _cachedTilemaps.Add(tm);
        }

        return _cachedTilemaps;
    }

    // -------------------------------------------------------
    // 특정 위치의 모든 레이어 타일을 수집
    // -------------------------------------------------------
    private List<TileBase> GetAllTilesAtPosition(Vector3Int position, ITilemap currentTilemap)
    {
        var tiles = new List<TileBase>();

        var tilemaps = GetCachedTilemaps(currentTilemap);
        if (tilemaps == null) return tiles;

        foreach (var tm in tilemaps)
        {
            if (tm == null) continue;
            TileBase tile = tm.GetTile(position);
            if (tile != null && !tiles.Contains(tile))
                tiles.Add(tile);
        }

        return tiles;
    }

    // -------------------------------------------------------
    // 단일 타일 조건 검사
    // -------------------------------------------------------
    public override bool RuleMatch(int neighbor, TileBase tile)
    {
        switch (neighbor)
        {
            case Neighbor.IsStair:
                return tile == stairTile;
            case Neighbor.IsFlatGround:
                return tile == flatGroundTile;
            case Neighbor.IsElevatedGround1:
                return tile == elevatedGroundTile1;
            case Neighbor.IsElevatedGround2:
                return tile == elevatedGroundTile2;
            case Neighbor.IsCliff:
                return tile == cliffTile;
            case Neighbor.IsWater:
                return tile == null;
        }

        return base.RuleMatch(neighbor, tile);
    }

    // -------------------------------------------------------
    // 핵심: 조건 종류에 따라 OR / AND 로직 분리
    // -------------------------------------------------------
    private bool RuleMatchWithAllTiles(int neighbor, List<TileBase> tiles)
    {
        // IsWater: 해당 위치에 아무 타일도 없어야 함
        // if (neighbor == Neighbor.IsWater)
        //     return tiles.Count == 0;

        // 커스텀 양성 조건 (OR: 하나라도 맞으면 true)
        if (neighbor == Neighbor.IsStair ||
            neighbor == Neighbor.IsFlatGround ||
            neighbor == Neighbor.IsElevatedGround1 ||
            neighbor == Neighbor.IsElevatedGround2)
        {
            foreach (var tile in tiles)
            {
                if (RuleMatch(neighbor, tile)) return true;
            }

            return false;
        }

        // This (1): 하나라도 이 Rule Tile이면 true (OR)
        if (neighbor == TilingRule.Neighbor.This)
        {
            foreach (var tile in tiles)
            {
                if (base.RuleMatch(TilingRule.Neighbor.This, tile)) return true;
            }

            return false;
        }

        // NotThis (2): 모든 타일이 이 Rule Tile이 아니어야 true (AND)
        // → 하나라도 이 Rule Tile이면 false
        if (neighbor == TilingRule.Neighbor.NotThis)
        {
            if (tiles.Count == 0) return true; // 빈칸은 NotThis 통과

            foreach (var tile in tiles)
            {
                if (base.RuleMatch(TilingRule.Neighbor.This, tile)) return false;
            }

            return true;
        }

        // 기타 기본 조건: 첫 번째 타일로 검사
        TileBase firstTile = tiles.Count > 0 ? tiles[0] : null;
        return base.RuleMatch(neighbor, firstTile);
    }

    // -------------------------------------------------------
    // 실제 규칙 매칭 (변환 행렬 적용)
    // -------------------------------------------------------
    private bool DoesRuleMatch(TilingRule rule, Vector3Int position, ITilemap tilemap, Matrix4x4 transform)
    {
        for (int i = 0; i < rule.m_Neighbors.Count; i++)
        {
            int neighbor = rule.m_Neighbors[i];
            Vector3Int neighborPos = rule.m_NeighborPositions[i];

            // 변환 행렬 적용 (회전/미러 처리)
            Vector3 rotated = transform.MultiplyPoint3x4(neighborPos);
            Vector3Int rotatedPos = new Vector3Int(
                Mathf.RoundToInt(rotated.x),
                Mathf.RoundToInt(rotated.y),
                Mathf.RoundToInt(rotated.z)
            );

            Vector3Int worldPos = position + rotatedPos;

            // 해당 위치의 모든 레이어 타일 수집
            List<TileBase> allTiles = GetAllTilesAtPosition(worldPos, tilemap);

            // OR/AND 로직으로 조건 검사
            if (!RuleMatchWithAllTiles(neighbor, allTiles))
                return false;
        }

        return true;
    }

    // -------------------------------------------------------
    // RuleMatches 오버라이드 (회전/미러 변환 포함)
    // -------------------------------------------------------
    public override bool RuleMatches(TilingRule rule, Vector3Int position, ITilemap tilemap, ref Matrix4x4 transform)
    {
        // 기본 방향
        if (DoesRuleMatch(rule, position, tilemap, Matrix4x4.identity))
        {
            transform = Matrix4x4.identity;
            return true;
        }

        // 회전
        if (rule.m_RuleTransform == TilingRuleOutput.Transform.Rotated)
        {
            for (int i = 1; i < 4; i++)
            {
                Matrix4x4 mat = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, 90 * i), Vector3.one);
                if (DoesRuleMatch(rule, position, tilemap, mat))
                {
                    transform = mat;
                    return true;
                }
            }
        }
        else if (rule.m_RuleTransform == TilingRuleOutput.Transform.MirrorX)
        {
            Matrix4x4 mat = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(-1, 1, 1));
            if (DoesRuleMatch(rule, position, tilemap, mat))
            {
                transform = mat;
                return true;
            }
        }
        else if (rule.m_RuleTransform == TilingRuleOutput.Transform.MirrorY)
        {
            Matrix4x4 mat = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1, -1, 1));
            if (DoesRuleMatch(rule, position, tilemap, mat))
            {
                transform = mat;
                return true;
            }
        }
        else if (rule.m_RuleTransform == TilingRuleOutput.Transform.MirrorXY)
        {
            Matrix4x4 mat = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(-1, -1, 1));
            if (DoesRuleMatch(rule, position, tilemap, mat))
            {
                transform = mat;
                return true;
            }
        }

        return false;
    }
}