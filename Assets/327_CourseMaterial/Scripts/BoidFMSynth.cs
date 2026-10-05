using System;
using UnityEngine;

public class BoidFMSynth : MonoBehaviour
{
    public bool followMotion = true;
    public float carrierFrequency = 220f;
    public float modulatorFrequency = 440f;
    [Range(0f, 20f)] public float modulationIndex = 1f;
    [Range(0f, 1f)] public float amplitude = 0.2f;
    [Range(-1f, 1f)] public float pan;

    struct Parameters
    {
        public float frequency, modulatorFrequency, index, gain, pan;
    }
    readonly object parameterLock = new object();
    Parameters parameters, target;
    float frequency = 220f, modulator = 440f, index, gain, stereo;
    double carrierPhase, modulatorPhase;
    const double TwoPi = Math.PI * 2.0;

    public void SetPhase(float phase)
    {
        carrierPhase = phase;
        modulatorPhase = phase * 1.37;
    }

    public void UpdateParameters()
    {
        // FixedUpdate sends values; the audio thread reads them once per buffer.
        lock (parameterLock)
        {
            parameters.frequency = Mathf.Clamp(carrierFrequency, 20f, 5000f);
            parameters.modulatorFrequency = Mathf.Clamp(modulatorFrequency, 0f, 10000f);
            parameters.index = Mathf.Clamp(modulationIndex, 0f, 20f);
            parameters.gain = isActiveAndEnabled ? Mathf.Clamp01(amplitude) : 0f;
            parameters.pan = Mathf.Clamp(this.pan, -1f, 1f);
        }
    }

    public void ReadParameters()
    {
        lock (parameterLock) target = parameters;
    }

    // Called by FlockingSynth's audio thread, one sample for this boid.
    public void Render(int sampleRate, float smoothing, out float left, out float right)
    {
        frequency += (target.frequency - frequency) * smoothing;
        modulator += (target.modulatorFrequency - modulator) * smoothing;
        index += (target.index - index) * smoothing;
        gain += (target.gain - gain) * smoothing;
        stereo += (target.pan - stereo) * smoothing;
        float sample = gain * FM(carrierPhase, modulatorPhase, index);
        left = sample * (float)Math.Sqrt(0.5f * (1f - stereo));
        right = sample * (float)Math.Sqrt(0.5f * (1f + stereo));
        carrierPhase = (carrierPhase + TwoPi * Math.Min(frequency, sampleRate * 0.45) / sampleRate) % TwoPi;
        modulatorPhase = (modulatorPhase + TwoPi * Math.Min(modulator, sampleRate * 0.45) / sampleRate) % TwoPi;
    }

    public float FM(double carrierPhase, double modulatorPhase, float index)
    {
        double modulator = Math.Sin(modulatorPhase);
        return (float)Math.Sin(carrierPhase + index * modulator);
    }
}
