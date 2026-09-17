// Proceduralni zvukovi (bez audio fajlova): klik selekcije, hvatanje/pustanje
// makete, tihi ambijent. Kaci se na MaketaV2.
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class SfxManager : MonoBehaviour
{
    public static SfxManager I;
    AudioSource src;
    AudioClip clickClip, grabClip, releaseClip, hoverClip, bellClip, stepsClip;
    AudioSource music;
    bool riding;
    // Background music should sit behind the interaction sounds and narration.
    float musicVolume = 0.12f;

    public void SetRiding(bool value) { riding = value; }

    void Update()
    {
        if (music != null) music.volume = Mathf.MoveTowards(music.volume, riding ? musicVolume * 0.3f : musicVolume, Time.deltaTime * 0.2f);
    }

    void Awake()
    {
        I = this;
        src = gameObject.AddComponent<AudioSource>();
        src.spatialBlend = 0.6f;
        clickClip = Tone("click", 1250f, 0.06f, 0.5f);
        grabClip = Tone("grab", 240f, 0.09f, 0.6f);
        releaseClip = Tone("release", 170f, 0.08f, 0.45f);
        hoverClip = Tone("hover", 1900f, 0.028f, 0.16f);
        bellClip = Bell();
        stepsClip = Steps();

        // muzika: ako postoji fajl u StreamingAssets/music koristi njega,
        // inace generisani pad
        var amb = gameObject.AddComponent<AudioSource>();
        music = amb;
        amb.clip = Ambient();
        amb.loop = true;
        amb.volume = musicVolume;
        amb.spatialBlend = 0f;
        amb.Play();
        StartCoroutine(TryCustomMusic(amb));

        var grab = GetComponent<XRGrabInteractable>();
        if (grab != null)
        {
            grab.selectEntered.AddListener(_ => Play(grabClip));
            grab.selectExited.AddListener(_ => Play(releaseClip));
        }
    }

    public void Click() => Play(clickClip);
    public void Hover() { if (hoverClip != null) src.PlayOneShot(hoverClip, 0.5f); }

    // zvuk selekcije zavisi od sporta: zvonce za bicikl, koraci za trcanje
    public void PlaySelect(string sport)
    {
        if (sport == "Ride" && bellClip != null) Play(bellClip);
        else if (sport == "Run" && stepsClip != null) Play(stepsClip);
        else Play(clickClip);
    }

    void Play(AudioClip c) { if (c != null) src.PlayOneShot(c); }

    static AudioClip Bell()
    {
        // dva metalna "cin-cin" udara biciklistickog zvonceta
        int sr = 44100; float dur = 0.85f; int n = (int)(sr * dur);
        var data = new float[n];
        void Ding(float start, float amp)
        {
            int s0 = (int)(start * sr);
            for (int i = s0; i < n; i++)
            {
                float t = (i - s0) / (float)sr;
                float env = Mathf.Exp(-t * 9f);
                data[i] += (Mathf.Sin(2 * Mathf.PI * 2093f * t)
                          + 0.6f * Mathf.Sin(2 * Mathf.PI * 3136f * t)
                          + 0.3f * Mathf.Sin(2 * Mathf.PI * 4699f * t)) * env * amp;
            }
        }
        Ding(0f, 0.22f);
        Ding(0.17f, 0.20f);
        var clip = AudioClip.Create("bell", n, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip Steps()
    {
        // sest brzih koraka trcanja (filtrirani sum, naizmenicno levi/desni)
        int sr = 44100; float dur = 1.0f; int n = (int)(sr * dur);
        var data = new float[n];
        var rnd = new System.Random(3);
        for (int step = 0; step < 6; step++)
        {
            int s0 = (int)(step * 0.155f * sr);
            float lp = 0, cutoff = step % 2 == 0 ? 0.10f : 0.14f;
            for (int i = 0; i < (int)(0.07f * sr) && s0 + i < n; i++)
            {
                float t = i / (float)sr;
                float w = (float)(rnd.NextDouble() * 2 - 1);
                lp = Mathf.Lerp(lp, w, cutoff);
                data[s0 + i] += lp * Mathf.Exp(-t * 55f) * 0.9f;
            }
        }
        var clip = AudioClip.Create("steps", n, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }

    System.Collections.IEnumerator TryCustomMusic(AudioSource amb)
    {
        yield return RuntimeData.Prepare();
        var dir = System.IO.Path.Combine(RuntimeData.Root, "music");
        if (!System.IO.Directory.Exists(dir)) yield break;
        string file = null;
        foreach (var f in System.IO.Directory.GetFiles(dir))
            if (f.EndsWith(".mp3") || f.EndsWith(".ogg") || f.EndsWith(".wav")) { file = f; break; }
        if (file == null) yield break;
        var type = file.EndsWith(".mp3") ? AudioType.MPEG
                 : file.EndsWith(".ogg") ? AudioType.OGGVORBIS : AudioType.WAV;
        using (var req = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip(
            "file:///" + file.Replace('\\', '/'), type))
        {
            yield return req.SendWebRequest();
            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                amb.clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(req);
                amb.volume = musicVolume;
                amb.Play();
                Debug.Log("[MojaSrbija] Custom muzika: " + System.IO.Path.GetFileName(file));
            }
        }
    }

    static AudioClip Tone(string name, float freq, float dur, float amp)
    {
        int sr = 44100, n = (int)(sr * dur);
        var data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float env = Mathf.Exp(-t * 22f);
            data[i] = Mathf.Sin(2 * Mathf.PI * freq * t) * env * amp
                    + Mathf.Sin(2 * Mathf.PI * freq * 2.01f * t) * env * amp * 0.25f;
        }
        var clip = AudioClip.Create(name, n, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip Ambient()
    {
        // mekan pad-akord (A2 + E3 + A3 + C#4) sa sporim talasanjem; duzina = ceo broj
        // perioda LFO-a pa se petlja glatko
        int sr = 22050, n = sr * 8;
        var data = new float[n];
        float[] freqs = { 220f, 329.6f, 440f, 554.4f };   // vise oktave: cuje se i na slabim zvucnicima
        float[] amps = { 0.28f, 0.20f, 0.14f, 0.08f };
        var rnd = new System.Random(7);
        float lp = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sr;
            float s = 0;
            for (int k = 0; k < freqs.Length; k++)
            {
                float trem = 0.75f + 0.25f * Mathf.Sin(2 * Mathf.PI * (0.125f * (k + 1)) * t + k);
                s += Mathf.Sin(2 * Mathf.PI * freqs[k] * t) * amps[k] * trem;
            }
            float w = (float)(rnd.NextDouble() * 2 - 1);
            lp = Mathf.Lerp(lp, w, 0.015f);
            data[i] = s * 0.5f + lp * 0.08f;
        }
        var clip = AudioClip.Create("ambient", n, 1, sr, false);
        clip.SetData(data, 0);
        return clip;
    }
}
