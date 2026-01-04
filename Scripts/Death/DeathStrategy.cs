using UnityEngine;
using System.Collections;

public abstract class DeathStrategy : ScriptableObject
{
    [Header("Base Settings")]
    public float deathDuration = 1.0f;
    public AudioClip hitSFX;
    public AudioClip deathSFX;

    [Header("Respawn Settings")]
    public float respawnDelay = 0.2f;
    public AudioClip respawnSFX;

    public abstract IEnumerator ExecuteDeath(PlayerController player, Vector3 hitDirection);
    public abstract IEnumerator TransitionIn(GameManager gm);
    public abstract IEnumerator ExecuteRespawn(PlayerController player, Vector2 spawnPos);
    public abstract IEnumerator TransitionOut(GameManager gm);
}