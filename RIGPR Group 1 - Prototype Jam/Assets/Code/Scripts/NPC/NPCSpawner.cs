using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCSpawner : MonoBehaviour
{
    public static NPCSpawner Instance;

    [SerializeField] private List<GameObject> npcPrefabs;
    [SerializeField] private int poolSize = 50;
    [SerializeField] private float spawnInterval = 3f;

    private Dictionary<GameObject, List<NPCController>> npcPool = new();
    private float timer;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        foreach (var prefab in npcPrefabs)
        {
            var pool = new List<NPCController>();

            for (int i = 0; i < poolSize; i++)
            {
                var npcObj = Instantiate(prefab, transform);
                npcObj.SetActive(false);
                var npc = npcObj.GetComponent<NPCController>();
                pool.Add(npc);
            }

            npcPool[prefab] = pool;
        }
    }

    private void Update()
    {
        timer -= Time.deltaTime;

        if (timer<= 0)
        {
            SpawnNPC();
            timer = spawnInterval;
        }
    }

    private void SpawnNPC()
    {

        var checkInStage = FlowManager.Instance.GetStage(StageType.CheckIn);

        GameObject chosenPrefab = npcPrefabs[Random.Range(0, npcPrefabs.Count)];

        var npc = npcPool[chosenPrefab].Find(n => n != null && !n.gameObject.activeSelf);

        npc.gameObject.SetActive(true);
        npc.ResetNPC();
        checkInStage.EnqueueNPC(npc);
    }

    public void RecycleNPC(NPCController npc)
    {
        npc.gameObject.SetActive(false);
    }
}
