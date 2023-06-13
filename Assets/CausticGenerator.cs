using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class PerlinArgs
{
    public readonly Vector2 origin;
    public readonly float amplitude;
    public readonly float widthStep;
    public readonly float heightStep;

    public PerlinArgs(
        Vector2 origin,
        float amplitude,
        float widthStep,
        float heightStep)
    {
        this.origin = origin;
        this.amplitude = amplitude;
        this.widthStep = widthStep;
        this.heightStep = heightStep;
    }
}

public class PlanePolyArgs
{
    public readonly Vector3 position;
    public readonly int segmentWidth;
    public readonly int segmentDepth;
    public readonly float width;
    public readonly float depth;
    public readonly Color color;
    public readonly PerlinArgs[] perlinSteps;

    public PlanePolyArgs() : this(new Vector3(), 10, 10, 0.1f, 0.1f, Color.white, new PerlinArgs[0]) { }

    public PlanePolyArgs(
        Vector3 position,
        int segmentWidth,
        int segmentDepth,
        float width,
        float depth,
        Color color,
        PerlinArgs[] perlinSteps)
    {
        this.position = position;

        this.segmentWidth = segmentWidth;
        this.segmentDepth = segmentDepth;

        this.width = width;
        this.depth = depth;

        this.color = color;
        this.perlinSteps = perlinSteps;
    }
}

public class PlanePoly
{
    public readonly List<Vector3> verts;
    public readonly List<int> tris;
    public readonly Vector3 offset;

    public readonly int segmentWidth;
    public readonly int segmentDepth;
    public readonly float stepX;
    public readonly float stepZ;
    public readonly Color color;

    public PlanePoly(): this(10, 10, 0.1f, 0.1f, Color.white) {}

    public PlanePoly(
        int segmentWidth,
        int segmentDepth,
        float width,
        float depth,
        Color color)
    {
        this.segmentWidth = segmentWidth;
        this.segmentDepth= segmentDepth;

        this.stepX = width / segmentWidth;
        this.stepZ = depth / segmentDepth;

        this.color = color;

        verts = new List<Vector3>();
        tris = new List<int>();

        offset = new Vector3(segmentWidth * stepX / 2f, 0, segmentDepth * stepZ / 2f);

        for (int x = 0; x <= segmentWidth; x++)
        {
            for (int z = 0; z <= segmentDepth; z++)
            {
                float sX = x * stepX;
                float sZ = z * stepZ;

                verts.Add(new Vector3(sX, 0, sZ) - offset);
            }
        }

        for (int x = 0; x < segmentWidth; x++)
        {
            for (int z = 0; z < segmentDepth; z++)
            {
                var v0 = coordToIndex(segmentDepth, x, z);
                var v1 = coordToIndex(segmentDepth, x, z + 1);
                var v2 = coordToIndex(segmentDepth, x + 1, z);
                var v3 = coordToIndex(segmentDepth, x + 1, z + 1);

                tris.Add(v0);
                tris.Add(v1);
                tris.Add(v2);
                tris.Add(v1);
                tris.Add(v3);
                tris.Add(v2);
            }
        }
    }

    public void PerlinHeights(
        Vector2 origin,
        float amplitude,
        float widthStep,
        float heightStep)
    {
        for (int x = 0; x <= segmentWidth; x++)
        {
            for (int z = 0; z <= segmentDepth; z++)
            {
                var sampleX = origin.x + x * widthStep;
                var sampleZ = origin.y + z * heightStep;

                var heightOffset = 
                    Mathf.PerlinNoise(sampleX, sampleZ) * amplitude;

                var index = coordToIndex(segmentDepth, x, z);

                verts[index] = 
                    new Vector3(
                        verts[index].x,
                        verts[index].y + heightOffset,
                        verts[index].z
                    );
            }
        }
    }

    public void SineHeights(float amplitude, float frequency)
    {
        for (int x = 0; x <= segmentWidth; x++)
        {
            for (int z = 0; z <= segmentDepth; z++)
            {
                var sampleZ = z * frequency;
                var sampleX = x * frequency / 4;
                var heightOffset = 
                    0.25f * amplitude * Mathf.Sin(sampleZ) + 
                    0.75f * amplitude * Mathf.Sin(sampleX);
                var index = coordToIndex(segmentDepth, x, z);

                verts[index] =
                    new Vector3(
                        verts[index].x,
                        verts[index].y + heightOffset,
                        verts[index].z
                    );
            }
        }
    }

    public static int coordToIndex(int depth, int x, int z)
    {
        return x * (depth + 1) + z;
    }
}

public class CausticGenerator : EditorWindow
{
    private int _time;

    private PlanePolyArgs _waterArgs;
    private PlanePolyArgs _terrainArgs;

    [MenuItem("Tools/Caustics")]
    public static void ShowMenu()
    {
        EditorWindow wnd = GetWindow<CausticGenerator>();
        wnd.titleContent = new GUIContent("Caustic Generator");

        wnd.minSize = new Vector2(450, 300);
        wnd.maxSize = new Vector2(1920, 1080);
    }

