using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("--- Audio Sources ---")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("--- Weapon Sound Clips ---")]
    [Tooltip("Finger UP hone par knife shoot ka sound clip")]
    [SerializeField] private AudioClip knifeShootSound;

    [Tooltip("Knife spawn hote waqt ka halka swoosh/click sound (Optional)")]
    [SerializeField] private AudioClip knifeSpawnSound;

    [Tooltip("Knife dushman/obstacle se takrane ka Hit sound")]
    [SerializeField] private AudioClip knifeHitSound;

    [Header("--- UI & Game Sound Clips ---")]
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip levelWinSound;
    [SerializeField] private AudioClip levelFailSound;

    [Header("--- Audio Settings ---")]
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 0.6f;
    public bool isSfxMuted = false;
    public bool isMusicMuted = false;

    private void Awake()
    {
        // 1. DontDestroyOnLoad Singleton Pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioSources();
            LoadAudioSettings();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeAudioSources()
    {
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
        }
    }

    #region Specific Game Audio Triggers

    /// <summary>
    /// Finger UP hone par call hoga: Knife Shoot Sound
    /// </summary>
    public void PlayShootSound()
    {
        PlaySFX(knifeShootSound, 0.95f, 1.05f); // Pitch variation for game feel
    }

    /// <summary>
    /// Knife create hote waqt ka sound
    /// </summary>
    public void PlaySpawnSound()
    {
        PlaySFX(knifeSpawnSound, 0.9f, 1.15f);
    }

    /// <summary>
    /// Knife impact / hit sound
    /// </summary>
    public void PlayHitSound()
    {
        PlaySFX(knifeHitSound, 0.9f, 1.1f);
    }

    public void PlayButtonClick()
    {
        PlaySFX(buttonClickSound);
    }

    public void PlayLevelWin()
    {
        PlaySFX(levelWinSound);
    }

    public void PlayLevelFail()
    {
        PlaySFX(levelFailSound);
    }

    #endregion

    #region Core Audio Methods (SFX & Music)

    /// <summary>
    /// Kisi bhi generic sound ko play karne ke liye (with pitch randomization)
    /// </summary>
    public void PlaySFX(AudioClip clip, float minPitch = 1f, float maxPitch = 1f)
    {
        if (clip == null || isSfxMuted || sfxSource == null) return;

        sfxSource.pitch = Random.Range(minPitch, maxPitch);
        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    /// <summary>
    /// Background music chalane ke liye
    /// </summary>
    public void PlayMusic(AudioClip musicClip)
    {
        if (musicClip == null || musicSource == null) return;

        musicSource.clip = musicClip;
        musicSource.volume = musicVolume;
        musicSource.mute = isMusicMuted;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource != null) musicSource.Stop();
    }

    #endregion

    #region Settings & Volume Control (UI Ready)

    public void ToggleSFX(bool state)
    {
        isSfxMuted = !state;
        PlayerPrefs.SetInt("SFX_Muted", isSfxMuted ? 1 : 0);
    }

    public void ToggleMusic(bool state)
    {
        isMusicMuted = !state;
        if (musicSource != null) musicSource.mute = isMusicMuted;
        PlayerPrefs.SetInt("Music_Muted", isMusicMuted ? 1 : 0);
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat("SFX_Volume", sfxVolume);
    }

    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        if (musicSource != null) musicSource.volume = musicVolume;
        PlayerPrefs.SetFloat("Music_Volume", musicVolume);
    }

    private void LoadAudioSettings()
    {
        isSfxMuted = PlayerPrefs.GetInt("SFX_Muted", 0) == 1;
        isMusicMuted = PlayerPrefs.GetInt("Music_Muted", 0) == 1;
        sfxVolume = PlayerPrefs.GetFloat("SFX_Volume", 1f);
        musicVolume = PlayerPrefs.GetFloat("Music_Volume", 0.6f);

        if (musicSource != null)
        {
            musicSource.volume = musicVolume;
            musicSource.mute = isMusicMuted;
        }
    }

    #endregion
}