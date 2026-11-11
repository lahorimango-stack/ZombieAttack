using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

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
    [Tooltip("Zombie marne par bhaari 'thud' (hit ke upar layer - kill ka wazan)")]
    [SerializeField] private AudioClip[] killImpactSounds;

    [Header("--- UI & Game Sound Clips ---")]
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip levelWinSound;
    [SerializeField] private AudioClip levelFailSound;
    [SerializeField] private AudioClip coinSound;
    [SerializeField] private AudioClip panelOpenSound;
    [SerializeField] private AudioClip waveStartSound;
    [SerializeField] private AudioClip finalWaveSound;
    [SerializeField] private AudioClip unlockSound;

    [Header("--- Music & Ambience ---")]
    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioClip gameplayMusic;
    [SerializeField] private AudioClip bossMusic;
    [SerializeField] private float musicFadeTime = 0.8f;
    [Tooltip("Hamesha chalne wala mahaul (baarish) - music ke neeche")]
    [SerializeField] private AudioSource ambienceSource;
    [SerializeField] private AudioClip ambienceClip;
    [Range(0f, 1f)][SerializeField] private float ambienceVolume = 0.35f;

    [Header("--- Audio Settings ---")]
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 0.6f;
    public bool isSfxMuted = false;
    public bool isMusicMuted = false;

    [Tooltip("Ek hi sound group ko is se jaldi dobara na bajaye (spam control)")]
    [SerializeField] private float sameSoundMinGap = 0.045f;

    private readonly Dictionary<int, float> lastPlayTime = new Dictionary<int, float>();
    private Tween musicFade;

    private void Awake()
    {
        // 1. DontDestroyOnLoad Singleton Pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioSources();
            LoadAudioSettings();
            StartAmbience();
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
        PlaySFX(knifeSpawnSound, 0.9f, 1.15f, 0.5f);
    }

    private AudioSource spawnSource;
    private float nextSpawnSoundTime;

    /// <summary>
    /// Weapon draw hote hi uski apni awaaz. index = formation mein kaunsa weapon (pitch thoda thoda upar - build-up feel).
    /// Alag AudioSource taake hit/throw sounds ka pitch na badle.
    /// </summary>
    public void PlayWeaponSpawn(AudioClip[] clips, int index, float basePitch, float volume, AudioClip[] firstClips = null)
    {
        if (isSfxMuted) return;
        // Pehla weapon: talwar nikalne jaisa 'shing' (poora), baaki: chhoti metallic clicks jo pitch mein charhti hain
        bool first = index == 0 && firstClips != null && firstClips.Length > 0;
        AudioClip[] pool = first ? firstClips : clips;
        AudioClip clip = pool != null && pool.Length > 0 ? pool[Random.Range(0, pool.Length)] : knifeSpawnSound;
        if (clip == null) return;
        if (Time.unscaledTime < nextSpawnSoundTime) return; // tez swipe mein ek frame ke 10 weapons = 1 awaaz
        nextSpawnSoundTime = Time.unscaledTime + 0.035f;

        if (spawnSource == null)
        {
            spawnSource = gameObject.AddComponent<AudioSource>();
            spawnSource.playOnAwake = false;
            spawnSource.spatialBlend = 0f;
        }
        if (first)
        {
            spawnSource.pitch = basePitch * Random.Range(0.97f, 1.03f);
            spawnSource.PlayOneShot(clip, sfxVolume * Mathf.Min(1f, volume * 1.3f));
            return;
        }
        spawnSource.pitch = basePitch * (1f + Mathf.Min(index, 12) * 0.045f) * Random.Range(0.98f, 1.02f);
        spawnSource.PlayOneShot(clip, sfxVolume * volume);
    }

    /// <summary>
    /// Knife impact / hit sound
    /// </summary>
    public void PlayHitSound()
    {
        PlaySFX(knifeHitSound, 0.9f, 1.1f);
    }

    /// <summary> Kill par bhaari thud (bohat saare ek saath maren to bhi ek) </summary>
    public void PlayKillImpact()
    {
        PlayRandomSFX(killImpactSounds, 0.85f, 1.05f, 0.75f);
    }

    public void PlayButtonClick()
    {
        PlaySFX(buttonClickSound);
    }

    public void PlayPanelOpen()
    {
        PlaySFX(panelOpenSound);
    }

    public void PlayCoin()
    {
        PlaySFX(coinSound, 0.95f, 1.15f, 0.6f);
    }

    public void PlayUnlock()
    {
        PlaySFX(unlockSound);
    }

    public void PlayWaveStart(bool isFinalWave)
    {
        PlaySFX(isFinalWave && finalWaveSound != null ? finalWaveSound : waveStartSound);
    }

    public void PlayLevelWin()
    {
        FadeMusicTo(0.25f);
        PlaySFX(levelWinSound);
    }

    public void PlayLevelFail()
    {
        FadeMusicTo(0.25f);
        PlaySFX(levelFailSound);
    }

    public void PlayMenuMusic() => SwitchMusic(menuMusic);
    public void PlayGameplayMusic() => SwitchMusic(gameplayMusic);
    public void PlayBossMusic() => SwitchMusic(bossMusic != null ? bossMusic : gameplayMusic);

    #endregion

    #region Core Audio Methods (SFX & Music)

    /// <summary>
    /// Kisi bhi generic sound ko play karne ke liye (with pitch randomization)
    /// </summary>
    public void PlaySFX(AudioClip clip, float minPitch = 1f, float maxPitch = 1f, float volumeScale = 1f)
    {
        if (clip == null || isSfxMuted || sfxSource == null) return;
        if (IsThrottled(clip.GetHashCode())) return;

        sfxSource.pitch = Random.Range(minPitch, maxPitch);
        sfxSource.PlayOneShot(clip, sfxVolume * volumeScale);
    }

    /// <summary>
    /// Clips mein se random ek bajaye (multiple hits ek frame mein hon to sirf ek baje)
    /// </summary>
    public void PlayRandomSFX(AudioClip[] clips, float minPitch = 1f, float maxPitch = 1f, float volumeScale = 1f)
    {
        if (clips == null || clips.Length == 0 || isSfxMuted || sfxSource == null) return;
        if (IsThrottled(clips.GetHashCode())) return;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;

        sfxSource.pitch = Random.Range(minPitch, maxPitch);
        sfxSource.PlayOneShot(clip, sfxVolume * volumeScale);
    }

    private bool IsThrottled(int key)
    {
        float now = Time.unscaledTime;
        if (lastPlayTime.TryGetValue(key, out float last) && now - last < sameSoundMinGap) return true;
        lastPlayTime[key] = now;
        return false;
    }

    /// <summary>
    /// Background music chalane ke liye
    /// </summary>
    public void PlayMusic(AudioClip musicClip)
    {
        if (musicClip == null || musicSource == null) return;

        musicFade?.Kill();
        musicSource.clip = musicClip;
        musicSource.volume = musicVolume;
        musicSource.mute = isMusicMuted;
        musicSource.Play();
    }

    /// <summary>
    /// Smooth crossfade (same clip ho to sirf volume wapis laaye)
    /// </summary>
    private void SwitchMusic(AudioClip clip)
    {
        if (clip == null || musicSource == null) return;

        if (musicSource.clip == clip && musicSource.isPlaying)
        {
            FadeMusicTo(1f);
            return;
        }

        musicFade?.Kill();
        musicFade = musicSource.DOFade(0f, musicFadeTime * 0.5f).SetUpdate(true).OnComplete(() =>
        {
            musicSource.clip = clip;
            musicSource.mute = isMusicMuted;
            musicSource.Play();
            musicFade = musicSource.DOFade(musicVolume, musicFadeTime).SetUpdate(true);
        });
    }

    private void FadeMusicTo(float volumeFactor)
    {
        if (musicSource == null) return;
        musicFade?.Kill();
        musicFade = musicSource.DOFade(musicVolume * volumeFactor, musicFadeTime).SetUpdate(true);
    }

    public void StopMusic()
    {
        if (musicSource != null) musicSource.Stop();
    }

    private void StartAmbience()
    {
        if (ambienceSource == null || ambienceClip == null) return;
        ambienceSource.clip = ambienceClip;
        ambienceSource.loop = true;
        ambienceSource.volume = ambienceVolume * musicVolume;
        ambienceSource.mute = isMusicMuted;
        ambienceSource.Play();
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
        if (ambienceSource != null) ambienceSource.mute = isMusicMuted;
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
