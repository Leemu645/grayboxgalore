using UnityEngine;

/// <summary>
/// One place for basement one-shots and short loops.
///
/// World sounds are 3D (they fall off with distance). The player's own
/// actions, the flashlight, and the hook game are flat. Looping beds
/// (the room, the furnace) are AudioSources on those objects. This
/// component's master volume is applied to the listener, so it turns
/// both down together.
///
/// An enemy can call PlayFootstep, PlayAt, or PlayStinger without a new
/// audio object. Stingers stay empty until that pass.
/// </summary>
[DisallowMultipleComponent]
public class GameAudio : MonoBehaviour
{
    public static GameAudio Instance { get; private set; }

    const int VoiceCount = 16;

    [Header("Mix")]
    [Tooltip("Scales every sound, including the room tone and the furnace. 1 is full. 0 is silent.")]
    [Range(0f, 1f)]
    [SerializeField] float masterVolume = 0.85f;

    [Header("Footsteps")]
    [SerializeField] AudioClip[] footstepWood;

    [Header("Body")]
    [SerializeField] AudioClip jump;
    [SerializeField] AudioClip land;

    [Header("Floorboard")]
    [SerializeField] AudioClip[] floorboardStrain;
    [SerializeField] AudioClip floorboardPop;

    [Header("Items")]
    [SerializeField] AudioClip pickup;
    [SerializeField] AudioClip keyJingle;
    [SerializeField] AudioClip keySkid;
    [SerializeField] AudioClip crateSmash;

    [Header("Axe")]
    [SerializeField] AudioClip[] axeWhoosh;
    [SerializeField] AudioClip[] axeHitWood;
    [SerializeField] AudioClip[] axeHitHard;

    [Header("Flashlight")]
    [SerializeField] AudioClip flashlightOn;
    [SerializeField] AudioClip flashlightOff;

    [Header("Doors and locks")]
    [SerializeField] AudioClip[] lockUnlock;
    [SerializeField] AudioClip lockRattle;
    [SerializeField] AudioClip barsSlide;
    [SerializeField] AudioClip[] closetCreak;

    [Header("Fire")]
    [SerializeField] AudioClip catchFire;
    [SerializeField] AudioClip fireCrackle;

    [Header("Hook game")]
    [SerializeField] AudioClip hookTick;
    [SerializeField] AudioClip hookScratch;
    [SerializeField] AudioClip hookSuccess;

    [Header("Later")]
    [Tooltip("Loud 2D stings for a future enemy. Leave empty until those clips exist.")]
    [SerializeField] AudioClip[] stingers;

