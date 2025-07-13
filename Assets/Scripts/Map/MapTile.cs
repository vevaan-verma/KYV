using System;
using UnityEngine;
using UnityEngine.Tilemaps;

[Serializable]
public class MapTile {

    [Header("Properties")]
    [SerializeField] private TileBase tile;
    [SerializeField][Range(0, 100)] private float spawnProbability;

    public TileBase GetTile() => tile;

    public float GetSpawnProbability() => spawnProbability;

}
