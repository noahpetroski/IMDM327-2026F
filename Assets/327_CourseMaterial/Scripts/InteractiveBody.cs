// InteractiveBody Starter Code
// Fall 2025. IMDM 327
// Instructor. Myungin Lee
using UnityEngine;

public class InteractiveBody : MonoBehaviour
{
    public float G = 1f; // Gravity constant https://en.wikipedia.org/wiki/Gravitational_constant
    private GameObject[] body;
    BodyProperty[] bp;
    private int numberOfSphere = 120;
    public float fastforwardConst = 1f;
    public float radius = 3.5f;
    TrailRenderer[] trailRenderer;
    private GameObject[] interactivePoint = new GameObject[2];
    private Vector3[] previousHandPosition = new Vector3[2];
    private bool[] handWasPinched = new bool[2];
    private float[] actuation = new float[2];
    public float interactiveMass = 18f; // how much to interact
    public float shakeSensitivity = 3f;
    public float gravityDecay = 2f;
    MediaPipeBodyTracker mp;
    Camera cam;
    public float maxVelocity = 6f;
    public float closeDistance = 0.16f; // squared distance
    public Shader trailShader;
    public Shader pointShader;

    struct BodyProperty // why struct?
    {                   // https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/choosing-between-class-and-struct
        public float mass;
        public Vector3 velocity;
        public Vector3 acceleration;
    }


