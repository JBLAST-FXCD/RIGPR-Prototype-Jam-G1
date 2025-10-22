// behavior states for flow control
// extend as new states added, logic should be handled in NPCController
public enum NPCState
{
    Idle,
    Moving,
    InQueue,
    Processing,
    Done,
    DoneAll
}
