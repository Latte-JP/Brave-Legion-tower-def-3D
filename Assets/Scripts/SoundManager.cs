using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("カスタムBGM（設定があれば優先再生）")]
    [SerializeField] private AudioClip customBattleBgm;

    public enum SoundType
    {
        Spawn,          // 出撃音
        MeleeHit,       // 近接ヒット音
        ShootArrow,     // 弓射撃音
        TowerShoot,     // タワー砲撃音
        MeteorCast,     // メテオ落下音
        MeteorExplosion,// メテオ着弾爆発音
        UnitDeath,      // 撃破音
        Victory,        // 勝利ファンファーレ
        Defeat          // 敗北ジングル
    }

    private AudioSource seSource;
    private AudioSource bgmSource;
    private BgmSynthEmitter bgmEmitter;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            // AudioListenerがカメラに無ければ自動付加
            if (FindFirstObjectByType<AudioListener>() == null)
            {
                Camera cam = Camera.main;
                if (cam != null && cam.GetComponent<AudioListener>() == null)
                {
                    cam.gameObject.AddComponent<AudioListener>();
                }
            }

            // SE用 AudioSource
            seSource = gameObject.AddComponent<AudioSource>();
            seSource.playOnAwake = false;
            seSource.spatialBlend = 0f;

            // BGM用の子オブジェクトを生成して分離
            GameObject bgmChild = new GameObject("BgmPlayer");
            bgmChild.transform.SetParent(transform, false);

            bgmSource = bgmChild.AddComponent<AudioSource>();
            bgmSource.playOnAwake = false;
            bgmSource.loop = true;
            bgmSource.spatialBlend = 0f;
            bgmSource.volume = 0.45f;

            bgmEmitter = bgmChild.AddComponent<BgmSynthEmitter>();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void PlayBattleBGM()
    {
        if (customBattleBgm != null)
        {
            if (bgmSource != null)
            {
                bgmSource.clip = customBattleBgm;
                bgmSource.Play();
            }
        }
        else
        {
            if (bgmEmitter != null)
            {
                bgmEmitter.StartBgm();
            }
        }
    }

    public void StopBGM()
    {
        if (bgmEmitter != null)
        {
            bgmEmitter.StopBgm();
        }
        if (bgmSource != null && bgmSource.isPlaying)
        {
            bgmSource.Stop();
        }
    }

    public void Play(SoundType type, float volumeScale = 1.0f)
    {
        if (seSource == null) return;
        AudioClip clip = GenerateClip(type);
        if (clip != null)
        {
            seSource.PlayOneShot(clip, volumeScale);
        }
    }

    private AudioClip GenerateClip(SoundType type)
    {
        int sRate = 44100;
        float duration = 0.15f;
        float[] samples;

        switch (type)
        {
            case SoundType.Spawn:
                duration = 0.12f;
                samples = new float[(int)(sRate * duration)];
                for (int i = 0; i < samples.Length; i++)
                {
                    float t = (float)i / sRate;
                    float freq = Mathf.Lerp(300f, 600f, t / duration);
                    samples[i] = Mathf.Sin(2 * Mathf.PI * freq * t) * (1f - t / duration) * 0.4f;
                }
                break;

            case SoundType.ShootArrow:
                duration = 0.1f;
                samples = new float[(int)(sRate * duration)];
                for (int i = 0; i < samples.Length; i++)
                {
                    float t = (float)i / sRate;
                    float noise = (Random.value * 2f - 1f) * (1f - t / duration);
                    samples[i] = noise * 0.3f;
                }
                break;

            case SoundType.MeleeHit:
                duration = 0.1f;
                samples = new float[(int)(sRate * duration)];
                for (int i = 0; i < samples.Length; i++)
                {
                    float t = (float)i / sRate;
                    float freq = Mathf.Lerp(180f, 60f, t / duration);
                    float wave = Mathf.Sin(2 * Mathf.PI * freq * t);
                    float noise = (Random.value * 2f - 1f) * 0.3f;
                    samples[i] = (wave + noise) * (1f - t / duration) * 0.5f;
                }
                break;

            case SoundType.TowerShoot:
                duration = 0.25f;
                samples = new float[(int)(sRate * duration)];
                for (int i = 0; i < samples.Length; i++)
                {
                    float t = (float)i / sRate;
                    float freq = Mathf.Lerp(220f, 70f, t / duration);
                    samples[i] = Mathf.Sin(2 * Mathf.PI * freq * t) * (1f - t / duration) * 0.6f;
                }
                break;

            case SoundType.UnitDeath:
                duration = 0.2f;
                samples = new float[(int)(sRate * duration)];
                for (int i = 0; i < samples.Length; i++)
                {
                    float t = (float)i / sRate;
                    float noise = (Random.value * 2f - 1f) * (1f - t / duration);
                    samples[i] = noise * 0.45f;
                }
                break;

            case SoundType.MeteorCast:
                duration = 0.45f;
                samples = new float[(int)(sRate * duration)];
                for (int i = 0; i < samples.Length; i++)
                {
                    float t = (float)i / sRate;
                    float freq = Mathf.Lerp(800f, 150f, t / duration);
                    samples[i] = Mathf.Sin(2 * Mathf.PI * freq * t) * (1f - t / duration) * 0.5f;
                }
                break;

            case SoundType.MeteorExplosion:
                duration = 0.6f;
                samples = new float[(int)(sRate * duration)];
                for (int i = 0; i < samples.Length; i++)
                {
                    float t = (float)i / sRate;
                    float noise = (Random.value * 2f - 1f);
                    float lowFreq = Mathf.Sin(2 * Mathf.PI * 55f * t);
                    samples[i] = (noise * 0.6f + lowFreq * 0.4f) * (1f - t / duration) * 0.8f;
                }
                break;

            case SoundType.Victory:
                duration = 0.8f;
                samples = new float[(int)(sRate * duration)];
                for (int i = 0; i < samples.Length; i++)
                {
                    float t = (float)i / sRate;
                    float freq = (t < 0.2f) ? 523.25f : (t < 0.4f) ? 659.25f : 783.99f;
                    samples[i] = Mathf.Sin(2 * Mathf.PI * freq * t) * (1f - t / duration) * 0.5f;
                }
                break;

            case SoundType.Defeat:
                duration = 0.9f;
                samples = new float[(int)(sRate * duration)];
                for (int i = 0; i < samples.Length; i++)
                {
                    float t = (float)i / sRate;
                    float freq = (t < 0.3f) ? 392f : (t < 0.6f) ? 349.23f : 311.13f;
                    samples[i] = Mathf.Sin(2 * Mathf.PI * freq * t) * (1f - t / duration) * 0.5f;
                }
                break;

            default:
                return null;
        }

        AudioClip clip = AudioClip.Create($"Procedural_{type}", samples.Length, 1, sRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}

/// <summary>
/// BGM専用リアルタイムDSPシンセ（完全スレッドセーフ版）
/// </summary>
public class BgmSynthEmitter : MonoBehaviour
{
    private bool isPlaying = false;
    private float bgmTime = 0f;
    private double sampleRate = 44100.0;
    private const float Bpm = 130f;

    // スレッドセーフな高速乱数シード (XORShift)
    private uint randState = 2463534242;

    private readonly float[] bassFreqs = { 110.0f, 87.31f, 130.81f, 98.0f };
    private readonly float[] arpScale = { 220f, 261.63f, 329.63f, 392.0f, 440f, 523.25f, 659.25f, 783.99f };

    private void Awake()
    {
        sampleRate = AudioSettings.outputSampleRate;
    }

    public void StartBgm()
    {
        bgmTime = 0f;
        isPlaying = true;
    }

    public void StopBgm()
    {
        isPlaying = false;
    }

    // オーディオスレッド内で安全に実行可能な高速疑似ノイズ生成
    private float NextNoise()
    {
        randState ^= randState << 13;
        randState ^= randState >> 17;
        randState ^= randState << 5;
        return (float)(randState & 0x7FFFFFFF) / 1073741824.0f - 1.0f;
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (!isPlaying) return;

        float secondsPerBeat = 60f / Bpm;
        float secondsPerStep = secondsPerBeat / 2f;
        float totalLoopTime = secondsPerStep * 32f;

        for (int i = 0; i < data.Length; i += channels)
        {
            bgmTime += 1.0f / (float)sampleRate;
            float loopPos = bgmTime % totalLoopTime;

            int step = Mathf.FloorToInt(loopPos / secondsPerStep) % 32;
            float stepTime = loopPos % secondsPerStep;
            float stepFrac = stepTime / secondsPerStep;

            // 1. キックドラム
            float kick = 0f;
            if (step % 2 == 0)
            {
                float kFreq = Mathf.Lerp(150f, 40f, stepFrac);
                kick = Mathf.Sin(2f * Mathf.PI * kFreq * stepTime) * Mathf.Exp(-stepFrac * 6f) * 0.7f;
            }

            // 2. ハイハット（スレッド安全なノイズ）
            float hat = 0f;
            if (step % 2 == 1)
            {
                hat = NextNoise() * Mathf.Exp(-stepFrac * 10f) * 0.15f;
            }

            // 3. シンセベース
            int chordIdx = (step / 8) % bassFreqs.Length;
            float bFreq = bassFreqs[chordIdx];
            if (step % 2 == 1) bFreq *= 1.5f;
            float bass = Mathf.Sin(2f * Mathf.PI * bFreq * bgmTime) * Mathf.Exp(-stepFrac * 4f) * 0.4f;

            // 4. アルペジオ
            int arpIdx = (step * 3) % arpScale.Length;
            float aFreq = arpScale[arpIdx];
            float arp = Mathf.Sin(2f * Mathf.PI * aFreq * bgmTime) * Mathf.Exp(-stepFrac * 3f) * 0.2f;

            float mixed = Mathf.Clamp(kick + hat + bass + arp, -1f, 1f) * 0.35f;

            for (int ch = 0; ch < channels; ch++)
            {
                data[i + ch] += mixed;
            }
        }
    }
}