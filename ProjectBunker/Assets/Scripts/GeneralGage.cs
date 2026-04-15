using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Events;

public class GeneralGage : MonoBehaviour
{
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool loop = false;
    [SerializeField] private bool useUnscaledTime = false;

    [Header("Timeline")]
    [SerializeField] private List<TimedSequenceEvent> events = new List<TimedSequenceEvent>();

    [Header("Timeline End")]
    [SerializeField, Min(0f)] private float endTime = 0f;
    [SerializeField] private UnityEvent onTimelineEnded;

    public float ElapsedTime => elapsedTime;
    public float EndTime => endTime;
    public bool IsPlaying => isPlaying;

    public float elapsedTime;
    private int nextEventIndex;
    private bool isPlaying;
    private bool timelineEndInvoked;

    // Runtime-only sorted view of the authored events
    private readonly List<RuntimeTimedEvent> runtimeEvents = new List<RuntimeTimedEvent>();

    private bool HasExplicitEndTime => endTime > 0f;

    private void Start()
    {
        RebuildRuntimeTimeline();

        if (playOnStart)
        {
            PlayFromBeginning();
        }
    }

    private void Update()
    {
        if (!isPlaying) return;

        elapsedTime += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        while (nextEventIndex < runtimeEvents.Count)
        {
            TimedSequenceEvent nextTimedEvent = runtimeEvents[nextEventIndex].timedEvent;
            if (nextTimedEvent == null)
            {
                nextEventIndex++;
                continue;
            }

            // Do not fire events that are scheduled after the explicit timeline end.
            if (HasExplicitEndTime && nextTimedEvent.time > endTime)
            {
                break;
            }

            if (elapsedTime < nextTimedEvent.time)
            {
                break;
            }

            TriggerEvent(nextTimedEvent);
            nextEventIndex++;
        }

        if (HasExplicitEndTime)
        {
            if (!timelineEndInvoked && elapsedTime >= endTime)
            {
                FinishTimeline();
            }
        }
        else if (nextEventIndex >= runtimeEvents.Count)
        {
            FinishTimeline();
        }
    }

    public void PlayFromBeginning()
    {
        RebuildRuntimeTimeline();
        elapsedTime = 0f;
        nextEventIndex = 0;
        isPlaying = true;
        timelineEndInvoked = false;
    }

    public void Pause()
    {
        isPlaying = false;
    }

    public void Resume()
    {
        isPlaying = true;
    }

    public void Stop()
    {
        isPlaying = false;
        elapsedTime = 0f;
        nextEventIndex = 0;
        timelineEndInvoked = false;
    }

    public void TriggerEventNow(int index)
    {
        if (index < 0 || index >= runtimeEvents.Count) return;
        TriggerEvent(runtimeEvents[index].timedEvent);
    }

    private void FinishTimeline()
    {
        if (timelineEndInvoked) return;

        timelineEndInvoked = true;
        onTimelineEnded?.Invoke();

        if (loop)
        {
            PlayFromBeginning();
        }
        else
        {
            isPlaying = false;
        }
    }

    private void TriggerEvent(TimedSequenceEvent timedEvent)
    {
        if (timedEvent == null) return;

        for (int i = 0; i < timedEvent.moveOrders.Count; i++)
        {
            UnitMoveOrder order = timedEvent.moveOrders[i];
            if (order == null || order.unit == null) continue;

            Vector3 destination = order.GetWorldDestination();
            CustomEvent.Trigger(order.unit.gameObject, "GoToPosition", destination);
        }

        for (int i = 0; i < timedEvent.fireOrders.Count; i++)
        {
            UnitFireOrder order = timedEvent.fireOrders[i];
            if (order == null || order.unit == null) continue;

            order.Fire();
        }

        for (int i = 0; i < timedEvent.unitsToStop.Count; i++)
        {
            GameObject unit = timedEvent.unitsToStop[i];
            if (unit == null) continue;
            CustomEvent.Trigger(unit, "Stop");
        }

        for (int i = 0; i < timedEvent.stopFireOrders.Count; i++)
        {
            UnitStopFire order = timedEvent.stopFireOrders[i];
            if (order == null || order.unit == null) continue;
            order.Stop();
        }

        for (int i = 0; i < timedEvent.setFallbackOrders.Count; i++)
        {
            UnitSetFallbackOrder order = timedEvent.setFallbackOrders[i];
            if (order == null || order.unit == null) continue;
            order.SetFallback();
        }

        timedEvent.onTriggered?.Invoke();
    }

