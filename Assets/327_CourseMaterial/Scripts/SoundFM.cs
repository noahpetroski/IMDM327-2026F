// Unity Audio Synthesis (Sine + FM + Envelope)
// IMDM 327 Class Material
// Revised educational version
// Author: Myungin Lee

using UnityEngine;
using System.Threading;

public class SoundFM : MonoBehaviour
{
    AudioSource audioSource;

    [Header("FM Synthesis")]

    [Range(20f, 2000f)]
    public float carrierFrequency = 440f;

    [Range(0f, 2000f)]
    public float modulatorFrequency = 220f;

    [Range(0f, 20f)]
    public float modulationIndex = 0f;

    [Range(0f, 1f)]
    public float amplitude = 0.5f;

    [Header("Envelope")]

    [Range(100f, 5000f)]
    public float envelopeDuration = 1000f;

    public float sampleRate = 44100f;

    // Separate oscillator phases make the FM structure easier to understand.
    float carrierPhase = 0f;
    float modulatorPhase = 0f;

    // Counts generated samples since the most recent trigger.
    float timeIdx = 0f;

    // Only the audio thread changes playback state and oscillator phases.
    bool isPlaying = false;
    int triggerRequested = 0;
    float currentEnvelope = 0f;
    float retriggerEnvelope = 0f;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        // Synthesis and spectrum frequency labels must use the same device rate.
        sampleRate = AudioSettings.outputSampleRate;

        // OnAudioFilterRead generates the sound.
        // Keep the source running; output silence between notes.
        audioSource.playOnAwake = false;
        audioSource.loop = true;
    }

    void OnEnable()
    {
        audioSource.Play();
    }

    void OnDisable()
    {
        audioSource.Stop();
    }

    void Update()
    {
        // Trigger a new synthesized sound.
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TriggerSound();
        }
    }

    void TriggerSound()
    {
        // Pass the request safely to the next audio buffer.
        Interlocked.Exchange(ref triggerRequested, 1);
    }

    // Unity calls this function on the audio thread.
    // Fill the "data" array with audio samples.
    void OnAudioFilterRead(float[] data, int channels)
    {
        if (Interlocked.Exchange(ref triggerRequested, 0) == 1)
        {
            // Preserve phase and blend from the current envelope over 5 ms.
            retriggerEnvelope = currentEnvelope;
            timeIdx = 0f;
            isPlaying = true;
        }

        if (!isPlaying)
        {
            System.Array.Clear(data, 0, data.Length);
            return;
        }

        float blendSamples = sampleRate * 0.005f;

        for (int i = 0; i < data.Length; i += channels)
        {
            // Advance the two oscillators independently.
            carrierPhase += 2f * Mathf.PI * carrierFrequency / sampleRate;
            modulatorPhase += 2f * Mathf.PI * modulatorFrequency / sampleRate;

            // TRUE FM / phase modulation form:
            //
            // output = sin(
            //     carrierPhase
            //     + modulationIndex * sin(modulatorPhase)
            // )
            //
            // modulationIndex = 0  -> pure sine wave
            // modulationIndex up   -> richer / more complex spectrum
            currentEnvelope = Mathf.Lerp(retriggerEnvelope,
                Envelope(timeIdx, envelopeDuration), Mathf.Clamp01(timeIdx / blendSamples));

            float sample =
                amplitude
                * FM(carrierPhase, modulatorPhase, modulationIndex)
                * currentEnvelope;

            // Write the same mono signal to every output channel.
            // For XR, this can later be used as a mono source and spatialized
            // by Steam Audio or another spatializer.
            for (int channel = 0; channel < channels; channel++)
            {
                data[i + channel] = sample;
            }

            WrapPhase(ref carrierPhase);
            WrapPhase(ref modulatorPhase);

            timeIdx++;
        }

        if (timeIdx > blendSamples && currentEnvelope < 0.00001f)
        {
            currentEnvelope = 0f;
            isPlaying = false;
        }
    }

    // Basic sine oscillator, useful for comparison in class.
    public float SinWave(float phase)
    {
        return Mathf.Sin(phase);
    }

    // Frequency Modulation / phase modulation synthesis.
    public float FM(float carrierPhase, float modulatorPhase, float index)
    {
        float modulator = Mathf.Sin(modulatorPhase);

        return Mathf.Sin(
            carrierPhase + index * modulator
        );
    }

    // Simple attack-decay style exponential envelope.
    public float Envelope(float timeIdx, float duration)
    {
        float a = 0.13f;
        float b = 0.45f;

        return Mathf.Abs(
            Mathf.Exp(-a * timeIdx / duration)
            - Mathf.Exp(-b * timeIdx / duration)
        );
    }

    void WrapPhase(ref float phase)
    {
        if (phase >= 2f * Mathf.PI)
        {
            phase -= 2f * Mathf.PI;
        }
    }
}
