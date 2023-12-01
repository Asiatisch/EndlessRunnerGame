using UnityEngine;


namespace EndlessRunner {

public enum TileType {
    STRAIGHT,
    LEFT,
    RIGHT,
    SIDEWAYS
}

public class Tile : MonoBehaviour
{
    public TileType type;
    public Transform pivot;
}

}