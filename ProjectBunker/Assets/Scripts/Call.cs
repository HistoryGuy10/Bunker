using UnityEngine;

public class Call : MonoBehaviour
{
    [SerializeField] private AudioSource localAudioSource;

    [Header("Command Clips")]
    [SerializeField] private AudioClip fireForward1;
    [SerializeField] private AudioClip fireForward2;
    [SerializeField] private AudioClip reload1;
    [SerializeField] private AudioClip reload2;

    [Header("Pitch Randomization")]
    [SerializeField] private bool randomizePitchOnStart = true;
    [SerializeField] private float minPitch = 0.95f;
    [SerializeField] private float maxPitch = 1.05f;

    [Header("Playback")]
    [SerializeField] private bool stopCurrentClipBeforePlaying = true;

    private void Reset()
    {
        if (localAudioSource != null)
        {
            localAudioSource.playOnAwake = false;
        }
    }

    private void Start()
    {
        if (randomizePitchOnStart)
        {
            ApplyRandomPitch();
        }
    }

    public void ApplyRandomPitch()
    {
        float low = Mathf.Min(minPitch, maxPitch);
        float high = Mathf.Max(minPitch, maxPitch);

        localAudioSource.pitch = UnityEngine.Random.Range(low, high);
    }

    public void SetPitch(float newPitch)
    {

        localAudioSource.pitch = newPitch;
    }

    public void PlayFireForward1()
    {
        PlayClip(fireForward1);
    }

    public void PlayFireForward2()
    {
        PlayClip(fireForward2);
    }

    public void PlayReload1()
    {
        PlayClip(reload1);
    }

    public void PlayReload2()
    {
        PlayClip(reload2);
    }

    public void StopVoice()
    {
        if (localAudioSource == null)
        {
            return;
        }

        localAudioSource.Stop();
    }

    private void PlayClip(AudioClip clip)
    {
        if (stopCurrentClipBeforePlaying)
        {
            localAudioSource.Stop();
            localAudioSource.clip = clip;
            localAudioSource.Play();
        }
        else
        {
            localAudioSource.PlayOneShot(clip);
        }
    }
}
