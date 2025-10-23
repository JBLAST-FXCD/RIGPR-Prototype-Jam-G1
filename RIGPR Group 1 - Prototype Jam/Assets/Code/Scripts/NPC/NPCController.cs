using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class NPCController : MonoBehaviour
{
    public StageType currentStage = StageType.CheckIn;

    public float baseProcessTime = 5f;
    // represents percentages
    public float baseSuccessRate = 0.3f;
    public float successRateIncrement = 0.1f;

    private StageController currentStageController;
    private float timer;
    private float currentChance;

    private NPCState state;
    public bool StageComplete = false;

    private void Start()
    {
        currentChance = baseSuccessRate;

        // incomplete, to be updated with movement/pathfinding system
        var startStage = FlowManager.Instance.GetStage(currentStage);
        if (startStage)
        {
            GoToStage(startStage);
        }
    }

    public void GoToStage(StageController stage)
    {
        state = NPCState.Moving;
        StageComplete = false;
        currentStageController = stage;

        // link to npc pathfinding here
        transform.position = stage.entryPoint.position;
        //
        stage.EnqueueNPC(this);
    }

    public void StartProcessing(StageController stage)
    {
        state = NPCState.Processing;
        currentStageController = stage;
        ResetProcessTimer();
    }

    private void ResetProcessTimer()
    {
        timer = baseProcessTime;
    }

    private void Update()
    {
        // timer iterates attempts until success
        if (state == NPCState.Processing && !StageComplete)
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                TryCompleteStage();
                timer = baseProcessTime;
            }
        }
    }

    private void TryCompleteStage()
    {
        float roll = Random.value;
        if (roll <= currentChance)
        {
            currentStageController.NotifyNPCDone(this);
            currentChance = baseSuccessRate;
        }
        else
        {
            // increments chance on failure
            currentChance = Mathf.Min(1f, currentChance + successRateIncrement);
            // potential link to npc "mood" system here
        }
    }

    public void SetState(NPCState newState)
    {
        state = newState;
    }

    public void FlagDoneAll()
    {
        state = NPCState.DoneAll;
        NPCSpawner.Instance.RecycleNPC(this);
    }

    public void ResetNPC()
    {
        StageComplete = false;
        currentStage = StageType.CheckIn;
        currentChance = baseSuccessRate;
        state = NPCState.Idle;
        timer = baseProcessTime;

        currentStageController = null;

        transform.position = Vector3.zero;
    }

    public void MoveToQueuePoition(Vector3 target)
    {
        transform.position = target;
    }
}