    void Start()
    {
        if (mp == null)
        {
            mp = FindObjectOfType<MediaPipeBodyTracker>();
            if (mp == null)
            {
                Debug.LogWarning("InteractiveBody could not locate a MediaPipeBodyTracker in the scene.");
            }
        }
        cam = Camera.main;
        // 0: left hand, 1: right hand
        for (int hand = 0; hand < 2; hand++)
        {
            interactivePoint[hand] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            interactivePoint[hand].transform.localScale = Vector3.one * 0.05f;
            Destroy(interactivePoint[hand].GetComponent<Collider>());
            interactivePoint[hand].GetComponent<Renderer>().material = new Material(pointShader != null ? pointShader : Shader.Find("Universal Render Pipeline/Unlit"));
            interactivePoint[hand].SetActive(false);
        }
        // Just like GO, computer should know how many room for struct is required:
        bp = new BodyProperty[numberOfSphere];
        body = new GameObject[numberOfSphere];
        trailRenderer = new TrailRenderer[numberOfSphere];
        // Loop generating the gameobject and assign initial conditions (type, position, (mass/velocity/acceleration)
        for (int i = 0; i < numberOfSphere; i++)
        {
            // Our gameobjects are created here:
            body[i] = GameObject.CreatePrimitive(PrimitiveType.Cube); // why sphere? try different options.
            Destroy(body[i].GetComponent<Collider>());
            // https://docs.unity3d.com/ScriptReference/GameObject.CreatePrimitive.html

            // initial conditions
            float r = radius;
            // position is (x,y,z). In this case, I want to plot them on the circle with r
            // ******** Fill in this part ******** // Initialization of the position
            body[i].transform.position = new Vector3(r * Mathf.Sin(i * 2f * Mathf.PI / numberOfSphere),
                                                      r * Mathf.Cos(i * 2f * Mathf.PI / numberOfSphere),
                                                      Random.Range(-0.05f, 0.05f) * r);

            bp[i].velocity = new Vector3(Mathf.Cos(i * 2f * Mathf.PI / numberOfSphere),
                                        -Mathf.Sin(i * 2f * Mathf.PI / numberOfSphere), 0f) * maxVelocity * 0.3f;
            bp[i].mass = Random.Range(0.01f, 0.05f); // Simplified. Try different initial condition
            body[i].GetComponent<MeshRenderer>().enabled = false;

            // + This is just pretty trails
            trailRenderer[i] = body[i].AddComponent<TrailRenderer>();
            // Configure the TrailRenderer's properties
            trailRenderer[i].time = 3.0f;  // Duration of the trail
            trailRenderer[i].startWidth = 0.025f;  // Width of the trail at the start
            trailRenderer[i].endWidth = 0.002f;    // Width of the trail at the end
            // a material to the trail
            trailRenderer[i].material = new Material(trailShader != null ? trailShader : Shader.Find("Sprites/Default"));
            // Set the trail color
            Gradient gradient = new Gradient();
            float h = (i / (float)numberOfSphere) % 1f;
            float s = 0.6f;
            float v = 1f;
            Color c = Color.HSVToRGB(h, s, v); 

            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(c, 0.0f),
                                        new GradientColorKey(c, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            trailRenderer[i].colorGradient = gradient;

        }
    }

    void FixedUpdate()
    {
        // Loop for N-body gravity
        // How should we design the loop?
        // initailize 
        for (int i = 0; i < numberOfSphere; i++)
        {
            bp[i].acceleration = Vector3.zero; // important
        }

        // Acceleration (Force)  
        for (int i = 0; i < numberOfSphere; i++)
        {
            for (int j = i + 1; j < numberOfSphere; j++)
            {
                // Vector from i to j body. Make sure which vector you are getting.
                Vector3 distance = body[j].transform.position - body[i].transform.position;
                // Gravity
                Vector3 gravity = CalculateGravity(distance, bp[i].mass, bp[j].mass);
                // Apply Gravity
                // F = ma -> a = F/m
                // Gravity is push and pull with same amount. Force: m1 <-> m2

                // .. only if it is not too close
                if (distance.sqrMagnitude > closeDistance)
                {
                    bp[i].acceleration += gravity / bp[i].mass; // why is this +?
                    bp[j].acceleration -= gravity / bp[j].mass; // why is this -? What decided the direction?                   
                }
                else // apply opposite gravity (push) if too close. 
                { // Hatred is stronger than attraction.
                    bp[i].acceleration -= 3f * gravity / bp[i].mass; // 
                    bp[j].acceleration += 3f *gravity / bp[j].mass; // 
                }

            }
        }
    
        // (Force) Hesitation: randomly hover the space for natural behavior  
        for (int i = 0; i < numberOfSphere; i++)
        {
            float randomScale = 1f;
            if (Random.Range(0f, 1.05f) > 1f)
            {
                bp[i].acceleration += new Vector3(randomScale * Random.Range(-1f, 1f), randomScale * Random.Range(-1f, 1f), randomScale * Random.Range(-1f, 1f));
            }
        }


        // (Force) Interactive Acceleration : reacts to the actuation of the interactive point
        if (mp == null)
        {
            mp = FindObjectOfType<MediaPipeBodyTracker>();
        }

        for (int hand = 0; hand < 2; hand++)
        {
            bool handTracked = mp != null && (hand == 0 ? mp.LeftHandTracked : mp.RightHandTracked);
            bool handPinched = handTracked && (hand == 0 ? mp.LeftHandPinch : mp.RightHandPinch);
            interactivePoint[hand].SetActive(handPinched);
            if (handPinched && cam != null)
            {
                Vector3 handPosition = hand == 0 ? mp.LeftHandPosition : mp.RightHandPosition;
                Vector3 interactPoint = cam.ViewportToWorldPoint(new Vector3(1f - handPosition.x, 1f - handPosition.y, -cam.transform.position.z));
                interactivePoint[hand].transform.position = interactPoint;
                if (!handWasPinched[hand]) previousHandPosition[hand] = handPosition;

                // Pinch and shake. Small movements do not create gravity.
                float speed = (handPosition - previousHandPosition[hand]).magnitude / Time.fixedDeltaTime;
                // Ignore small movements and scale shake strength to 0-1.
                float movement = Mathf.Clamp01((speed - 0.15f) * shakeSensitivity);
                // Stronger shaking increases gravity; stopping lets it fade.
                actuation[hand] = Mathf.Max(movement * movement, Mathf.MoveTowards(actuation[hand], 0f, gravityDecay * Time.fixedDeltaTime));
                interactivePoint[hand].transform.localScale = Vector3.one * (0.05f + 0.12f * actuation[hand]);

                for (int i = 0; i < numberOfSphere; i++)
                {
                    Vector3 distance = interactPoint - body[i].transform.position;
                    bp[i].acceleration += CalculateGravity(distance, bp[i].mass, interactiveMass) / bp[i].mass * actuation[hand];
                }
                previousHandPosition[hand] = handPosition;
            }
            else
            {
                actuation[hand] = 0f;
            }
            handWasPinched[hand] = handPinched;
        }

        // Apply acceleration to velocity, to position
        for (int i = 0; i < numberOfSphere; i++)
        {
            // Bring the boids back into the frame.
            Vector3 distance = body[i].transform.position;
            if (distance.magnitude > radius)
            {
                bp[i].acceleration -= distance.normalized * (distance.magnitude - radius) * 6f;
            }
            bp[i].acceleration.z -= distance.z * 3f + bp[i].velocity.z;
            bp[i].acceleration = Vector3.ClampMagnitude(bp[i].acceleration, 20f);
            // velocity is sigma(Acceleration*time)
            bp[i].velocity += bp[i].acceleration * Time.deltaTime * fastforwardConst;
            // Limit the maximum velocity
            if (bp[i].velocity.magnitude > maxVelocity)
            {
                bp[i].velocity = maxVelocity * bp[i].velocity.normalized;
            }
            // Prevent extra ordinary speed
            body[i].transform.position += bp[i].velocity * Time.deltaTime * fastforwardConst;
            if (bp[i].velocity.sqrMagnitude > 0f)
                body[i].transform.LookAt(body[i].transform.position + bp[i].velocity);
        }

    }


    // Gravity Fuction
    private Vector3 CalculateGravity(Vector3 distanceVector, float m1, float m2)
    {
        Vector3 gravity = new Vector3(0f, 0f, 0f); // note this is also Vector3
                                                   // **** Fill in the function below.
        float eps = 0.1f;
        gravity = G * m1 * m2 / (distanceVector.magnitude + eps) * distanceVector.normalized;
        return gravity;
    }
}

