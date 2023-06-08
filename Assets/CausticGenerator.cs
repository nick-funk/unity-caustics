using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using TreeEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class PlanePoly
{
    public readonly List<Vector3> verts;
    public readonly List<int> tris;
    public readonly Vector3 offset;
    public readonly int width;
    public readonly int depth;

    public PlanePoly(): this(10, 10, 0.1f, 0.1f) {}

    public PlanePoly(int width, int depth, float stepX, float stepY)
    {
        this.width = width;
        this.depth= depth;
        verts = new List<Vector3>();
        tris = new List<int>();

        offset = new Vector3(width * stepX / 2f, 0, depth * stepY / 2f);

        for (int x = 0; x <= width; x++)
        {
            for (int z = 0; z <= depth; z++)
            {
                float sX = x * stepX;
                float sZ = z * stepY;

                verts.Add(new Vector3(sX, 0, sZ) - offset);
            }
        }

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                var v0 = coordToIndex(depth, x, z);
                var v1 = coordToIndex(depth, x, z + 1);
                var v2 = coordToIndex(depth, x + 1, z);
                var v3 = coordToIndex(depth, x + 1, z + 1);

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
        float scale,
        float sampleWidth,
        float sampleHeight)
    {
        for (int x = 0; x <= width; x++)
        {
            for (int z = 0; z <= depth; z++)
            {
                var sampleX = origin.x + x * sampleWidth;
                var sampleZ = origin.y + z * sampleHeight;

                var heightOffset = 
                    Mathf.PerlinNoise(sampleX, sampleZ) * scale;

                var index = coordToIndex(depth, x, z);

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
        for (int x = 0; x <= width; x++)
        {
            for (int z = 0; z <= depth; z++)
            {
                var sampleZ = z * frequency;
                var sampleX = x * frequency / 4;
                var heightOffset = 
                    0.25f * amplitude * Mathf.Sin(sampleZ) + 
                    0.75f * amplitude * Mathf.Sin(sampleX);
                var index = coordToIndex(depth, x, z);

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
    private GameObject _waterSurface;
    private GameObject _terrainSurface;
    private PlanePoly _waterPlane;
    private PlanePoly _terrainPlane;

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
        var generateButton = new Button(onGenerate);
        generateButton.text = "Generate";
        rootVisualElement.Add(generateButton);

        var castLightButton = new Button(onCastLight);
        castLightButton.text = "Cast Light";
        rootVisualElement.Add(castLightButton);
    }

    private GameObject createPlane(PlanePoly plane, string name, Color color)
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
        material.color = color;
        renderer.material = material;

        obj.AddComponent<MeshCollider>();

        return obj;
    }

    private void onCastLight()
    {
        if (!_waterSurface || !_terrainSurface || _waterPlane == null || _terrainPlane == null)
        {
            Debug.Log("missing surfaces and/or planes");
            return;
        }

        int res = 512;

        var litPoints = new List<Vector3>();
        for (int p = 0; p < 3; p++)
        {
            var litSpots = castLight(
                res,
                res,
                _waterPlane,
                _terrainPlane,
                _waterSurface,
                _terrainSurface,
                new Vector3(
                    UnityEngine.Random.Range(0.0f, 0.001f),
                    0,
                    UnityEngine.Random.Range(0.0f, 0.001f)
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

        var blurred = blurTexture(texture, 8);
        byte[] bytes = ImageConversion.EncodeToPNG(blurred);

        DestroyImmediate(texture);
        DestroyImmediate(blurred);

        var path = Application.dataPath + "/../gen-output/caustic.png";
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
                if (waterCollider.Raycast(ray, out hit, 2f))
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

                    if (terrainCollider.Raycast(refRay, out hit, 3f))
                    {
                        if (terrain.transform.name != hit.transform.name)
                        {
                            continue;
                        }

                        refHitCount++;

                        Debug.DrawRay(hit.point + new Vector3(0, 0.05f, 0), Vector3.down * 0.1f, Color.red, 5);

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
        if (_waterSurface)
        {
            DestroyImmediate(_waterSurface);
        }
        if (_terrainSurface)
        {
            DestroyImmediate(_terrainSurface);
        }

        int widthSegments = 200;
        int heightSegments = 200;

        Vector2 size = new Vector2(4f, 4f);
        float stepX = size.x / widthSegments;
        float stepY = size.y / heightSegments;

        _waterPlane = new PlanePoly(widthSegments, heightSegments, stepX, stepY);
        _waterPlane.PerlinHeights(new Vector2(0, 0), 0.25f, 0.0325f, 0.0325f);
        _waterPlane.PerlinHeights(new Vector2(100, 100), 0.035f, 0.1f, 0.1f);

        _waterSurface = createPlane(_waterPlane, "water", Color.blue);
        _waterSurface.transform.position = new Vector3(0, 6, 0);

        Vector2 terrainSize = new Vector2(2f, 2f);
        float terrainStepX = terrainSize.x / widthSegments;
        float terrainStepY = terrainSize.y / heightSegments;

        _terrainPlane = new PlanePoly(widthSegments, heightSegments, terrainStepX, terrainStepY);
        _terrainSurface = createPlane(_terrainPlane, "terrain", Color.grey);
        _terrainSurface.transform.position = new Vector3(0, 5, 0);
    }
}
