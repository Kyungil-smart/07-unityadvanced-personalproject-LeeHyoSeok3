using UnityEngine;
using UnityEngine.Tilemaps;


[ExecuteInEditMode]
public class TilemapRegistry : MonoBehaviour
{
    void OnEnable() // Awake 대신 OnEnable 사용
    {
        MultiLayerRuleTile.allTilemaps.Clear();
        Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        foreach (var tm in tilemaps)
        {
            MultiLayerRuleTile.allTilemaps.Add(tm);
        }
    }
}