    public void CreateGUI()
    {
        _waterArgs = new PlanePolyArgs(
            new Vector3(0, 6, 0),
            200, 200, 2.25f, 2.25f, Color.blue, 
            new PerlinArgs[]
            {
                new PerlinArgs(new Vector2(0, 0), 0.25f, 0.03f, 0.03f),
                new PerlinArgs(new Vector2(100, 100), 0.035f, 0.1f, 0.1f),
            }
        );
        _terrainArgs = new PlanePolyArgs(
            new Vector3(0, 5.5f, 0),
            200, 200, 2f, 2f, Color.white,
            new PerlinArgs[0]
        );

        var generateButton = new Button(onGenerate);
        generateButton.text = "Generate";
        rootVisualElement.Add(generateButton);

        var castLightButton = new Button(onCastLight);
        castLightButton.text = "Cast Light";
        rootVisualElement.Add(castLightButton);

        var clearButton = new Button(onClear);
        clearButton.text = "Clear";
        rootVisualElement.Add(clearButton);
    }

    private void onClear()
    {
        var water = GameObject.Find("water");
        if (water)
        {
            DestroyImmediate(water);
        }

        var terrain = GameObject.Find("terrain");
        if (terrain)
        {
            DestroyImmediate(terrain);
        }
    }

    private GameObject createOrFindPlane(string name, PlanePolyArgs args)
    {
        var obj = GameObject.Find(name);
        if (!obj)
        {
            return createPlaneObjFromArgs(name, args);
        }

        var planeBeh = obj.GetComponent<CausticPlaneBehaviour>();
        if (!planeBeh || planeBeh.PolyDef == null)
        {
            DestroyImmediate(obj);
            return createPlaneObjFromArgs(name, args);
        }

        return obj;
    }

    private GameObject createPlaneObjFromArgs(string name, PlanePolyArgs args)
    {
        var newPlane = new PlanePoly(args.segmentWidth, args.segmentDepth, args.width, args.depth, args.color);
        foreach (var step in args.perlinSteps)
        {
            newPlane.PerlinHeights(step.origin, step.amplitude, step.widthStep, step.heightStep);
        }

        return createPlaneObj(newPlane, name, args.position);
    }

    private GameObject createPlaneObj(PlanePoly plane, string name, Vector3 position)
    {
        var obj = new GameObject();
        obj.name = name;

        var meshFilter = obj.AddComponent<MeshFilter>();
        var renderer = obj.AddComponent<MeshRenderer>();

        var mesh = new Mesh();
        meshFilter.mesh = mesh;

        mesh.Clear();
        mesh.vertices = plane.verts.ToArray();
        mesh.triangles = plane.tris.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();

        var material = new Material(Shader.Find("Diffuse"));
        material.color = plane.color;
        renderer.material = material;

        obj.AddComponent<MeshCollider>();

        var planeBeh = obj.AddComponent<CausticPlaneBehaviour>();
        planeBeh.PolyDef = plane;

        obj.transform.position = position;

        return obj;
    }

    private void onCastLight()
    {
        var waterSurface = createOrFindPlane("water", _waterArgs);
        var terrainSurface = createOrFindPlane("terrain", _terrainArgs);

        if (!waterSurface || !terrainSurface)
        {
            Debug.Log("missing surfaces");
            return;
        }

        var waterPlaneBeh = waterSurface.GetComponent<CausticPlaneBehaviour>();
        var terrainPlaneBeh = terrainSurface.GetComponent<CausticPlaneBehaviour>();

        if (!waterPlaneBeh || waterPlaneBeh.PolyDef == null || !terrainPlaneBeh || terrainPlaneBeh.PolyDef == null)
        {
            Debug.Log("missing planes");
            return;
        }

        int res = 512;

        var litPoints = new List<Vector3>();
        for (int p = 0; p < 5; p++)
        {
            var litSpots = castLight(
                res,
                res,
                waterPlaneBeh.PolyDef,
                terrainPlaneBeh.PolyDef,
                waterSurface,
                terrainSurface,
                new Vector3(
                    UnityEngine.Random.Range(0.0f, 0.1f),
                    0,
                    UnityEngine.Random.Range(0.0f, 0.1f)
                )
            );

            litPoints.AddRange(litSpots);
        }

        Texture2D texture = new Texture2D(res, res, TextureFormat.RGBA32, false);

        var baseColor = new Color(0.1f, 0.1f, 0.1f, 1.0f);
        var colors = texture.GetPixels();
        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = baseColor;
        }
        texture.SetPixels(colors);
        texture.Apply();

