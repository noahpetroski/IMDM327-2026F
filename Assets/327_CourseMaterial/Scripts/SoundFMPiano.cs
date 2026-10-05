// Discrete, polyphonic FM notes for a separate GameObject.
// A..' = white keys; Q..[ = black keys, with gaps at E, U and P.
using System;
using System.Collections.Generic;
using UnityEngine;

public class SoundFMPiano : MonoBehaviour
{
    [Header("Notes")]
    [Range(0, 108)] public int rootMidi = 48; // C3
    [Range(0f, 1f)] public float amplitude = 0.12f;
    [Header("FM Timbre")]
    [Range(0.1f, 8f)] public float modulatorRatio = 2f;
    [Range(0f, 20f)] public float modulationIndex = 1.5f;
    [Header("Envelope (seconds)")]
    [Range(0.001f, 1f)] public float attackSeconds = 0.01f;
    [Range(0.01f, 2f)] public float releaseSeconds = 0.18f;
    public bool showKeyboard = true;

    static readonly KeyCode[] Keys =
    {
        KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.F, KeyCode.G, KeyCode.H,
        KeyCode.J, KeyCode.K, KeyCode.L, KeyCode.Semicolon, KeyCode.Quote,
        KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R, KeyCode.T, KeyCode.Y,
        KeyCode.U, KeyCode.I, KeyCode.O, KeyCode.P, KeyCode.LeftBracket
    };
    static readonly string[] KeyLabels =
        { "A", "S", "D", "F", "G", "H", "J", "K", "L", ";", "'",
          "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "[" };
    static readonly int[] WhiteNotes = { 0, 2, 4, 5, 7, 9, 11, 12, 14, 16, 17 };
    static readonly int[] BlackNotes = { 1, 3, -1, 6, 8, 10, -1, 13, 15, -1, 18 };
    static readonly string[] NoteNames =
        { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
    const double TwoPi = Math.PI * 2.0;

    struct Voice
    {
        public bool active, held;
        public double phase, modulatorPhase, frequency;
        public float envelope, releaseStep;
    }
    struct NoteEvent
    {
        public int key;
        public bool down;
        public float frequency;
    }
    struct Parameters
    {
        public float gain, ratio, index, attack, release;
    }

    // Main thread sends events; only the audio thread changes oscillator voices.
    readonly object eventLock = new object();
    readonly Queue<NoteEvent> events = new Queue<NoteEvent>(64);
    readonly Voice[] voices = new Voice[22];
    readonly bool[] heldKeys = new bool[22];
    readonly int[] heldMidi = new int[22];
    Parameters parameters;
    AudioSource source;
    int sampleRate;
    GUIStyle keyStyle;

    // Visual demos read the same notes used by the synthesizer.
    public int KeyCount => Keys.Length;
    public bool IsNoteHeld(int key) => isActiveAndEnabled && heldKeys[key];

    public int GetNoteMidi(int key)
    {
        if (heldKeys[key]) return heldMidi[key];
        int offset = key < 11 ? WhiteNotes[key] : BlackNotes[key - 11];
        return offset < 0 ? -1 : Mathf.Clamp(rootMidi + offset, 0, 127);
    }

    public string GetNoteName(int key)
    {
        int midi = GetNoteMidi(key);
        return midi < 0 ? "" : NoteNames[midi % 12] + (midi / 12 - 1);
    }

    void Awake()
    {
        sampleRate = AudioSettings.outputSampleRate;
        source = GetComponent<AudioSource>();
        if (source == null) source = gameObject.AddComponent<AudioSource>();
        source.Stop();
        source.clip = null;
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        UpdateParameters();
        // Without a clip, this filter is the source (same as SoundFM).
        // Notes reach the next DSP buffer without a streaming clip's read-ahead.
    }

    void OnEnable()
    {
        if (source != null) source.Play();
    }

    void Update()
    {
        UpdateParameters();
        for (int i = 0; i < Keys.Length; i++)
        {
            int offset = i < 11 ? WhiteNotes[i] : BlackNotes[i - 11];
            if (offset < 0) continue; // No black key between E-F or B-C.
            if (Input.GetKeyDown(Keys[i]))
            {
                int midi = Mathf.Clamp(rootMidi + offset, 0, 127);
                heldKeys[i] = true;
                heldMidi[i] = midi;
                Enqueue(i, true, 440f * Mathf.Pow(2f, (midi - 69) / 12f));
            }
            if (heldKeys[i] && Input.GetKeyUp(Keys[i]))
            {
                heldKeys[i] = false;
                Enqueue(i, false, 0f);
            }
        }
    }

    void UpdateParameters()
    {
        lock (eventLock)
            parameters = new Parameters
            {
                gain = Mathf.Clamp01(amplitude), ratio = Mathf.Max(0.1f, modulatorRatio),
                index = Mathf.Max(0f, modulationIndex), attack = Mathf.Max(0.001f, attackSeconds),
                release = Mathf.Max(0.01f, releaseSeconds)
            };
    }

    void Enqueue(int key, bool down, float frequency)
    {
        lock (eventLock)
            events.Enqueue(new NoteEvent { key = key, down = down, frequency = frequency });
    }

    void ReleaseAll()
    {
        for (int i = 0; i < heldKeys.Length; i++)
        {
            heldKeys[i] = false;
            Enqueue(i, false, 0f);
        }
    }

    void OnApplicationFocus(bool focused)
    {
        if (!focused) ReleaseAll();
    }

    void OnDisable()
    {
        ReleaseAll();
        if (source != null) source.Stop();
    }

    // Generate one mono frame and copy it to every output channel.
    void OnAudioFilterRead(float[] data, int channels)
    {
        Parameters settings;
        lock (eventLock)
        {
            settings = parameters;
            while (events.Count > 0)
            {
                NoteEvent note = events.Dequeue();
                if (note.down)
                    voices[note.key] = new Voice { active = true, held = true, frequency = note.frequency };
                else
                {
                    voices[note.key].held = false;
                    voices[note.key].releaseStep = voices[note.key].envelope / (settings.release * sampleRate);
                }
            }
        }

        float attackStep = 1f / (settings.attack * sampleRate);
        for (int sample = 0; sample < data.Length; sample += channels)
        {
            double mix = 0.0;
            for (int i = 0; i < voices.Length; i++)
            {
                if (!voices[i].active) continue;
                Voice voice = voices[i];
                voice.envelope = voice.held ? Math.Min(1f, voice.envelope + attackStep)
                    : Math.Max(0f, voice.envelope - voice.releaseStep);
                if (!voice.held && voice.envelope <= 0f) voice.active = false;

                mix += Math.Sin(voice.phase + settings.index * Math.Sin(voice.modulatorPhase))
                    * voice.envelope * settings.gain;
                voice.phase = (voice.phase + TwoPi * voice.frequency / sampleRate) % TwoPi;
                voice.modulatorPhase = (voice.modulatorPhase + TwoPi * voice.frequency
                    * settings.ratio / sampleRate) % TwoPi;
                voices[i] = voice;
            }
            // Soft limiting keeps simultaneous notes below digital clipping.
            float output = (float)Math.Tanh(mix);
            for (int channel = 0; channel < channels; channel++)
                data[sample + channel] = output;
        }
    }

    void OnGUI()
    {
        if (!showKeyboard) return;
        if (keyStyle == null)
            keyStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
        Matrix4x4 previousMatrix = GUI.matrix;
        Color previousColor = GUI.color;
        float scale = Mathf.Min(1f, Screen.width / 1000f);
        GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 980f * scale) * 0.5f, 8f, 0f),
            Quaternion.identity, Vector3.one * scale);
        GUI.Box(new Rect(0f, 0f, 980f, 166f), "06_FMPiano  |  Q row: black keys  |  A row: white keys  |  Hold keys to sustain");
        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < 11; col++)
            {
                int i = (1 - row) * 11 + col;
                int offset = row == 0 ? BlackNotes[col] : WhiteNotes[col];
                if (offset < 0) continue;
                int midi = heldKeys[i] ? heldMidi[i] : Mathf.Clamp(rootMidi + offset, 0, 127);
                Rect rect = new Rect(18f + col * 86f, 30f + row * 64f, 82f, 58f);
                GUI.color = heldKeys[i] ? new Color(0.35f, 1f, 0.75f)
                    : row == 0 ? new Color(0.12f, 0.12f, 0.12f) : Color.white;
                GUI.DrawTexture(rect, Texture2D.whiteTexture);
                GUI.color = Color.white;
                keyStyle.normal.textColor = row == 0 && !heldKeys[i] ? Color.white : Color.black;
                string note = NoteNames[midi % 12] + (midi / 12 - 1);
                GUI.Label(rect, KeyLabels[i] + "\n" + note, keyStyle);
            }
        }
        GUI.matrix = previousMatrix;
        GUI.color = previousColor;
    }
}
