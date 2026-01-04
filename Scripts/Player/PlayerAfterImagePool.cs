using UnityEngine;
using System.Collections.Generic;

public class PlayerAfterImagePool : MonoBehaviour
{
    public GameObject ghostPrefab;
    public float spawnRate = 0.05f;
    public int initialPoolSize = 10;

    private PlayerController player;
    private float nextSpawnTime;
    private Queue<GameObject> availableObjects = new Queue<GameObject>();

    private void Awake()
    {
        player = GetComponent<PlayerController>();
        InitializePool();
    }

    private void InitializePool()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            CreateNewGhost();
        }
    }

    private GameObject CreateNewGhost()
    {
        GameObject instance = Instantiate(ghostPrefab);
        instance.transform.SetParent(transform);
        instance.SetActive(false);
        availableObjects.Enqueue(instance);
        return instance;
    }

    public void CheckIfShouldPlaceGhost()
    {
        if (Time.time >= nextSpawnTime)
        {
            SpawnGhost();
            nextSpawnTime = Time.time + spawnRate;
        }
    }

    private void SpawnGhost()
    {
        if (player.SR == null) return;

        GameObject currentGhost;
        if (availableObjects.Count > 0)
        {
            currentGhost = availableObjects.Dequeue();
        }
        else
        {
            currentGhost = CreateNewGhost();
            availableObjects.Dequeue();
        }

        currentGhost.SetActive(true);
        currentGhost.transform.SetParent(null);
        currentGhost.transform.position = player.SR.transform.position;
        currentGhost.transform.rotation = player.SR.transform.rotation;

        GhostSprite ghostScript = currentGhost.GetComponent<GhostSprite>();
        if (ghostScript != null)
        {
            ghostScript.Init(
                player.SR.sprite,
                player.SR.flipX,
                player.SR.transform.position,
                player.SR.transform.localScale,
                this
            );
        }
    }

    public void ReturnToPool(GameObject instance)
    {
        instance.SetActive(false);
        instance.transform.SetParent(transform);
        availableObjects.Enqueue(instance);
    }
}