        foreach (var point in litPoints ) {
            int x = (int)Mathf.Clamp(point.x, 0f, res);
            int y = (int)Mathf.Clamp(point.z, 0f, res);

            Color color = texture.GetPixel(x, y);

            var intensity = Mathf.Clamp(color.r + 0.1f, 0f, 1.0f);
            texture.SetPixel(
                x, y, 
                new Color(
                    intensity, intensity, intensity, 1.0f
                )
            );
        }

        texture.Apply();

        var blurred = blurTexture(texture, 5);
        byte[] bytes = ImageConversion.EncodeToPNG(blurred);

        DestroyImmediate(texture);
        DestroyImmediate(blurred);

        var path = Application.dataPath + $"/../gen-output/caustic{_time}.png";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, bytes);
    }

    private Texture2D blurTexture(Texture2D image, int blurSize)
    {
        Texture2D blurred = new Texture2D(image.width, image.height);

        // look at every pixel in the blur rectangle
        for (int xx = 0; xx < image.width; xx++)
        {
            for (int yy = 0; yy < image.height; yy++)
            {
                float avgR = 0, avgG = 0, avgB = 0, avgA = 0;
                int blurPixelCount = 0;

                // average the color of the red, green and blue for each pixel in the
                // blur size while making sure you don't go outside the image bounds
                for (int x = xx; (x < xx + blurSize && x < image.width) ; x++)
                {
                    for (int y = yy; (y < yy + blurSize && y < image.height) ; y++)
                    {
                        Color pixel = image.GetPixel(x, y);

                        avgR += pixel.r;
                        avgG += pixel.g;
                        avgB += pixel.b;
                        avgA += pixel.a;

                        blurPixelCount++;
                    }
                }

                avgR = avgR / blurPixelCount;
                avgG = avgG / blurPixelCount;
                avgB = avgB / blurPixelCount;
                avgA = avgA / blurPixelCount;

                // now that we know the average for the blur size, set each pixel to that color
                for (int x = xx; x < xx + blurSize &&  x < image.width; x++)
                {
                    for (int y = yy; y < yy + blurSize && y < image.height; y++)
                    {
                        blurred.SetPixel(x, y, new Color(avgR, avgG, avgB, avgA));
                    }
                }
            }
        }

        blurred.Apply();
        return blurred;
    }

    private List<Vector3> castLight(
        int width, 
        int height,
        PlanePoly waterPoly,
        PlanePoly terrainPoly,
        GameObject water,
        GameObject terrain,
        Vector3 shift)
    {
        float xStep = waterPoly.offset.x * 2f / width;
        float yStep = waterPoly.offset.z * 2f / height;

        var waterCollider = water.GetComponent<MeshCollider>();
        var terrainCollider = terrain.GetComponent<MeshCollider>();

        float refAir = 1.0003f;
        float refWater = 1.33f;

        int hitCount = 0;
        int refHitCount = 0;

        var lightPoints = new List<Vector3>();

        var rayDist = water.transform.position.y - terrain.transform.position.y + 1f;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float sX = x * xStep;
                float sZ = (height - y) * yStep;

                var ray = new Ray(
                    water.transform.position + new Vector3(sX, 1f, sZ) - waterPoly.offset + shift,
                    Vector3.down
                );

                RaycastHit hit;
                if (waterCollider.Raycast(ray, out hit, rayDist))
                {
                    hitCount++;

                    var incidentPos = hit.point + ray.direction * 0.001f;

                    var n1 = refAir;
                    var n2 = refWater;

                    var norm = hit.normal;
                    var incident = ray.direction.normalized;

                    Vector3 refracted = 
                        (n1 / n2 * 
                            Vector3.Cross(norm, Vector3.Cross(-norm, incident)) - 
                            norm * Mathf.Sqrt(
                                1 - Vector3.Dot(
                                    Vector3.Cross(norm, incident) * (n1 / n2 * n1 / n2),
                                    Vector3.Cross(norm, incident)
                                )
                            )
                        ).normalized;

                    var refRay = new Ray(incidentPos, refracted);
                    // Debug.DrawRay(refRay.origin, refRay.direction * 1.33f, Color.yellow, 5);

                    if (terrainCollider.Raycast(refRay, out hit, rayDist))
                    {
                        if (terrain.transform.name != hit.transform.name)
                        {
                            continue;
                        }

                        refHitCount++;

                        // Debug.DrawRay(hit.point + new Vector3(0, 0.05f, 0), Vector3.down * 0.1f, Color.red, 5);

                        var offsetPoint = hit.point - terrain.transform.position + terrainPoly.offset;
                        var lightPoint = 
                            new Vector3(
                                Mathf.Round(offsetPoint.x / (terrainPoly.offset.x * 2) * width), 
                                0.0f,
                                Mathf.Round(offsetPoint.z / (terrainPoly.offset.z * 2) * height)
                            );

                        lightPoints.Add(lightPoint);
                    }
                }
            }
        }

        return lightPoints;
    }

    private void onGenerate()
    {
        createOrFindPlane("water", _waterArgs);
        createOrFindPlane("terrain", _terrainArgs);
    }
}
