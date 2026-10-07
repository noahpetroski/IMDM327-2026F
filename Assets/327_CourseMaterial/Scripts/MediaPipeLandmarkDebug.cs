using System.Collections;
using Mediapipe;
using Mediapipe.Unity;
using Mediapipe.Unity.Sample.Holistic;
using UnityEngine;
using Color = UnityEngine.Color;

public class MediaPipeLandmarkDebug : MonoBehaviour
{
    public HolisticTrackingGraph graphRunner;
    public bool debugMode = true;
    public float dotSize = 1.8f;
    public Shader landmarkShader;
    public Camera sceneCamera;

    private NormalizedLandmarkList leftHandLandmarks, rightHandLandmarks, poseLandmarks, faceLandmarks;
    private readonly object dataLock = new object();
    private GameObject[] leftHandDots, rightHandDots, poseDots, faceDots;
    private GameObject debugPoints;
    private Material material;
    private Coroutine subscribeRoutine;
    private bool hasSubscriptions;
    private int[] facePoints = { 1, 4, 33, 133, 263, 362, 61, 291, 10, 152 };

    private void OnEnable()
    {
        if (graphRunner == null) graphRunner = FindObjectOfType<HolisticTrackingGraph>();
        subscribeRoutine = StartCoroutine(SubscribeWhenReady());
    }

    private IEnumerator SubscribeWhenReady()
    {
        while (isActiveAndEnabled)
        {
            if (TrySubscribe())
            {
                subscribeRoutine = null;
                yield break;
            }
            yield return null;
        }
    }

    private bool TrySubscribe()
    {
        if (graphRunner == null || hasSubscriptions) return hasSubscriptions;
        try
        {
            graphRunner.OnLeftHandLandmarksOutput += HandleLeftHand;
            graphRunner.OnRightHandLandmarksOutput += HandleRightHand;
            graphRunner.OnPoseLandmarksOutput += HandlePose;
            graphRunner.OnFaceLandmarksOutput += HandleFace;
            hasSubscriptions = true;
            return true;
        }
        catch (System.NullReferenceException) { return false; }
    }

    private void HandleLeftHand(object sender, OutputStream<NormalizedLandmarkList>.OutputEventArgs eventArgs)
    {
        var landmarks = ExtractNormalizedLandmarks(eventArgs.packet);
        lock (dataLock) leftHandLandmarks = landmarks;
    }

    private void HandleRightHand(object sender, OutputStream<NormalizedLandmarkList>.OutputEventArgs eventArgs)
    {
        var landmarks = ExtractNormalizedLandmarks(eventArgs.packet);
        lock (dataLock) rightHandLandmarks = landmarks;
    }

    private void HandlePose(object sender, OutputStream<NormalizedLandmarkList>.OutputEventArgs eventArgs)
    {
        var landmarks = ExtractNormalizedLandmarks(eventArgs.packet);
        lock (dataLock) poseLandmarks = landmarks;
    }

    private void HandleFace(object sender, OutputStream<NormalizedLandmarkList>.OutputEventArgs eventArgs)
    {
        var landmarks = ExtractNormalizedLandmarks(eventArgs.packet);
        lock (dataLock) faceLandmarks = landmarks;
    }

    private NormalizedLandmarkList ExtractNormalizedLandmarks(Packet<NormalizedLandmarkList> packet)
    {
        return packet == null ? null : packet.Get(NormalizedLandmarkList.Parser);
    }

    private void Update()
    {
        if (debugMode && debugPoints == null)
        {
            debugPoints = new GameObject("Landmark dots");
            debugPoints.transform.SetParent(transform, false);
            material = new Material(landmarkShader != null ? landmarkShader : Shader.Find("Universal Render Pipeline/Unlit"));
            leftHandDots = CreateDots(21, new Color(0.2f, 0.8f, 1f));
            rightHandDots = CreateDots(21, new Color(1f, 0.5f, 0.2f));
            poseDots = CreateDots(33, new Color(0.35f, 1f, 0.5f));
            faceDots = CreateDots(facePoints.Length, new Color(1f, 0.8f, 0.25f));
        }
        if (debugPoints == null) return;
        debugPoints.SetActive(debugMode);
        if (!debugMode) return;
        lock (dataLock)
        {
            DrawDots(leftHandDots, leftHandLandmarks);
            DrawDots(rightHandDots, rightHandLandmarks);
            DrawDots(poseDots, poseLandmarks);
            DrawDots(faceDots, faceLandmarks, facePoints);
        }
    }

    private GameObject[] CreateDots(int count, Color color)
    {
        GameObject[] dots = new GameObject[count];
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        for (int i = 0; i < count; i++)
        {
            dots[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(dots[i].GetComponent<Collider>());
            dots[i].transform.SetParent(debugPoints.transform, false);
            dots[i].GetComponent<Renderer>().sharedMaterial = material;
            dots[i].GetComponent<Renderer>().SetPropertyBlock(block);
            dots[i].SetActive(false);
        }
        return dots;
    }

    private void DrawDots(GameObject[] dots, NormalizedLandmarkList landmarks, int[] indices = null)
    {
        for (int i = 0; i < dots.Length; i++)
        {
            int point = i;
            if (indices != null) point = indices[i];
            bool visible = landmarks != null && point < landmarks.Landmark.Count;
            if (visible && landmarks.Landmark[point].HasVisibility)
                visible = landmarks.Landmark[point].Visibility > 0.5f;
            dots[i].SetActive(visible);
            if (!visible) continue;
            var p = landmarks.Landmark[point];
            Vector3 position = new Vector3(p.X, p.Y, p.Z);
            // Same coordinates as InteractiveBody's hand gravity point.
            dots[i].transform.position = -position * 200f + new Vector3(100f, 100f, 180f);
            if (sceneCamera != null)
                dots[i].transform.position = sceneCamera.ViewportToWorldPoint(new Vector3(1f - p.X, 1f - p.Y, -sceneCamera.transform.position.z));
            dots[i].transform.localScale = Vector3.one * dotSize;
        }
    }

    private void OnDisable()
    {
        if (subscribeRoutine != null) StopCoroutine(subscribeRoutine);
        subscribeRoutine = null;
        if (hasSubscriptions && graphRunner != null)
        {
            try
            {
                graphRunner.OnLeftHandLandmarksOutput -= HandleLeftHand;
                graphRunner.OnRightHandLandmarksOutput -= HandleRightHand;
                graphRunner.OnPoseLandmarksOutput -= HandlePose;
                graphRunner.OnFaceLandmarksOutput -= HandleFace;
            }
            catch (System.NullReferenceException) { }
        }
        hasSubscriptions = false;
        lock (dataLock) leftHandLandmarks = rightHandLandmarks = poseLandmarks = faceLandmarks = null;
        if (debugPoints != null) debugPoints.SetActive(false);
    }

    private void OnDestroy()
    {
        if (debugPoints != null) Destroy(debugPoints);
        if (material != null) Destroy(material);
    }
}
