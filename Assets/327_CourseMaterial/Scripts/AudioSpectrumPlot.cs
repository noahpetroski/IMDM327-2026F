// Unity Audio Spectrum Plot Example
// IMDM Class Material
// Based on IMDM290 AudioSpectrumPlot.cs
using UnityEngine;

[RequireComponent(typeof(AudioSpectrum))]
public class AudioSpectrumPlot : MonoBehaviour
{
    // Scale the plot.
    [Range(1f, 100f)]
    public float scale = 10f;

    public int displayBins = 512;
    public float plotWidth = 10f;
    public Vector3 origin = new Vector3(-5f, -2f, 0f);

    AudioSpectrum spectrum;
    GameObject[] sampleBin;
    float xStep;

    void Start()
    {
        spectrum = GetComponent<AudioSpectrum>();
        displayBins = Mathf.Clamp(displayBins, 2, AudioSpectrum.FFTSIZE);
        sampleBin = new GameObject[displayBins];
        xStep = plotWidth / (displayBins - 1);

        // Create one cube for each frequency bin.
        for (int i = 0; i < sampleBin.Length; i++)
        {
            sampleBin[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sampleBin[i].name = "FFT_" + i;
            sampleBin[i].transform.SetParent(transform);
            sampleBin[i].transform.position = origin + new Vector3(i * xStep, 0f, 0f);
            sampleBin[i].transform.localScale = new Vector3(xStep * 0.8f, 0.001f, 0.1f);
            Destroy(sampleBin[i].GetComponent<Collider>());

            Material material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.color = Color.HSVToRGB(i / (float)sampleBin.Length, 0.7f, 1f);
            sampleBin[i].GetComponent<Renderer>().sharedMaterial = material;
        }

        // A few frequency labels below the bars, like AudioSpectrumPlotV2.
        for (int i = 0; i <= 6; i++)
        {
            int bin = Mathf.RoundToInt(i * (displayBins - 1) / 6f);
            CreateLabel($"{bin * spectrum.HzPerBin:0} Hz", new Vector3(bin * xStep, -0.2f, 0f));
        }
        CreateLabel(gameObject.name, new Vector3(plotWidth * 0.5f, -0.7f, 0f));
    }

    void CreateLabel(string value, Vector3 offset)
    {
        GameObject label = new GameObject(value);
        label.transform.SetParent(transform);
        label.transform.position = origin + offset;

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        TextMesh text = label.AddComponent<TextMesh>();
        text.font = font;
        text.text = value;
        text.fontSize = 48;
        text.characterSize = 0.035f;
        text.anchor = TextAnchor.UpperCenter;
        text.alignment = TextAlignment.Center;
        text.color = Color.white;
        label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
    }

    void LateUpdate()
    {
        // AudioSpectrum.Update reads the samples before we draw them.
        for (int i = 0; i < sampleBin.Length; i++)
        {
            float height = Mathf.Max(spectrum.Samples[i] * scale * scale, 0.001f);
            sampleBin[i].transform.localScale = new Vector3(xStep * 0.8f, height, 0.1f);
            sampleBin[i].transform.position = origin + new Vector3(i * xStep, height * 0.5f, 0f);
        }
    }

    void OnDestroy()
    {
        if (sampleBin == null) return;
        for (int i = 0; i < sampleBin.Length; i++)
            if (sampleBin[i] != null)
                Destroy(sampleBin[i].GetComponent<Renderer>().sharedMaterial);
    }
}
