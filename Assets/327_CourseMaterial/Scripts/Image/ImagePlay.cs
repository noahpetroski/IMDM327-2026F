// IMDM 327
// Import an image and plot into cubes + interactive update
// Based on Myungin Lee's 2025 ImagePlay example
using UnityEngine;

public class ImagePlay : MonoBehaviour
{
    public string imageFileName = "kandinsky";
    [Range(1, 64)] public int decimate = 32;
    public float depthOffset = 8f;
    [Range(0f, 1f)] public float alphaThreshold = 0.02f;
    public float timescale = 0.02f;

    GameObject[] cubes;
    int columns, rows;
    float time;
    Material material;

    void Start()
    {
        var texture = Resources.Load<Texture2D>(imageFileName);
        if (texture == null || !texture.isReadable)
        {
            Debug.LogWarning("ImagePlay: image missing or Read/Write disabled: " + imageFileName, this);
            return;
        }

        Color32[] pixels = texture.GetPixels32();
        decimate = Mathf.Clamp(decimate, 1, 64);
        columns = Mathf.Max(1, texture.width / decimate);
        rows = Mathf.Max(1, texture.height / decimate);
        cubes = new GameObject[columns * rows];
        material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        var color = new MaterialPropertyBlock();

        // Sample the image pixels and build a grid of colored cubes.
        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                Color pixel = pixels[y * decimate * texture.width + x * decimate];
                if (pixel.a < alphaThreshold) continue;

                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.SetParent(transform, false);
                cube.transform.localPosition = new Vector3(x - (columns - 1) * 0.5f,
                    y - (rows - 1) * 0.5f, depthOffset);
                cube.transform.localScale = new Vector3(1f, 1f, 1f + 30f * pixel.r);
                Destroy(cube.GetComponent<Collider>());

                var renderer = cube.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                color.SetColor("_BaseColor", pixel);
                renderer.SetPropertyBlock(color);
                cubes[x * rows + y] = cube;
            }
        }
    }

    void Update()
    {
        if (cubes == null) return;
        float scaledTime = time * timescale;
        for (int x = 0; x < columns; x++)
        {
            for (int y = 0; y < rows; y++)
            {
                var cube = cubes[x * rows + y];
                if (cube == null) continue;
                float z = depthOffset * (1f + Mathf.Sin(x * scaledTime));
                cube.transform.localPosition = new Vector3(x - (columns - 1) * 0.5f,
                    y - (rows - 1) * 0.5f, z);
            }
        }
        time += Time.deltaTime;
    }

    void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
