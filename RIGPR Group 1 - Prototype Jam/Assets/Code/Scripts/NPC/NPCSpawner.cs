using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCSpawner : MonoBehaviour
{
    public static NPCSpawner Instance;

    [SerializeField] private GameObject npcPrefab;
    [SerializeField] private int poolSize = 50;
    [SerializeField] private float spawnInterval = 3f;

    private List<NPCController> npcPool = new List<NPCController>();
    private float timer;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        for (int i=0; i < poolSize; i++)
        {
            var npcObj = Instantiate(npcPrefab, transform);
            npcObj.gameObject.SetActive(false);

            var npc = npcObj.GetComponent<NPCController>();
            npcPool.Add(npc);
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
        NPCController npc = npcPool.Find(n => !n.gameObject.activeSelf);

        if (npc == null)
        {
            return;
        }

        npc.gameObject.SetActive(true);
        npc.ResetNPC();
        FlowManager.Instance.GetStage(StageType.CheckIn).EnqueueNPC(npc);
    }

    public void RecycleNPC(NPCController npc)
    {
        npc.gameObject.SetActive(false);
    }
}
