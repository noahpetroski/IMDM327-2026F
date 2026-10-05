// Adapted from IMDM290-2026F/Assets/Scripts/Audio/AudioSpectrum.cs.
// Analyze this object's AudioSource (audio clip or synthesized sound).
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioSpectrum : MonoBehaviour
{
    public const int FFTSIZE = 4096;
    public float[] Samples { get; } = new float[FFTSIZE];
    public float HzPerBin => AudioSettings.outputSampleRate * 0.5f / FFTSIZE;

    AudioSource source;

    void Start()
    {
        source = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (source != null && source.isPlaying)
            source.GetSpectrumData(Samples, 0, FFTWindow.Hanning);
        else
            System.Array.Clear(Samples, 0, Samples.Length);
    }
}