    private void RebuildRuntimeTimeline()
    {
        runtimeEvents.Clear();

        for (int i = 0; i < events.Count; i++)
        {
            runtimeEvents.Add(new RuntimeTimedEvent(events[i], i));
        }

        runtimeEvents.Sort((a, b) =>
        {
            int timeCompare = a.timedEvent.time.CompareTo(b.timedEvent.time);
            if (timeCompare != 0) return timeCompare;

            // Preserve inspector order for same-time events
            return a.originalIndex.CompareTo(b.originalIndex);
        });
    }

    [ContextMenu("Sort Authored Events By Time")]
    public void SortAuthoredEvents()
    {
        // Manual only. Do not call this in OnValidate.
        var indexed = new List<RuntimeTimedEvent>();

        for (int i = 0; i < events.Count; i++)
        {
            indexed.Add(new RuntimeTimedEvent(events[i], i));
        }

        indexed.Sort((a, b) =>
        {
            int timeCompare = a.timedEvent.time.CompareTo(b.timedEvent.time);
            if (timeCompare != 0) return timeCompare;

            return a.originalIndex.CompareTo(b.originalIndex);
        });

        events.Clear();
        for (int i = 0; i < indexed.Count; i++)
        {
            events.Add(indexed[i].timedEvent);
        }
    }

    private struct RuntimeTimedEvent
    {
        public TimedSequenceEvent timedEvent;
        public int originalIndex;

        public RuntimeTimedEvent(TimedSequenceEvent timedEvent, int originalIndex)
        {
            this.timedEvent = timedEvent;
            this.originalIndex = originalIndex;
        }
    }
}

[Serializable]
public class TimedSequenceEvent
{
    public string label = "New Event";
    [Min(0f)] public float time = 0f;
    [TextArea(2, 4)] public string notes;

    [Header("Unit Commands")]
    public List<UnitMoveOrder> moveOrders = new List<UnitMoveOrder>();
    public List<UnitFireOrder> fireOrders = new List<UnitFireOrder>();
    public List<UnitStopFire> stopFireOrders = new List<UnitStopFire>();
    public List<GameObject> unitsToStop = new List<GameObject>();
    public List<UnitSetFallbackOrder> setFallbackOrders = new List<UnitSetFallbackOrder>();

    [Header("Extra Inspector Hooks")]
    public UnityEvent onTriggered;
}

[Serializable]
public class UnitMoveOrder
{
    public ScriptMachine unit;
    public Transform targetTransform;
    public Vector3 targetOffset;

    public Vector3 GetWorldDestination()
    {
        Vector3 basePosition = targetTransform != null ? targetTransform.position : Vector3.zero;
        return basePosition + targetOffset;
    }
}

public enum FireMode { Low, Mid, Long }

[Serializable]
public class UnitFireOrder
{
    public ScriptMachine unit;
    public FireMode fireMode = FireMode.Low;

    public void Fire()
    {
        if (unit == null) return;
        CustomEvent.Trigger(unit.gameObject, "Engage");
    }
}

[Serializable]
public class UnitStopFire
{
    public ScriptMachine unit;

    public void Stop()
    {
        if (unit == null) return;
        CustomEvent.Trigger(unit.gameObject, "Disengage");
    }
}

[Serializable]
public class UnitSetFallbackOrder
{
    public ScriptMachine unit;
    public void SetFallback()
    {
        if (unit == null) return;
        CustomEvent.Trigger(unit.gameObject, "SetFallBack", unit.transform.position);
    }
}