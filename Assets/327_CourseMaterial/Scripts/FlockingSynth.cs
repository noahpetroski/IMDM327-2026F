using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class FlockingSynth : MonoBehaviour
{
    [Range(1, 300)] public int numberOfBoids = 100;
    public Vector3 center = new Vector3(0f, 1f, 2f);
    public float fieldRadius = 3.5f;
    public float maxVelocity = 3f;
    public float closeDistance = 0.16f; // Squared distance, as in PianoBoids.
    public float fastforwardConst = 1f;
    public float G = 1f;
    public float hesitation = 3f;
    public float accelerationScale = 4f;
    [Range(0f, 1f)] public float masterGain = 0.2f;
    public float smoothingSeconds = 0.05f;
    public Vector2 carrierFrequency = new Vector2(80f, 1200f);
    public Vector2 modulatorFrequency = new Vector2(40f, 2400f);
    public Vector2 modulationIndex = new Vector2(0.2f, 12f);
    public Vector2 amplitude = new Vector2(0.05f, 0.8f);
    public Shader trailShader;

    struct BodyProperty
    {
        public float mass;
        public Vector3 position, velocity, acceleration;
    }

    BodyProperty[] bp;
    Transform[] body;
    BoidFMSynth[] synth;
    TrailRenderer[] trails;
    AudioSource source;
    GameObject visuals;
    Material trailMaterial;
    int sampleRate;
    volatile bool ready;

    void Start()
    {
        numberOfBoids = Mathf.Clamp(numberOfBoids, 1, 300);
        sampleRate = AudioSettings.outputSampleRate;
        source = GetComponent<AudioSource>();
        source.Stop();
        source.clip = null;
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 1f;
        source.pitch = 1f;
        visuals = new GameObject("Flocking Synth Boids");
        visuals.transform.SetParent(transform, false);
        trailMaterial = new Material(trailShader != null ? trailShader : Shader.Find("Sprites/Default"));
        bp = new BodyProperty[numberOfBoids];
        body = new Transform[numberOfBoids];
        synth = new BoidFMSynth[numberOfBoids];
        trails = new TrailRenderer[numberOfBoids];

        for (int i = 0; i < bp.Length; i++)
        {
            GameObject boid = new GameObject("Boid_" + i);
            boid.transform.SetParent(visuals.transform, false);
            body[i] = boid.transform;
            float angle = i * 2f * Mathf.PI / bp.Length;
            bp[i].position = center + new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f) * fieldRadius;
            bp[i].position.z += Random.Range(-0.2f, 0.2f);
            bp[i].mass = Random.Range(0.01f, 0.05f);
            bp[i].velocity = new Vector3(Mathf.Cos(angle), -Mathf.Sin(angle), 0f) * maxVelocity * Random.Range(0.2f, 0.5f);
            body[i].localPosition = bp[i].position;
            synth[i] = boid.AddComponent<BoidFMSynth>();
            synth[i].SetPhase(angle);
            trails[i] = boid.AddComponent<TrailRenderer>();
            trails[i].sharedMaterial = trailMaterial;
            trails[i].time = 3f;
            trails[i].startWidth = 0.025f;
            trails[i].endWidth = 0.002f;
            trails[i].minVertexDistance = 0.04f;
            Color color = Color.HSVToRGB(i / (float)bp.Length, 0.65f, 1f);
            trails[i].startColor = color;
            color.a = 0f;
            trails[i].endColor = color;
            UpdateSound(i);
        }
        ready = true;
        source.Play();
    }

    void FixedUpdate()
    {
        if (bp == null) return;
        // Clear acceleration before adding forces.
        for (int i = 0; i < bp.Length; i++)
        {
            bp[i].acceleration = Vector3.zero;
        }
        for (int i = 0; i < bp.Length; i++)
        {
            for (int j = i + 1; j < bp.Length; j++)
            {
                Vector3 distance = bp[j].position - bp[i].position;
                Vector3 gravity = CalculateGravity(distance, bp[i].mass, bp[j].mass);
                if (distance.sqrMagnitude > closeDistance)
                {
                    bp[i].acceleration += gravity / bp[i].mass;
                    bp[j].acceleration -= gravity / bp[j].mass;
                }
                else
                {
                    bp[i].acceleration -= 3f * gravity / bp[i].mass;
                    bp[j].acceleration += 3f * gravity / bp[j].mass;
                }
            }
        }

        float step = Time.fixedDeltaTime * Mathf.Max(0f, fastforwardConst);
        for (int i = 0; i < bp.Length; i++)
        {
            if (Random.Range(0f, 1.05f) > 1f)
                bp[i].acceleration += Random.insideUnitSphere * hesitation;
            Vector3 distance = bp[i].position - center;
            float radius = Mathf.Max(0.01f, fieldRadius);
            if (distance.magnitude > radius)
                bp[i].acceleration -= distance.normalized * (distance.magnitude - radius) * 6f;
            bp[i].acceleration.z -= distance.z * 3f + bp[i].velocity.z;
            bp[i].acceleration = Vector3.ClampMagnitude(bp[i].acceleration, 20f);
            bp[i].velocity += bp[i].acceleration * step;
            if (bp[i].velocity.magnitude > Mathf.Max(0f, maxVelocity))
            {
                bp[i].velocity = Mathf.Max(0f, maxVelocity) * bp[i].velocity.normalized;
            }
            bp[i].position += bp[i].velocity * step;
            body[i].localPosition = bp[i].position;
            UpdateSound(i);
        }
    }

    Vector3 CalculateGravity(Vector3 distance, float m1, float m2)
    {
        return G * m1 * m2 / (distance.magnitude + 0.1f) * distance.normalized;
    }

    void UpdateSound(int i)
    {
        float radius = Mathf.Max(0.01f, fieldRadius);
        Vector3 distance = bp[i].position - center;
        float speed = Mathf.Clamp01(bp[i].velocity.magnitude / Mathf.Max(0.01f, maxVelocity));
        float acceleration = Mathf.Clamp01(bp[i].acceleration.magnitude / Mathf.Max(0.01f, accelerationScale));
        float height = Mathf.InverseLerp(-radius, radius, distance.y);

        // Change these four lines to connect other body parameters to the sound.
        if (synth[i].followMotion)
        {
            synth[i].carrierFrequency = carrierFrequency.x * Mathf.Pow(carrierFrequency.y / Mathf.Max(1f, carrierFrequency.x), speed);
            synth[i].modulatorFrequency = Mathf.Lerp(modulatorFrequency.x, modulatorFrequency.y, height);
            synth[i].modulationIndex = Mathf.Lerp(modulationIndex.x, modulationIndex.y, acceleration);
            synth[i].amplitude = Mathf.Lerp(amplitude.x, amplitude.y, speed);
            synth[i].pan = Mathf.Clamp(distance.x / radius, -0.9f, 0.9f);
        }
        synth[i].UpdateParameters();
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!ready || channels <= 0 || sampleRate <= 0)
        {
            System.Array.Clear(data, 0, data.Length);
            return;
        }
        float smoothing = 1f - (float)System.Math.Exp(-1f / (sampleRate * System.Math.Max(0.001f, smoothingSeconds)));
        float gain = Mathf.Clamp01(masterGain) / (float)System.Math.Sqrt(synth.Length);
        for (int i = 0; i < synth.Length; i++) synth[i].ReadParameters();
        for (int sample = 0; sample < data.Length; sample += channels)
        {
            double left = 0, right = 0;
            for (int i = 0; i < synth.Length; i++)
            {
                synth[i].Render(sampleRate, smoothing, out float l, out float r);
                left += l;
                right += r;
            }
            float outputL = (float)System.Math.Tanh(left * gain);
            float outputR = (float)System.Math.Tanh(right * gain);
            if (channels == 1) data[sample] = (outputL + outputR) * 0.5f;
            else
            {
                data[sample] = outputL;
                data[sample + 1] = outputR;
                for (int channel = 2; channel < channels; channel++) data[sample + channel] = 0f;
            }
        }
    }

    void OnEnable()
    {
        if (visuals != null) visuals.SetActive(true);
        if (source != null && ready) source.Play();
    }

    void OnDisable()
    {
        if (source != null) source.Stop();
        if (visuals != null) visuals.SetActive(false);
    }

    void OnDestroy()
    {
        ready = false;
        if (source != null) source.Stop();
        if (visuals != null) Destroy(visuals);
        if (trailMaterial != null) Destroy(trailMaterial);
    }
}
