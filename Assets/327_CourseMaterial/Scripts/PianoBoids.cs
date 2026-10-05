// IMDM 327
// Based on InteractiveBody (Myungin Lee, 2025): gravity + repulsion + trails.
// Piano notes replace the hand-controlled interactive point.
using UnityEngine;

[RequireComponent(typeof(SoundFMPiano))]
public class PianoBoids : MonoBehaviour
{
    [Range(16, 300)] public int numberOfBoids = 120;
    public Vector3 center = new Vector3(0f, 1f, 2f);
    public float fieldRadius = 3.5f;
    public float maxVelocity = 3f;
    public float closeDistance = 0.16f; // Squared distance, like InteractiveBody.
    public float fastforwardConst = 1f;
    public float G = 1f;
    public float noteMass = 5f;
    public float resonanceStrength = 4f;
    [Range(0f, 1f)] public float resonanceThreshold = 0.3f;
    public float connectionDistance = 2f;

    struct BodyProperty
    {
        public float mass;
        public Vector3 position, velocity, acceleration;
    }

    SoundFMPiano piano;
    BodyProperty[] bp;
    Transform[] body;
    TrailRenderer[] trails;
    GameObject[] notePoints;
    Vector3[] notePositions;
    Color[] noteColors;
    bool[] activeNotes;
    float[] noteWeights, notePulls;
    float[,] resonance;
    int[] noteMidi, boidNotes, linkTargets;
    float[] linkDistances;
    LineRenderer[] links;
    GameObject visuals;
    Material material, trailMaterial;
    MaterialPropertyBlock color;

    void Start()
    {
        piano = GetComponent<SoundFMPiano>();
        numberOfBoids = Mathf.Clamp(numberOfBoids, 16, 300);
        visuals = new GameObject("Piano Boids");
        visuals.transform.SetParent(transform, false);
        material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        trailMaterial = new Material(Shader.Find("Sprites/Default"));
        color = new MaterialPropertyBlock();
        bp = new BodyProperty[numberOfBoids];
        body = new Transform[numberOfBoids];
        trails = new TrailRenderer[numberOfBoids];
        boidNotes = new int[numberOfBoids];
        linkTargets = new int[numberOfBoids];
        linkDistances = new float[numberOfBoids];
        links = new LineRenderer[numberOfBoids];

        // Original circle initialization, scaled to the 00_World camera.
        for (int i = 0; i < numberOfBoids; i++)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Boid_" + i;
            cube.transform.SetParent(visuals.transform, false);
            cube.transform.localScale = Vector3.one * 0.07f;
            Destroy(cube.GetComponent<Collider>());
            body[i] = cube.transform;
            cube.GetComponent<MeshRenderer>().enabled = false; // Only show the trail.

            float angle = i * 2f * Mathf.PI / numberOfBoids;
            bp[i].position = center + new Vector3(fieldRadius * Mathf.Sin(angle),
                fieldRadius * Mathf.Cos(angle), Random.Range(-0.2f, 0.2f));
            // Small masses suit this smaller scene. Tangential velocity starts an orbit.
            bp[i].mass = Random.Range(0.01f, 0.05f);
            bp[i].velocity = new Vector3(Mathf.Cos(angle), -Mathf.Sin(angle), 0f) * maxVelocity * 0.3f;
            body[i].localPosition = bp[i].position;
            trails[i] = cube.AddComponent<TrailRenderer>();
            trails[i].sharedMaterial = trailMaterial;
            trails[i].time = 5f;
            trails[i].startWidth = 0.045f;
            trails[i].endWidth = 0.004f;
            trails[i].minVertexDistance = 0.04f;
            SetColor(i, Color.HSVToRGB(i / (float)numberOfBoids, 0.65f, 1f));

            var link = new GameObject("Connection_" + i);
            link.transform.SetParent(visuals.transform, false);
            links[i] = link.AddComponent<LineRenderer>();
            links[i].sharedMaterial = trailMaterial;
            links[i].useWorldSpace = false;
            links[i].positionCount = 2;
            links[i].enabled = false;
        }

        // One gravity point for each playable keyboard key.
        notePoints = new GameObject[piano.KeyCount];
        notePositions = new Vector3[piano.KeyCount];
        noteColors = new Color[piano.KeyCount];
        activeNotes = new bool[piano.KeyCount];
        noteWeights = new float[piano.KeyCount];
        notePulls = new float[piano.KeyCount];
        resonance = new float[piano.KeyCount, piano.KeyCount];
        noteMidi = new int[piano.KeyCount];
        for (int key = 0; key < noteMidi.Length; key++) noteMidi[key] = -1;
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        for (int key = 0; key < piano.KeyCount; key++)
        {
            int midi = piano.GetNoteMidi(key);
            if (midi < 0) continue;
            float t = (midi - piano.rootMidi) / 19f;
            float angle = t * 2f * Mathf.PI;
            notePositions[key] = center + new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f) * fieldRadius * 0.65f;
            noteColors[key] = Color.HSVToRGB(t, 0.7f, 1f);

