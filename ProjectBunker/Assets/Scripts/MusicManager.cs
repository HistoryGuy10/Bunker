using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MusicManager : MonoBehaviour
{
    [Header("Music Pieces")]
    [SerializeField] private List<AudioClip> musicPieces = new List<AudioClip>();

    [Header("Playback")]
    [SerializeField] private float musicVolume = 1f;
    [SerializeField] private float fadeDuration = 1.5f;
    [SerializeField] private bool ignoreSameIndexRequest = true;

    [Header("Optional")]
    [SerializeField] private int playOnStartIndex = -1;

    private AudioSource sourceA;
    private AudioSource sourceB;

    private AudioSource activeSource;
    private AudioSource inactiveSource;

    private Coroutine transitionRoutine;
    private int currentIndex = -1;
    private bool isTransitioning = false;

    public int CurrentIndex => currentIndex;

    private void Awake()
    {
        SetupAudioSources();
    }

    private void Start()
    {
        if (playOnStartIndex >= 0)
        {
            PlayPieceByIndex(playOnStartIndex);
        }
    }

    private bool wasPausedByTimeScale = false;

    private void Update()
    {
        bool shouldBePaused = Time.timeScale == 0.001f;

        if (shouldBePaused && !wasPausedByTimeScale)
        {
            AudioListener.pause = true;
            wasPausedByTimeScale = true;
        }
        else if (!shouldBePaused && wasPausedByTimeScale)
        {
            AudioListener.pause = false;
            wasPausedByTimeScale = false;
        }
    }

    private void SetupAudioSources()
    {
        AudioSource[] existingSources = GetComponents<AudioSource>();

        if (existingSources.Length >= 2)
        {
            sourceA = existingSources[0];
            sourceB = existingSources[1];
        }
        else if (existingSources.Length == 1)
        {
            sourceA = existingSources[0];
            sourceB = gameObject.AddComponent<AudioSource>();
        }
        else
        {
            sourceA = gameObject.AddComponent<AudioSource>();
            sourceB = gameObject.AddComponent<AudioSource>();
        }

        ConfigureSource(sourceA);
        ConfigureSource(sourceB);

        activeSource = sourceA;
        inactiveSource = sourceB;
    }

    private void ConfigureSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = true;
        source.volume = 0f;
    }

    public void PlayPieceByIndex(int index)
    {
        if (index == -1)
        {
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
            }

            transitionRoutine = StartCoroutine(FadeOutCurrent());
            return;
        }

        if (index < 0 || index >= musicPieces.Count)
        {
            Debug.LogWarning($"{name}: Music index {index} is out of range.", this);
            return;
        }

        AudioClip clipToPlay = musicPieces[index];

        if (clipToPlay == null)
        {
            Debug.LogWarning($"{name}: Music clip at index {index} is null.", this);
            return;
        }

        if (ignoreSameIndexRequest && currentIndex == index && activeSource.isPlaying)
        {
            return;
        }

        if (!activeSource.isPlaying && !isTransitioning)
        {
            StartFirstTrack(index, clipToPlay);
            return;
        }

        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
        }

        transitionRoutine = StartCoroutine(CrossfadeToTrack(index, clipToPlay));
    }

    public void StopMusic()
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }

        isTransitioning = false;
        currentIndex = -1;

        sourceA.Stop();
        sourceB.Stop();
        sourceA.volume = 0f;
        sourceB.volume = 0f;
    }

    public void FadeOutAndStop()
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
        }

        transitionRoutine = StartCoroutine(FadeOutCurrent());
    }

    public void SetMusicVolume(float newVolume)
    {
        musicVolume = Mathf.Clamp01(newVolume);

        if (!isTransitioning)
        {
            if (activeSource != null)
            {
                activeSource.volume = musicVolume;
            }
        }
    }

    private void StartFirstTrack(int index, AudioClip clip)
    {
        activeSource.clip = clip;
        activeSource.volume = musicVolume;
        activeSource.Play();
        currentIndex = index;
    }

    private IEnumerator CrossfadeToTrack(int newIndex, AudioClip newClip)
    {
        isTransitioning = true;

        inactiveSource.Stop();
        inactiveSource.clip = newClip;
        inactiveSource.volume = 0f;
        inactiveSource.Play();

        float startActiveVolume = activeSource.volume;
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float t = fadeDuration <= 0f ? 1f : timer / fadeDuration;

            activeSource.volume = Mathf.Lerp(startActiveVolume, 0f, t);
            inactiveSource.volume = Mathf.Lerp(0f, musicVolume, t);

            yield return null;
        }

        activeSource.Stop();
        activeSource.volume = 0f;
        inactiveSource.volume = musicVolume;

        AudioSource oldActive = activeSource;
        activeSource = inactiveSource;
        inactiveSource = oldActive;

        currentIndex = newIndex;
        isTransitioning = false;
        transitionRoutine = null;
    }

    private IEnumerator FadeOutCurrent()
    {
        isTransitioning = true;

        float startVolume = activeSource.volume;
        float timer = 0f;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float t = fadeDuration <= 0f ? 1f : timer / fadeDuration;

            activeSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        activeSource.Stop();
        activeSource.volume = 0f;

        inactiveSource.Stop();
        inactiveSource.volume = 0f;

        currentIndex = -1;
        isTransitioning = false;
        transitionRoutine = null;
    }
}
