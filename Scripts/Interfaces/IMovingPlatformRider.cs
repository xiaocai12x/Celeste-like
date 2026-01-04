using UnityEngine;

public interface IMovingPlatformRider
{
    void ManualMove(Vector2 delta);
    bool CheckWallForCrush(Vector2 pushDirection, float dist);
    void DieByCrush(DeathStrategy strategy);
}