using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Rendering;
using UnityEngine;

public class StageController : MonoBehaviour
{
    public StageType stageType;
    public Transform entryPoint;
    // adjust for concurrent processing
    public int maxNPCCapacity = 1;
    //

    [SerializeField] private float queueSpacing = 1.5f;
    [SerializeField] private Transform queueOrigin;

    private Queue<NPCController> npcQueue = new Queue<NPCController>();
    private List<NPCController> processing = new List<NPCController>();

    // allows for external systems to react upon invocation
    public event Action<NPCController> OnNPCProcessed;

    private void Update()
    {
        // move npcs from wait queue to processing if capcacity allows
        while (processing.Count < maxNPCCapacity && npcQueue.Count > 0)
        {
            var npc = npcQueue.Dequeue();
            npc.StartProcessing(this);
            processing.Add(npc);
        }

        UpdateQueuePositions();

        // remove npc when stage complete
        for (int i = processing.Count - 1; i >= 0; i--)
        {
            var npc = processing[i];
            if (npc.StageComplete)
            {
                processing.RemoveAt(i);
                OnNPCProcessed?.Invoke(npc);

                // allows for stages to work simultaneously
                if (npcQueue.Count > 0)
                {
                    var nextNPC = npcQueue.Dequeue();
                    nextNPC.StartProcessing(this);
                    processing.Add(nextNPC);
                }
            }
        }
    }

    // arriving npcs call to join stage queue
    public void EnqueueNPC(NPCController npc)
    {
        npcQueue.Enqueue(npc);
        npc.SetState(NPCState.InQueue);
        UpdateQueuePositions();
    }

    // flag npc as stage completed
    public void NotifyNPCDone(NPCController npc)
    {
        npc.SetState(NPCState.Done);
        npc.StageComplete = true;
    }

    private void UpdateQueuePositions()
    {
        int index = 0;
        foreach (var npc in npcQueue)
        {
            Vector3 targetPosition = queueOrigin.position - transform.forward * queueSpacing * index;
            npc.MoveToQueuePoition(targetPosition);
            index++;
        }
    }
}
