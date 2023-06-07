using System;
using System.Collections;
using System.Collections.Generic;
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

    public PlanePoly(): this(10, 10, 0.1f) {}

    public PlanePoly(int width, int depth, float step)
    {
        this.width = width;
        this.depth= depth;
        verts = new List<Vector3>();
        tris = new List<int>();

        offset = new Vector3(width * step / 2f, 0, depth * step / 2f);

        for (int x = 0; x <= width; x++)
        {
            for (int z = 0; z <= depth; z++)
            {
                float sX = x * step;
                float sZ = z * step;

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

    public void RandomizeHeights()
    {
        for (int x = 0; x <= width; x++)
        {
            for (int z = 0; z <= depth; z++)
            {
                var heightOffset = UnityEngine.Random.Range(0.0f, 0.1f);
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
        if (!_waterSurface || !_terrainSurface || _waterPlane == null)
        {
            return;
        }

        castLight(32, 32, _waterPlane, _waterSurface);
    }

    private void castLight(int width, int height, PlanePoly poly, GameObject water)
    {
        float xStep = poly.offset.x * 2f / width;
        float yStep = poly.offset.z * 2f / height;

        var waterCollider = water.GetComponent<MeshCollider>();

        float refAir = 1.0003f;
        float refWater = 1.33f;

        int hitCount = 0;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                float sX = x * xStep;
                float sZ = (height - y) * yStep;

                var ray = new Ray(
                    water.transform.position + new Vector3(sX, 1f, sZ) - poly.offset,
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

                    Vector3 refracted = (n1 / n2 * Vector3.Cross(norm, Vector3.Cross(-norm, incident)) - norm * Mathf.Sqrt(1 - Vector3.Dot(Vector3.Cross(norm, incident) * (n1 / n2 * n1 / n2), Vector3.Cross(norm, incident)))).normalized;

                    var refRay = new Ray(incidentPos, refracted);
                    Debug.DrawRay(refRay.origin, refRay.direction * 1.33f, Color.yellow, 5);
                }
            }
        }

        Debug.Log($"{hitCount} / {width * height}");
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

        int width = 10;
        int height = 10;
        float step = 0.1f;

        _waterPlane = new PlanePoly(width, height, step);
        _waterPlane.RandomizeHeights();

        _waterSurface = createPlane(_waterPlane, "water", Color.blue);
        _waterSurface.transform.position = new Vector3(0, 6, 0);

        var terrainPlane = new PlanePoly(width, height, step);
        _terrainSurface = createPlane(terrainPlane, "terrain", Color.grey);
        _terrainSurface.transform.position = new Vector3(0, 5, 0);
    }
}