    AudioSource[] voices;
    int nextVoice;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("More than one GameAudio is in the scene. Keeping the first.", this);
            return;
        }

        Instance = this;
        voices = new AudioSource[VoiceCount];
        for (int i = 0; i < voices.Length; i++)
        {
            GameObject child = new GameObject("Voice" + i);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.4f;
            source.maxDistance = 16f;
            source.spatialBlend = 1f;
            voices[i] = source;
        }
    }

    void LateUpdate()
    {
        AudioListener.volume = masterVolume;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            AudioListener.volume = 1f;
        }
    }

    void OnValidate()
    {
        masterVolume = Mathf.Clamp01(masterVolume);
        if (Application.isPlaying)
            AudioListener.volume = masterVolume;
    }

    /// <summary>
    /// A wood footstep at a world position. The player uses the clips on this
    /// component. A later enemy can pass its own set, or null to reuse these.
    /// </summary>
    public void PlayFootstep(Vector3 position, bool sprint, AudioClip[] clips = null)
    {
        AudioClip[] set = clips != null && clips.Length > 0 ? clips : footstepWood;
        Play(Pick(set), position, sprint ? 0.5f : 0.36f, 1f, 0.08f);
    }

    public void PlayJump()
    {
        Play(jump, transform.position, 0.4f, 0f, 0.04f);
    }

    public void PlayLand(Vector3 position)
    {
        Play(land, position, 0.55f, 1f, 0.05f);
    }

    public void PlayFloorboardStrain(Vector3 position)
    {
        Play(Pick(floorboardStrain), position, 0.6f, 1f, 0.06f);
    }

    public void PlayFloorboardPop(Vector3 position)
    {
        Play(floorboardPop, position, 0.75f, 1f, 0.04f);
    }

    public void PlayPickup()
    {
        Play(pickup, transform.position, 0.5f, 0f, 0.03f);
    }

    public void PlayKeyJingle(Vector3 position)
    {
        Play(keyJingle, position, 0.65f, 1f, 0.04f);
    }

    public void PlayKeySkid(Vector3 position)
    {
        Play(keySkid, position, 0.55f, 1f, 0.05f);
    }

    public void PlayCrateSmash(Vector3 position)
    {
        Play(crateSmash, position, 0.8f, 1f, 0.04f);
    }

    public void PlayAxeWhoosh()
    {
        Play(Pick(axeWhoosh), transform.position, 0.42f, 0f, 0.06f);
    }

    /// <summary>
    /// Wood is a crate or a board. Anything else is a hard thunk.
    /// </summary>
    public void PlayAxeHit(Vector3 position, bool wood)
    {
        Play(Pick(wood ? axeHitWood : axeHitHard), position, wood ? 0.7f : 0.62f, 1f, 0.05f);
    }

    public void PlayFlashlight(bool turnedOn)
    {
        Play(turnedOn ? flashlightOn : flashlightOff, transform.position, 0.45f, 0f, 0.02f);
    }

    public void PlayLockUnlock(Vector3 position)
    {
        Play(Pick(lockUnlock), position, 0.7f, 1f, 0.03f);
    }

    public void PlayLockRattle(Vector3 position)
    {
        Play(lockRattle, position, 0.55f, 1f, 0.04f);
    }

    public void PlayBarsSlide(Vector3 position)
    {
        Play(barsSlide, position, 0.62f, 1f, 0.03f);
    }

    public void PlayClosetCreak(Vector3 position)
    {
        Play(Pick(closetCreak), position, 0.6f, 1f, 0.04f);
    }

    public void PlayCatchFire()
    {
        Play(catchFire, transform.position, 0.55f, 0f, 0.04f);
    }

    /// <summary>
    /// Crackling that follows a burning item. Flat, because it is in the player's hand.
    /// Stop it with EndLoop when the flame goes out.
    /// </summary>
    public AudioSource BeginFireCrackle(Transform parent)
    {
        return BeginLoop(fireCrackle, parent, 0.32f, 0f, 1f, 8f);
    }

    public void PlayHookTick()
    {
        Play(hookTick, transform.position, 0.18f, 0f, 0.08f);
    }

    public void PlayHookScratch()
    {
        Play(hookScratch, transform.position, 0.22f, 0f, 0.06f);
    }

    public void PlayHookSuccess()
    {
        Play(hookSuccess, transform.position, 0.6f, 0f, 0.03f);
    }

    /// <summary>
    /// A 2D sting from the Stingers list. Does nothing while that list is empty.
    /// </summary>
    public void PlayStinger(int index = 0)
    {
        if (stingers == null || stingers.Length == 0)
            return;

        index = Mathf.Clamp(index, 0, stingers.Length - 1);
        Play(stingers[index], transform.position, 0.85f, 0f, 0.02f);
    }

    /// <summary>
    /// One shot. spatialBlend 1 is fully 3D. Pass 0 for a sound with no position.
    /// </summary>
    public void PlayAt(AudioClip clip, Vector3 position, float volume = 1f, float spatialBlend = 1f, float pitchJitter = 0.04f)
    {
        Play(clip, position, volume, spatialBlend, pitchJitter);
    }

    public void Play2D(AudioClip clip, float volume = 1f, float pitchJitter = 0.04f)
    {
        Play(clip, transform.position, volume, 0f, pitchJitter);
    }

    /// <summary>
    /// A looping source parented to an object. Used for the carried flame.
    /// The furnace and the room tone are AudioSources placed by the scene builder.
    /// </summary>
    public AudioSource BeginLoop(AudioClip clip, Transform parent, float volume, float spatialBlend, float minDistance, float maxDistance)
    {
        if (clip == null || parent == null)
            return null;

        GameObject go = new GameObject("Loop");
        go.transform.SetParent(parent, false);
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.volume = volume;
        source.spatialBlend = spatialBlend;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minDistance;
        source.maxDistance = Mathf.Max(minDistance + 0.1f, maxDistance);
        source.priority = 64;
        source.Play();
        return source;
    }

    public void EndLoop(AudioSource source)
    {
        if (source == null)
            return;

        source.Stop();
        Destroy(source.gameObject);
    }

    void Play(AudioClip clip, Vector3 position, float volume, float spatialBlend, float pitchJitter)
    {
        if (clip == null || voices == null || masterVolume <= 0.001f)
            return;

        AudioSource source = TakeVoice();
        source.transform.position = position;
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.spatialBlend = Mathf.Clamp01(spatialBlend);
        source.pitch = 1f + Random.Range(-Mathf.Abs(pitchJitter), Mathf.Abs(pitchJitter));
        source.Play();
    }

    AudioSource TakeVoice()
    {
        for (int i = 0; i < voices.Length; i++)
        {
            int index = (nextVoice + i) % voices.Length;
            if (!voices[index].isPlaying)
            {
                nextVoice = (index + 1) % voices.Length;
                return voices[index];
            }
        }

        AudioSource stolen = voices[nextVoice];
        nextVoice = (nextVoice + 1) % voices.Length;
        stolen.Stop();
        return stolen;
    }

    static AudioClip Pick(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;

        int start = Random.Range(0, clips.Length);
        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = clips[(start + i) % clips.Length];
            if (clip != null)
                return clip;
        }

        return null;
    }
}
