using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class FlowManager : MonoBehaviour
{
    public static FlowManager Instance {get; private set; }

    // flow sequence between stages, linear finite state machine
    private Dictionary<StageType, StageType> flowMap = new Dictionary<StageType, StageType>()
    {
        { StageType.CheckIn, StageType.Security },
        { StageType.Security, StageType.Boarding },
        { StageType.Boarding, StageType.None }
    };

    private Dictionary<StageType, StageController> stages = new Dictionary<StageType, StageController>();

    private void Awake()
    {
        Instance = this;

        // registers all objects with StageController in scene, designed for modularity as player adds/removes stages in game
        foreach (var s in FindObjectsOfType<StageController>())
        {
            stages[s.stageType] = s;
            s.OnNPCProcessed += HandleNPCFinished;
        }
    }

    private void HandleNPCFinished(NPCController npc)
    {
        // assign next stage based on flow definition
        StageType next = GetNextStage(npc.currentStage);
        npc.currentStage = next;

        if (next != StageType.None)
        {
            var nextStage = stages[next];
            npc.GoToStage(stages[next]);
            nextStage.ForceQueueUpdate();
        }
        else
        {
            // terminal/boarded stage - can be expanded for game feel
            npc.FlagDoneAll();
        }
    }

    public StageType GetNextStage(StageType current)
    {
        return flowMap.ContainsKey(current) ? flowMap[current] : StageType.None;
    }

    public StageController GetStage(StageType type)
    {
        if (stages.ContainsKey(type))
        {
            return stages[type];
        }
        else
        {
            return null;
        }
    }
}
