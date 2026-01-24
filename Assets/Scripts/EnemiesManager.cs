using UnityEngine;

public class EnemiesManager : MonoBehaviour
{
    public GameObject enemyPrefab;
    public float spawnRate = 2f;
    public float spawnDistance = 10f;

    private Transform player;
    private float nextSpawnTime = 0f;

    void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
    }

    void Update()
    {
        if (Time.time >= nextSpawnTime)
        {
            SpawnEnemy();
            nextSpawnTime = Time.time + spawnRate;
        }
    }

    void SpawnEnemy()
    {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector2 spawnPosition = (Vector2)player.position + spawnDistance * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

        GameObject enemy = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
        enemy.tag = "Enemy";

        Percussion em = enemy.AddComponent<Percussion>();
        em.target = player;
    }
}
