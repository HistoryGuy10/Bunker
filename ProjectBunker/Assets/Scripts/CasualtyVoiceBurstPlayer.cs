using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CasualtyVoiceBurstPlayer : MonoBehaviour
{
    [Serializable]
    public class VoiceEntry
    {
        public string label;
        public AudioClip clip;

        [Min(0f)]
        public float weight = 1f;

        [Range(0f, 1f)]
        public float volumeMin = 0.85f;

        [Range(0f, 1f)]
        public float volumeMax = 1f;

        public float pitchMin = 0.92f;
        public float pitchMax = 1.08f;
    }

    [Header("Voice Entries")]
    [SerializeField] private List<VoiceEntry> voiceEntries = new List<VoiceEntry>();

    [Header("Burst Chance")]
    [SerializeField, Range(0f, 1f)] private float overallChanceToPlay = 1f;

    [Header("Burst Size")]
    [SerializeField] private int minVoicesPerBurst = 2;
    [SerializeField] private int maxVoicesPerBurst = 4;
    [SerializeField] private bool allowDuplicateEntriesInSameBurst = true;
    [SerializeField] private float spacingBetweenVoices = 0.03f;

    [Header("Source Pool")]
    [SerializeField] private AudioSource sourceTemplate;
    [SerializeField] private int sourcePoolSize = 6;
    [SerializeField] private bool force2D = true;

    [Header("Master")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField] private float cooldown = 0.05f;

    private readonly List<AudioSource> sourcePool = new List<AudioSource>();
    private int nextSourceIndex = 0;
    private float nextAllowedTime = -999f;
    private Coroutine burstRoutine;

    private void Awake()
    {
        BuildSourcePool();
    }

    private void Reset()
    {
        sourceTemplate = GetComponent<AudioSource>();
    }

    public void PlayHitVoices()
    {
        int minCount = Mathf.Max(1, minVoicesPerBurst);
        int maxCount = Mathf.Max(minCount, maxVoicesPerBurst);

        int count = UnityEngine.Random.Range(minCount, maxCount + 1);
        TryPlayBurst(count);
    }

    public void PlayHitVoicesExact(int exactCount)
    {
        TryPlayBurst(exactCount);
    }

    public void PlayHitVoicesFromIntensity(int intensity)
    {
        int clamped = Mathf.Clamp(intensity, 1, Mathf.Max(1, sourcePoolSize));
        TryPlayBurst(clamped);
    }

    public void StopAllVoices()
    {
        if (burstRoutine != null)
        {
            StopCoroutine(burstRoutine);
            burstRoutine = null;
        }

        for (int i = 0; i < sourcePool.Count; i++)
        {
            if (sourcePool[i] != null)
            {
                sourcePool[i].Stop();
            }
        }
    }

    private void TryPlayBurst(int requestedVoiceCount)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (Time.time < nextAllowedTime)
        {
            return;
        }

        if (voiceEntries == null || voiceEntries.Count == 0)
        {
            return;
        }

        if (UnityEngine.Random.value > overallChanceToPlay)
        {
            return;
        }

        int availableEntries = CountPlayableEntries();
        if (availableEntries <= 0)
        {
            return;
        }

        int maxAllowedByEntries = allowDuplicateEntriesInSameBurst
            ? Mathf.Max(1, sourcePoolSize)
            : availableEntries;

        int voiceCount = Mathf.Clamp(requestedVoiceCount, 1, Mathf.Max(1, maxAllowedByEntries));

        if (burstRoutine != null)
        {
            StopCoroutine(burstRoutine);
        }

        burstRoutine = StartCoroutine(PlayBurstRoutine(voiceCount));
        nextAllowedTime = Time.time + cooldown;
    }

    private IEnumerator PlayBurstRoutine(int voiceCount)
    {
        HashSet<int> usedIndices = allowDuplicateEntriesInSameBurst ? null : new HashSet<int>();

        for (int i = 0; i < voiceCount; i++)
        {
            int entryIndex = GetWeightedEntryIndex(usedIndices);

            if (entryIndex < 0)
            {
                break;
            }

            VoiceEntry entry = voiceEntries[entryIndex];
            PlaySingleEntry(entry);

            if (usedIndices != null)
            {
                usedIndices.Add(entryIndex);
            }

            if (i < voiceCount - 1 && spacingBetweenVoices > 0f)
            {
                yield return new WaitForSeconds(spacingBetweenVoices);
            }
        }

        burstRoutine = null;
    }

    private void PlaySingleEntry(VoiceEntry entry)
    {
        if (entry == null || entry.clip == null)
        {
            return;
        }

        AudioSource source = GetNextSource();
        if (source == null)
        {
            return;
        }

        float lowPitch = Mathf.Min(entry.pitchMin, entry.pitchMax);
        float highPitch = Mathf.Max(entry.pitchMin, entry.pitchMax);

        float lowVolume = Mathf.Min(entry.volumeMin, entry.volumeMax);
        float highVolume = Mathf.Max(entry.volumeMin, entry.volumeMax);

        source.clip = entry.clip;
        source.pitch = UnityEngine.Random.Range(lowPitch, highPitch);
        source.volume = UnityEngine.Random.Range(lowVolume, highVolume) * masterVolume;
        source.Play();
    }

    private int GetWeightedEntryIndex(HashSet<int> excluded)
    {
        float totalWeight = 0f;

        for (int i = 0; i < voiceEntries.Count; i++)
        {
            VoiceEntry entry = voiceEntries[i];
            if (!IsEntryPlayable(entry))
            {
                continue;
            }

            if (excluded != null && excluded.Contains(i))
            {
                continue;
            }

            totalWeight += entry.weight;
        }

        if (totalWeight <= 0f)
        {
            return -1;
        }

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float running = 0f;
        int lastValidIndex = -1;

        for (int i = 0; i < voiceEntries.Count; i++)
        {
            VoiceEntry entry = voiceEntries[i];
            if (!IsEntryPlayable(entry))
            {
                continue;
            }

            if (excluded != null && excluded.Contains(i))
            {
                continue;
            }

            running += entry.weight;
            lastValidIndex = i;

            if (roll <= running)
            {
                return i;
            }
        }

        return lastValidIndex;
    }

    private bool IsEntryPlayable(VoiceEntry entry)
    {
        return entry != null && entry.clip != null && entry.weight > 0f;
    }

    private int CountPlayableEntries()
    {
        int count = 0;

        for (int i = 0; i < voiceEntries.Count; i++)
        {
            if (IsEntryPlayable(voiceEntries[i]))
            {
                count++;
            }
        }

        return count;
    }

    private AudioSource GetNextSource()
    {
        if (sourcePool.Count == 0)
        {
            return null;
        }

        for (int i = 0; i < sourcePool.Count; i++)
        {
            int index = (nextSourceIndex + i) % sourcePool.Count;
            AudioSource source = sourcePool[index];

            if (source != null && !source.isPlaying)
            {
                nextSourceIndex = (index + 1) % sourcePool.Count;
                return source;
            }
        }

        AudioSource stolen = sourcePool[nextSourceIndex];
        nextSourceIndex = (nextSourceIndex + 1) % sourcePool.Count;

        if (stolen != null)
        {
            stolen.Stop();
        }

        return stolen;
    }

    private void BuildSourcePool()
    {
        sourcePool.Clear();

        if (sourceTemplate == null)
        {
            sourceTemplate = GetComponent<AudioSource>();

            if (sourceTemplate == null)
            {
                sourceTemplate = gameObject.AddComponent<AudioSource>();
            }
        }

        AudioSource[] existing = GetComponents<AudioSource>();

        ConfigureSource(sourceTemplate);
        sourcePool.Add(sourceTemplate);

        for (int i = 0; i < existing.Length; i++)
        {
            AudioSource source = existing[i];

            if (source == null || source == sourceTemplate)
            {
                continue;
            }

            if (sourcePool.Count >= sourcePoolSize)
            {
                break;
            }

            CopyTemplateSettings(sourceTemplate, source);
            sourcePool.Add(source);
        }

        while (sourcePool.Count < Mathf.Max(1, sourcePoolSize))
        {
            AudioSource newSource = gameObject.AddComponent<AudioSource>();
            CopyTemplateSettings(sourceTemplate, newSource);
            sourcePool.Add(newSource);
        }
    }

    private void ConfigureSource(AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.playOnAwake = false;
        source.loop = false;
        source.clip = null;
        source.pitch = 1f;
        source.volume = 1f;

        if (force2D)
        {
            source.spatialBlend = 0f;
        }
    }

    private void CopyTemplateSettings(AudioSource from, AudioSource to)
    {
        if (from == null || to == null)
        {
            return;
        }

        to.playOnAwake = false;
        to.loop = false;
        to.clip = null;

        to.outputAudioMixerGroup = from.outputAudioMixerGroup;
        to.mute = from.mute;
        to.bypassEffects = from.bypassEffects;
        to.bypassListenerEffects = from.bypassListenerEffects;
        to.bypassReverbZones = from.bypassReverbZones;
        to.priority = from.priority;
        to.panStereo = from.panStereo;
        to.reverbZoneMix = from.reverbZoneMix;
        to.dopplerLevel = from.dopplerLevel;
        to.spread = from.spread;
        to.rolloffMode = from.rolloffMode;
        to.minDistance = from.minDistance;
        to.maxDistance = from.maxDistance;
        to.ignoreListenerPause = from.ignoreListenerPause;

        to.spatialBlend = force2D ? 0f : from.spatialBlend;
        to.pitch = 1f;
        to.volume = 1f;
    }
}