            var point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            point.name = "Note_" + piano.GetNoteName(key);
            point.transform.SetParent(visuals.transform, false);
            point.transform.localPosition = notePositions[key];
            point.transform.localScale = Vector3.one * 0.22f;
            Destroy(point.GetComponent<Collider>());
            point.GetComponent<Renderer>().sharedMaterial = material;
            color.SetColor("_BaseColor", noteColors[key]);
            point.GetComponent<Renderer>().SetPropertyBlock(color);
            notePoints[key] = point;

            var label = new GameObject("Note name");
            label.transform.SetParent(point.transform, false);
            label.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            TextMesh text = label.AddComponent<TextMesh>();
            text.font = font;
            text.text = piano.GetNoteName(key);
            text.fontSize = 48;
            text.characterSize = 0.1f;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = noteColors[key];
            label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            point.SetActive(false);
        }
    }

    void FixedUpdate()
    {
        if (bp == null) return;

        bool pitchChanged = false;
        for (int key = 0; key < activeNotes.Length; key++)
        {
            int midi = piano.GetNoteMidi(key);
            if (noteMidi[key] != midi) pitchChanged = true;
            noteMidi[key] = midi;
            if (notePoints[key] == null) continue;
            bool held = piano.IsNoteHeld(key);
            float duration = held ? piano.attackSeconds : piano.releaseSeconds;
            noteWeights[key] = Mathf.MoveTowards(noteWeights[key], held ? 1f : 0f,
                Time.fixedDeltaTime / Mathf.Max(duration, 0.001f));
            activeNotes[key] = noteWeights[key] > 0.001f;
            notePoints[key].SetActive(activeNotes[key]);
            notePoints[key].transform.localScale = Vector3.one * 0.22f * noteWeights[key];
        }
        if (pitchChanged)
        {
            for (int a = 0; a < noteMidi.Length; a++)
                for (int b = a + 1; b < noteMidi.Length; b++)
                    resonance[a, b] = resonance[b, a] = noteMidi[a] < 0 || noteMidi[b] < 0 ? 0f
                        : CalculateResonance(440f * Mathf.Pow(2f, (noteMidi[a] - 69) / 12f),
                            440f * Mathf.Pow(2f, (noteMidi[b] - 69) / 12f));
        }

        // Initialize acceleration, as in InteractiveBody.
        float loudness = Mathf.Clamp(piano.amplitude / 0.12f, 0f, 3f);
        for (int key = 0; key < activeNotes.Length; key++)
            notePulls[key] = activeNotes[key] ? noteWeights[key] * loudness
                * Mathf.Pow(2f, (noteMidi[key] - 60) / 24f) : 0f;
        for (int i = 0; i < bp.Length; i++)
        {
            bp[i].acceleration = Vector3.zero;
            boidNotes[i] = -1;
            linkTargets[i] = -1;
            linkDistances[i] = Mathf.Max(0f, connectionDistance) * Mathf.Max(0f, connectionDistance);

            // Follow the note exerting the strongest gravity at this position.
            float strongest = 0f;
            for (int key = 0; key < activeNotes.Length; key++)
            {
                if (!activeNotes[key]) continue;
                float pull = notePulls[key] / ((notePositions[key] - bp[i].position).magnitude + 0.1f);
                if (pull <= strongest) continue;
                strongest = pull;
                boidNotes[i] = key;
            }
        }

        // N-body gravity: attract if far, repel three times as strongly if close.
        for (int i = 0; i < bp.Length; i++)
        {
            for (int j = i + 1; j < bp.Length; j++)
            {
                Vector3 distance = bp[j].position - bp[i].position;
                Vector3 gravity = CalculateGravity(distance, bp[i].mass, bp[j].mass);
                float coupling = GetCoupling(i, j, loudness);
                if (distance.sqrMagnitude > closeDistance)
                {
                    gravity *= 1f + Mathf.Max(0f, resonanceStrength) * coupling;
                    bp[i].acceleration += gravity / bp[i].mass;
                    bp[j].acceleration -= gravity / bp[j].mass;
                }
                else
                {
                    bp[i].acceleration -= 3f * gravity / bp[i].mass;
                    bp[j].acceleration += 3f * gravity / bp[j].mass;
                }
                // One nearby connection per body, found in the existing pair loop.
                if (coupling <= 0f || coupling < resonanceThreshold) continue;
                if (distance.sqrMagnitude < linkDistances[i])
                {
                    linkDistances[i] = distance.sqrMagnitude;
                    linkTargets[i] = j;
                }
                if (distance.sqrMagnitude < linkDistances[j])
                {
                    linkDistances[j] = distance.sqrMagnitude;
                    linkTargets[j] = i;
                }
            }
        }

        // Original random "hesitation" force; FM index controls its strength.
        float randomScale = Mathf.Clamp(piano.modulationIndex, 0f, 20f) * 0.4f;
        for (int i = 0; i < bp.Length; i++)
        {
            if (Random.Range(0f, 1.05f) > 1f)
                bp[i].acceleration += new Vector3(Random.Range(-1f, 1f),
                    Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * randomScale;
        }

        // Each sounding note acts like the original interactivePoint.
        // Sum all active notes, so chords generate several gravity sources.
        for (int key = 0; key < activeNotes.Length; key++)
        {
            if (!activeNotes[key]) continue;
            float interactiveMass = noteMass * notePulls[key];
            for (int i = 0; i < bp.Length; i++)
            {
                Vector3 distance = notePositions[key] - bp[i].position;
                bp[i].acceleration += CalculateGravity(distance, bp[i].mass, interactiveMass) / bp[i].mass;
            }
        }

        // Integrate acceleration -> velocity -> position, with the original speed limit.
        // FM modulator ratio controls how quickly the simulation advances.
        float step = Time.fixedDeltaTime * fastforwardConst
            * Mathf.Clamp(0.5f + piano.modulatorRatio * 0.25f, 0.25f, 3f);
        for (int i = 0; i < bp.Length; i++)
        {
            // Keep this small demo in camera view, without a forced orbit target.
            Vector3 fromCenter = bp[i].position - center;
            if (fromCenter.magnitude > fieldRadius)
                bp[i].acceleration -= fromCenter.normalized * (fromCenter.magnitude - fieldRadius) * 6f;
            bp[i].acceleration.z -= fromCenter.z * 3f + bp[i].velocity.z;

            bp[i].velocity += Vector3.ClampMagnitude(bp[i].acceleration, 20f) * step;
            if (bp[i].velocity.magnitude > maxVelocity)
                bp[i].velocity = maxVelocity * bp[i].velocity.normalized;
            bp[i].position += bp[i].velocity * step;
            body[i].localPosition = bp[i].position;

            // Same hue per body, saturation reacting to acceleration.
            float saturation = Mathf.Clamp01(0.45f + bp[i].acceleration.sqrMagnitude / 1000f);
            SetColor(i, Color.HSVToRGB(i / (float)bp.Length, saturation, 0.98f));
        }

        for (int i = 0; i < bp.Length; i++)
        {
            int j = linkTargets[i];
            // Draw a mutual connection only once.
            links[i].enabled = j >= 0 && !(j < i && linkTargets[j] == i);
            if (!links[i].enabled) continue;
            float strength = GetCoupling(i, j, loudness);
            links[i].startWidth = links[i].endWidth = 0.008f + 0.025f * strength;
            Color a = noteColors[boidNotes[i]], b = noteColors[boidNotes[j]];
            a.a = b.a = 0.15f + 0.65f * strength;
            links[i].startColor = a;
            links[i].endColor = b;
            links[i].SetPosition(0, bp[i].position);
            links[i].SetPosition(1, bp[j].position);
        }
    }

    float GetCoupling(int i, int j, float loudness)
    {
        int a = boidNotes[i], b = boidNotes[j];
        if (a < 0 || b < 0 || a == b) return 0f;
        return resonance[a, b] * noteWeights[a] * noteWeights[b] * Mathf.Clamp01(loudness);
    }

    float CalculateResonance(float frequencyA, float frequencyB)
    {
        // Demo score: overlapping low harmonics, with tolerance for equal temperament.
        float best = 0f;
        for (int a = 1; a <= 6; a++)
            for (int b = 1; b <= 6; b++)
            {
                float cents = 1200f * Mathf.Log(frequencyA * a / (frequencyB * b), 2f);
                float overlap = Mathf.Exp(-cents * cents / (40f * 40f));
                best = Mathf.Max(best, overlap * 2f / Mathf.Sqrt(a * b));
            }
        return Mathf.Clamp01(best);
    }

    Vector3 CalculateGravity(Vector3 distance, float m1, float m2)
    {
        // Keep the original InteractiveBody formula (1 / distance).
        float eps = 0.1f;
        return G * m1 * m2 / (distance.magnitude + eps) * distance.normalized;
    }

    void SetColor(int i, Color value)
    {
        // Same color/alpha ramp as the original Gradient, without per-frame allocations.
        trails[i].startColor = value;
        trails[i].endColor = new Color(value.r, value.g, value.b, 0f);
    }

    void OnEnable()
    {
        if (visuals != null) visuals.SetActive(true);
    }

    void OnDisable()
    {
        if (visuals != null) visuals.SetActive(false);
    }

    void OnDestroy()
    {
        if (visuals != null) Destroy(visuals);
        if (material != null) Destroy(material);
        if (trailMaterial != null) Destroy(trailMaterial);
    }
}

