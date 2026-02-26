using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Tiles/MultiLayer Rule Tile")]
public class MultiLayerRuleTile : RuleTile<MultiLayerRuleTile.Neighbor>
{
    [Header("조건으로 쓸 다른 레이어 타일")]
    public TileBase stairTile;
    public TileBase flatGroundTile;
    public TileBase elevatedGroundTile;
    public TileBase waterTile;

    // 다른 Tilemap들을 런타임에 등록할 정적 목록
    public static List<Tilemap> allTilemaps = new List<Tilemap>();

    public class Neighbor : RuleTile.TilingRule.Neighbor
    {
        public const int IsStair      = 3;
        public const int IsFlatGround = 4;
        public const int IsElevatedGround = 5;
        public const int IsWater      = 6;
    }

    // 특정 위치에서 모든 레이어를 뒤져서 타일을 찾아옴
    private TileBase GetTileFromAllLayers(Vector3Int position)
    {
        foreach (var tilemap in allTilemaps)
        {
            if (tilemap == null) continue;
            TileBase tile = tilemap.GetTile(position);
            if (tile != null) return tile;
        }
        return null;
    }

    public override bool RuleMatch(int neighbor, TileBase tile)
    {
        switch (neighbor)
        {
            case Neighbor.IsStair:
                return tile == stairTile;
            case Neighbor.IsFlatGround:
                return tile == flatGroundTile;
            case Neighbor.IsElevatedGround:
                return tile == elevatedGroundTile;
            case Neighbor.IsWater:
                return tile == null;
        }
        return base.RuleMatch(neighbor, tile);
    }

    // RuleMatch 호출 전에 다른 레이어 타일로 교체
    public override bool RuleMatches(TilingRule rule, Vector3Int position, ITilemap tilemap, ref Matrix4x4 transform)
    {
        for (int i = 0; i < rule.m_Neighbors.Count; i++)
        {
            int neighbor = rule.m_Neighbors[i];
            Vector3Int neighborPos = position + rule.m_NeighborPositions[i];

            // 현재 레이어에서 먼저 확인
            TileBase tile = tilemap.GetTile(neighborPos);

            // 없으면 다른 레이어에서 찾기
            if (tile == null)
            {
                tile = GetTileFromAllLayers(neighborPos);
            }

            if (!RuleMatch(neighbor, tile))
                return false;
        }
        return true;
    }
}