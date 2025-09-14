/** @author Czajkowski Andrzej a.czajkowski@issi.uz.zgora.pl
 *   On-Device 3D Point Cloud Segmentation for Mixed Reality Applications
 *   License GPL-2.0 license 
 *   https://github.com/aczajkowskiuz/3dpointcloudsegmentation
 *  @remarks How to use: add script to any empty gameopbject in Unity (offline version with real data from depth sensor)
 *  put json files from github in your persitance folder typically C:\Users\USERNAME\AppData\LocalLow\CompanyName\ProjectName
 *  script still needs some refactoring and cleaning so it may change in next days
 */



using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Color = UnityEngine.Color;

[System.Serializable]
public class Vector3ArrayWrapper
{
    public Vector3[] vectors;
}
public class IntegersArrayWrapper
{
    public int[] ints;
}
public class BoundingBoxGenerator : MonoBehaviour
{
    LineRenderer borderlineRenderer;
    public GameObject AreaToCheck;

    /*  forreal hardware testing and when WaveSDK installed in Unity then uncomment
     *  private ScenePerceptionMeshFacade _scenePerceptionMeshFacade;
        public GameObject scannedRoomGO = null;
    */

    private LineRenderer lineRenderer;
    private Bounds meshBounds;
    void Start()
    {
        MeshFilter meshFilter = gameObject.AddComponent<MeshFilter>();

        MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));

        /* forreal hardware testing and when WaveSDK installed in Unity then uncomment
        List<GeneratedSceneMesh> generatedSceneMeshes = _scenePerceptionMeshFacade.GetMeshContainer().GetMeshesFromContainer();

        foreach (GeneratedSceneMesh tmpOBJ in generatedSceneMeshes)
        {
            scannedRoomGO = tmpOBJ.go;
        }
        Mesh mesh = scannedRoomGO.GetComponent<MeshFilter>().mesh;
        */

        //      SaveMesh(mesh); switch between load and save when pc// vive 

        Mesh mesh = LoadMesh(); // use loaded mesh on Vive
        OptimizeMesh(mesh);
        mesh.RecalculateNormals();
        meshFilter.mesh = mesh;


        borderlineRenderer = gameObject.AddComponent<LineRenderer>();
        borderlineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        borderlineRenderer.startColor = Color.red;
        borderlineRenderer.endColor = Color.green;
        borderlineRenderer.startWidth = 0.005f;
        borderlineRenderer.endWidth = 0.005f;


        int[] triangles = mesh.triangles;
        Vector3[] vertices = mesh.vertices;

        Dictionary<int, Vector3> vertOne = new Dictionary<int, Vector3>();
        Dictionary<int, Vector3> vertTwo = new Dictionary<int, Vector3>();
        Dictionary<int, Vector3> vertThree = new Dictionary<int, Vector3>();

        for (int i = 0; i < triangles.Length; i += 3)
        {
            vertOne.Add(i, vertices[triangles[i]]);
            vertTwo.Add(i, vertices[triangles[i + 1]]);
            vertThree.Add(i, vertices[triangles[i + 2]]);
        }

        var lookupOne = vertOne.ToLookup(a => a.Value);
        var lookupTwo = vertTwo.ToLookup(a => a.Value);
        var lookupThree = vertThree.ToLookup(a => a.Value);

        for (int i = 0; i < vertices.Length; i++)
        {
            HashSet<Vector3> candVerts = new HashSet<Vector3>();

            var resultOne = lookupOne[vertices[i]];

            foreach (var item in resultOne)
            {
                int triangleIdx = item.Key;
                candVerts.Add(vertices[triangles[triangleIdx]]);
                candVerts.Add(vertices[triangles[triangleIdx + 1]]);
                candVerts.Add(vertices[triangles[triangleIdx + 2]]);
            }
            var resultTwo = lookupTwo[vertices[i]];
            foreach (var item in resultTwo)
            {
                int triangleIdx = item.Key;
                candVerts.Add(vertices[triangles[triangleIdx]]);
                candVerts.Add(vertices[triangles[triangleIdx + 1]]);
                candVerts.Add(vertices[triangles[triangleIdx + 2]]);
            }

            var resultThree = lookupThree[vertices[i]];
            foreach (var item in resultThree)
            {
                int triangleIdx = item.Key;
                candVerts.Add(vertices[triangles[triangleIdx]]);
                candVerts.Add(vertices[triangles[triangleIdx + 1]]);
                candVerts.Add(vertices[triangles[triangleIdx + 2]]);
            }

            if (candVerts.Count > 3)
            {
                candVerts.Remove(vertices[i]);

                Vector3 tmpVert = Vector3.zero;
                foreach (var item in candVerts)
                {
                    tmpVert += item;
                }
                tmpVert /= candVerts.Count;
                vertices[i] = tmpVert;
            }
        }
        mesh.vertices = vertices;

        createPlanes(vertices, triangles);


        // create walls

        float minX = 0, minY = 0, minZ = 0, maxX = 0, maxY = 0, maxZ = 0;
        for (int i = 0; i < vertices.Length; i++)
        {
            if (i == 0)
            {
                minX = vertices[i].x;
                minY = vertices[i].y;
                minZ = vertices[i].z;
                maxX = vertices[i].x;
                maxY = vertices[i].y;
                maxZ = vertices[i].z;
            }
            else
            {
                if (vertices[i].x < minX) minX = vertices[i].x;
                if (vertices[i].y < minY) minY = vertices[i].y;
                if (vertices[i].z < minZ) minZ = vertices[i].z;
                if (vertices[i].x > maxX) maxX = vertices[i].x;
                if (vertices[i].y > maxY) maxY = vertices[i].y;
                if (vertices[i].z > maxZ) maxZ = vertices[i].z;
            }
        }

        borderlineRenderer.positionCount = 10;

        borderlineRenderer.SetPosition(0, new Vector3(minX, minY, minZ));
        borderlineRenderer.SetPosition(1, new Vector3(minX, minY, maxZ));
        borderlineRenderer.SetPosition(2, new Vector3(maxX, minY, maxZ));
        borderlineRenderer.SetPosition(3, new Vector3(maxX, minY, minZ));
        borderlineRenderer.SetPosition(4, new Vector3(minX, minY, minZ));

        borderlineRenderer.SetPosition(5, new Vector3(minX, maxY, minZ));
        borderlineRenderer.SetPosition(6, new Vector3(minX, maxY, maxZ));
        borderlineRenderer.SetPosition(7, new Vector3(maxX, maxY, maxZ));
        borderlineRenderer.SetPosition(8, new Vector3(maxX, maxY, minZ));
        borderlineRenderer.SetPosition(9, new Vector3(minX, maxY, minZ));
    }

    private void createPlanes(Vector3[] vertices, int[] triangles)
    {
        List<HashSet<Vector3>> detectedPlaneList = new List<HashSet<Vector3>>();

        Dictionary<int, Vector3> vertsInTris = new Dictionary<int, Vector3>();

        for (int i = 0; i < triangles.Length; i++)
        {

            vertsInTris.Add(i, vertices[triangles[i]]);
        }
        var lookup = vertsInTris.ToLookup(a => a.Value);

        HashSet<int> checkedTriangles = new HashSet<int>();



        for (int i = 0; i < triangles.Length; i += 3)
        {
            if (!checkedTriangles.Contains(i))
            {
                Vector3 p1First = vertices[triangles[i]];
                Vector3 p2First = vertices[triangles[i + 1]];
                Vector3 p3First = vertices[triangles[i + 2]];

                Vector3 normalFirstTris = Vector3.Cross(p2First - p1First, p3First - p1First);

                Vector3 n1 = Vector3.Normalize(normalFirstTris);

                HashSet<int> trianglesToCheck = new HashSet<int>();

                trianglesToCheck.Add(i);

                HashSet<Vector3> vertsInPlane = new HashSet<Vector3>();

                while (trianglesToCheck.Count > 0)
                {
                    int item = trianglesToCheck.First();
                    trianglesToCheck.Remove(item);
                    checkedTriangles.Add(item);
                    Vector3 p1 = vertices[triangles[item]];
                    Vector3 p2 = vertices[triangles[item + 1]];
                    Vector3 p3 = vertices[triangles[item + 2]];
                    Vector3[] pointsArray = { p1, p2, p3 };


                    Vector3 normal = Vector3.Cross(p2 - p1, p3 - p1);
                    Vector3 n2 = Vector3.Normalize(normal);
                    float dot = Vector3.Dot(n1, n2);
                    float tolerance = 0.99f; // adaptacyjnie ?
                    if (dot >= tolerance)
                    {
                        vertsInPlane.Add(p1);
                        vertsInPlane.Add(p2);
                        vertsInPlane.Add(p3);

                        // jeśli jest na plaszczyznie dodajemy sasiednie trojkaty do sprwadzenia wybierane po jego wierzcholkach

                        foreach (Vector3 point in pointsArray)
                        {
                            var result = lookup[point]; // dostaje trojkaty wspoldzielace dany point 

                            foreach (var tris in result)
                            {
                                int triangleIdx = tris.Key;
                                triangleIdx = triangleIdx - triangleIdx % 3;
                                if (!checkedTriangles.Contains(triangleIdx)) trianglesToCheck.Add(triangleIdx);
                            }
                        }
                    }              
                }
                detectedPlaneList.Add(vertsInPlane);
            }
        }
        int counter = 0;

        List<Mesh> simplifiedPlanes = new List<Mesh>();

        foreach (var vertsInPlaneList in detectedPlaneList)
        {
            if (vertsInPlaneList.Count == 0) continue;



           
            // Build vertices
            Vector3[] array = GetSyntheticQuad(vertsInPlaneList.ToList());
            Vector3 a = array[0];
            Vector3 corner1 = array[1];
            Vector3 corner2 = array[2];
            Vector3 b = array[3];

            Vector3[] verticesNew = new Vector3[4];
            verticesNew[0] = a;
            verticesNew[1] = corner1;
            verticesNew[2] = b;
            verticesNew[3] = corner2;

            int[] trianglesNew = { 0, 2, 1, 0, 2, 3 };
            Mesh mesh = new Mesh()
            {

                indexFormat = IndexFormat.UInt32,
                vertices = verticesNew,
                triangles = trianglesNew
            };     

            Vector3 middlePoint = Vector3.zero;
            foreach (Vector3 v in vertsInPlaneList)
            {
                middlePoint += v;
            }
            middlePoint /= vertsInPlaneList.Count;

            Vector3 planeMiddlePoint = (a + b + corner1 + corner2) / 4;

            float distance = Vector3.Distance(planeMiddlePoint, middlePoint);

            if (distance > .4f)
            {


                Vector3 pointA = verticesNew[0];
                Vector3 pointB = verticesNew[1];
                Vector3 pointC = verticesNew[2];
                Vector3 pointD = verticesNew[3];

                Vector3 concaveCorner1 = pointB; // moves towards pointA
                Vector3 otherMovablePoint1 = pointC; // moves towards pointD 

                // mesh two :
                Vector3 concaveCorner2 = pointB; // moves towards pointC
                Vector3 otherMovablePoint2 = pointA; // moves towards pointD 


                Vector3 borderPoint1 = pointA;
                Vector3 borderPoint2 = pointC;

                // founding vertices conditional for stoping
                float maxDist1a = Vector3.Distance(borderPoint1, concaveCorner1);
                float maxDist1b = Vector3.Distance(borderPoint1, pointA);
                float maxDist2a = Vector3.Distance(borderPoint2, concaveCorner2);
                float maxDist2b = Vector3.Distance(borderPoint2, pointC);
                int vertCounter = 0;

                float distToll = 0.1f;

                float minDist1 = Vector3.Distance(pointA, concaveCorner1) + distToll;
                float minDist2 = Vector3.Distance(pointC, concaveCorner2) + distToll;

                foreach (var vert in vertsInPlaneList)
                {
                    float dist1a = Vector3.Distance(vert, concaveCorner1);
                    float dist1b = Vector3.Distance(vert, pointA);

                    float dist2a = Vector3.Distance(vert, concaveCorner2);
                    float dist2b = Vector3.Distance(vert, pointC);


                    if ((dist1a + dist1b) < minDist1)
                    {
                        if (dist1a < maxDist1a)
                        {
                            borderPoint1 = vert;
                            maxDist1a = dist1a;
                            maxDist1b = dist1b;

                            Debug.Log("change vert: " + vertCounter);
                        }
                    }

                    if ((dist2a + dist2b) < minDist2)
                    {
                        if (dist2a < maxDist2a)
                        {
                            borderPoint2 = vert;
                            maxDist2a = dist2a;
                            maxDist2b = dist2b;
                        }
                    }
                }


                float oldDistant1 = Vector3.Distance(borderPoint1, concaveCorner1);
                float oldDistant2 = Vector3.Distance(borderPoint2, concaveCorner2);

                // moving towards
                bool runFlag = true;
                float step = 0.01f;
                while (runFlag)
                {
                    Vector3 concaveCorner1cand = Vector3.MoveTowards(concaveCorner1, pointA, step);
                    Vector3 otherMovablePoint1cand = Vector3.MoveTowards(otherMovablePoint1, pointD, step);

                    float newDistant = Vector3.Distance(borderPoint1, concaveCorner1);
                    if (newDistant > oldDistant1)
                    {
                        runFlag = false;
                    }
                    else
                    {

                        oldDistant1 = newDistant;
                        concaveCorner1 = concaveCorner1cand;
                        otherMovablePoint1 = otherMovablePoint1cand;
                    }
                }
                runFlag = true;
                while (runFlag)
                {
                    Vector3 concaveCorner2cand = Vector3.MoveTowards(concaveCorner2, pointC, step);
                    Vector3 otherMovablePoint2cand = Vector3.MoveTowards(otherMovablePoint2, pointD, step);

                    float newDistant = Vector3.Distance(borderPoint2, concaveCorner2);
                    if (newDistant > oldDistant2)
                    {
                        runFlag = false;
                    }
                    else
                    {

                        concaveCorner2 = concaveCorner2cand;
                        otherMovablePoint2 = otherMovablePoint2cand;
                        oldDistant2 = newDistant;
                    }

                }

                Vector3[] verticesNew1 = new Vector3[4];
                verticesNew1[0] = pointA;
                verticesNew1[1] = concaveCorner1;
                verticesNew1[2] = pointD;
                verticesNew1[3] = otherMovablePoint1;

                int[] trianglesNew1 = { 0, 2, 1, 0, 2, 3 };
                Mesh dividedMesh1 = new Mesh()
                {

                    indexFormat = IndexFormat.UInt32,
                    vertices = verticesNew1,
                    triangles = trianglesNew1
                };
                simplifiedPlanes.Add(dividedMesh1);

                Vector3[] verticesNew2 = new Vector3[4];
                verticesNew2[0] = pointC;
                verticesNew2[1] = concaveCorner2;
                verticesNew2[2] = pointD;
                verticesNew2[3] = otherMovablePoint2;

                int[] trianglesNew2 = { 0, 2, 1, 0, 2, 3 };
                Mesh dividedMesh2 = new Mesh()
                {

                    indexFormat = IndexFormat.UInt32,
                    vertices = verticesNew2,
                    triangles = trianglesNew2
                };
                simplifiedPlanes.Add(dividedMesh2);
            }
            else // nie concave to:
            {
                simplifiedPlanes.Add(mesh);
            }
        }

        HashSet<Mesh> checkedMeshes = new HashSet<Mesh>();

        float combinedTolerance = 0.9f; // adaptacyjnie ? 
        float minDist = 0.3f;
        float minBigSize = 1.0f;
        List<GameObject> cubeList = new List<GameObject>();
        List<GameObject> removeCubeList = new List<GameObject>();

        // check if there are the same facing planes neear each other
        foreach (var currentPlane in simplifiedPlanes)
        {
            List<Vector3> combinedVertices = new List<Vector3>();

            checkedMeshes.Add(currentPlane);

            Vector3[] verts = currentPlane.vertices;
            Vector3 p1 = verts[0];
            Vector3 p2 = verts[1];
            Vector3 p3 = verts[2];
            foreach (var item in verts)
            {
                combinedVertices.Add(item);
            }
            bool isNotBig = true;

            Vector3 size = currentPlane.bounds.size; // dziala bo meshe a nie cuby jak nizej

            if (size.magnitude > minBigSize) isNotBig = false;

            //sprawdzana tylko drobnica
            if (isNotBig)
            {

                Vector3 normal = Vector3.Cross(p2 - p1, p3 - p1); // wystarczy dla pierwszych bo mesh jest planarny
                Vector3 nnormal = Vector3.Normalize(normal);

                foreach (var plane in simplifiedPlanes)
                {
                    if (checkedMeshes.Contains(plane)) { continue; }
                    Vector3[] vertsOther = plane.vertices;
                    Vector3 pp1 = vertsOther[0];
                    Vector3 pp2 = vertsOther[1];
                    Vector3 pp3 = vertsOther[2];

                    Vector3 normalOther = Vector3.Cross(pp2 - pp1, pp3 - pp1);
                    Vector3 nnormalOther = Vector3.Normalize(normalOther);


                    float dot = Vector3.Dot(nnormal, nnormalOther);

                    if (dot >= combinedTolerance)
                    {

                        Vector3 middlePoint = Vector3.zero;
                        foreach (Vector3 v in vertsOther)
                        {
                            middlePoint += v;
                        }
                        middlePoint /= vertsOther.Length;

                        foreach (var item in combinedVertices)
                        {
                            if (Vector3.Distance(middlePoint, item) < minDist)
                            {
                                checkedMeshes.Add(plane);
                                foreach (var vertToCombine in vertsOther)
                                {
                                    combinedVertices.Add(vertToCombine);
                                }
                                break;
                            }
                        }
                    }
                }
                // if (combinedVertices.Count <= 4) continue;
            }
            //   Vector3[] array = GetSyntheticQuad(combinedVertices.ToList());
            Vector3[] array = FindRepresentativeQuad(combinedVertices.ToList());
            Vector3 a = array[0];
            Vector3 corner1 = array[1];
            Vector3 corner2 = array[2];
            Vector3 b = array[3];
            /*
            GameObject planeCombined = GameObject.CreatePrimitive(PrimitiveType.Plane);
            planeCombined.name = "plane" + counterName++;

            MeshRenderer meshRenderer = planeCombined.GetComponent<MeshRenderer>();
            meshRenderer.material = new Material(Shader.Find("Sprites/Default"));
           
            Destroy(planeCombined.GetComponent<MeshCollider>());
            */
            Vector3[] verticesNew = new Vector3[4];
            verticesNew[0] = a;
            verticesNew[1] = corner1;
            verticesNew[2] = b;
            verticesNew[3] = corner2;

            int[] trianglesNew = { 0, 2, 1, 0, 2, 3 };
            Mesh mesh = new Mesh()
            {

                indexFormat = IndexFormat.UInt32,
                vertices = verticesNew,
                triangles = trianglesNew
            };

            // planeCombined.GetComponent<MeshFilter>().mesh = mesh;


            meshBounds = mesh.bounds;
            if (meshBounds.size.magnitude == 0.0f) continue;
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            //       cube.transform.SetParent(transform, false);
            cube.transform.localPosition = meshBounds.center;
            cube.transform.localScale = meshBounds.size;
            cube.name = "cube" + counter;

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            float R = UnityEngine.Random.value;
            float G = UnityEngine.Random.value;
            float B = UnityEngine.Random.value;

            mat.SetColor("_BaseColor", new Color(R, G, B, 0.9f)); // green, 25% opacity
            /*
            // Make it transparent
            mat.SetFloat("_Surface", 1f); // 0 = Opaque, 1 = Transparent
            mat.SetFloat("_Blend", 0f);   // Alpha blending
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;

            // Enable transparency keywords
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            */
            cube.GetComponent<Renderer>().material = mat;
            cubeList.Add(cube);

            counter++;
            //         if (counter >= 10) break;

        }
        // check cubes intersections and remove much smaller one if similiar join them?
        int mainCounter = 0;
        foreach (var cube in cubeList)
        {
            if (removeCubeList.Contains(cube))
            {
                mainCounter++;
                continue;
            }
            MeshFilter cubeMesh = cube.GetComponent<MeshFilter>();

            int secondaryCounter = 0;
            foreach (var otherCube in cubeList)
            {

                if (cube == otherCube)
                {
                    secondaryCounter++;
                    continue;
                }
                if (removeCubeList.Contains(otherCube))
                {
                    secondaryCounter++;
                    continue;
                }
                MeshFilter otherCubeMesh = otherCube.GetComponent<MeshFilter>();
                if (CheckCollision(cubeMesh, otherCubeMesh))
                {
              
                    if (cubeMesh.transform.localScale.magnitude > minBigSize && otherCubeMesh.transform.localScale.magnitude > minBigSize) continue;
                    if (cubeMesh.transform.localScale.magnitude > otherCubeMesh.transform.localScale.magnitude)
                    {
                        removeCubeList.Add(otherCube);
                    }
                    else
                    {
                        removeCubeList.Add(cube);
                        break;
                    }
                }
                secondaryCounter++;
            }
            mainCounter++;

        }
        foreach (var cube in removeCubeList)
        {
            cubeList.Remove(cube);
            Destroy(cube);
        }
        // clasterisation of small cubes
        float clasterRange = 0.3f;
        float maxSizeInClaster = 0.5f;

        HashSet<GameObject> checkedCubes = new HashSet<GameObject>();
        int clasterisationCounter = 0;
        foreach (var cube in cubeList)
        {
            if (checkedCubes.Contains(cube)) { continue; }

            MeshFilter mfCube = cube.GetComponent<MeshFilter>();
            if (mfCube.transform.localScale.magnitude > maxSizeInClaster) continue;

            int cubeCount = 1;
            // struktura na wiercholki
            HashSet<Vector3> clasteredVerts = new HashSet<Vector3>();


            Vector3 clasterCenter = cube.transform.position;

            foreach (var vert in mfCube.mesh.vertices)
            {
                clasteredVerts.Add(cube.transform.TransformPoint(vert));
            }

            //if cube duzy to continue

            // petla wyszukjaca
            bool nothingAdded = false;
            int cubesInClaster = 1;
            //      int whileCounter = 0;
            while (!nothingAdded)
            {
                nothingAdded = true;


                foreach (var otherCube in cubeList)
                {

                    if (cube == otherCube) continue;
                    if (checkedCubes.Contains(otherCube)) { continue; }
                    if (otherCube.transform.localScale.magnitude > maxSizeInClaster) continue;

                    Vector3 otherCubePosition = otherCube.transform.position;
                    if (Vector3.Distance(otherCubePosition, clasterCenter / cubesInClaster) < clasterRange)
                    {
                        if (!checkedCubes.Contains(cube))
                        {
                            checkedCubes.Add(cube);
                            clasterisationCounter++;
                        }
                        if (!checkedCubes.Contains(otherCube)) { checkedCubes.Add(otherCube); }

                        MeshFilter mfOtherCube = otherCube.GetComponent<MeshFilter>();
                        foreach (var vert in mfOtherCube.mesh.vertices)
                        {
                            clasteredVerts.Add(otherCube.transform.TransformPoint(vert));
                          
                        }
                        nothingAdded = false;
                        cubeCount++;
                        clasterCenter += otherCubePosition;
                        cubesInClaster++;
                    
                    }
                }
            }
            if (cubeCount > 1)
            {
                Vector3[] array = FindRepresentativeQuad(clasteredVerts.ToList());
                Vector3 a = array[0];
                Vector3 corner1 = array[1];
                Vector3 corner2 = array[2];
                Vector3 b = array[3];


                Vector3[] verticesNew = new Vector3[4];
                verticesNew[0] = a;
                verticesNew[1] = corner1;
                verticesNew[2] = b;
                verticesNew[3] = corner2;

                int[] trianglesNew = { 0, 2, 1, 0, 2, 3 };
                Mesh mesh = new Mesh()
                {

                    indexFormat = IndexFormat.UInt32,
                    vertices = verticesNew,
                    triangles = trianglesNew
                };

                meshBounds = mesh.bounds;

                GameObject combinedCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                combinedCube.transform.localPosition = meshBounds.center;
                combinedCube.transform.localScale = meshBounds.size;
                combinedCube.name = "combinedCube";
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                float R = UnityEngine.Random.value;
                float G = UnityEngine.Random.value;
                float B = UnityEngine.Random.value;

                mat.SetColor("_BaseColor", new Color(R, G, B, 0.9f)); // green, 25% opacity

                // Make it transparent
                mat.SetFloat("_Surface", 1f); // 0 = Opaque, 1 = Transparent
                mat.SetFloat("_Blend", 0f);   // Alpha blending
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)RenderQueue.Transparent;

                // Enable transparency keywords
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");

                combinedCube.GetComponent<Renderer>().material = mat;
            }

            //       if (clasterisationCounter>=6) break;
        }
        foreach (var item in checkedCubes)
        {
            Destroy(item.gameObject);
        }

    }

    // Update is called once per frame
    void Update()
    {

    }
    private void OptimizeMesh(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        Dictionary<Vector3, int> vertDict = new Dictionary<Vector3, int>();

        for (int i = 0, index = 0; i < vertices.Length; i++)
        {
            if (!vertDict.ContainsKey(vertices[i]))
            {
                vertDict.Add(vertices[i], index++);
            }
        }
        Vector3[] newVertices = vertDict.Keys.ToArray<Vector3>();


        for (int i = 0; i < triangles.Length; i++)
        {
            triangles[i] = vertDict[vertices[triangles[i]]];
        }
        mesh.triangles = triangles;
        mesh.vertices = newVertices;
    }
    void SaveMesh(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        string filePathVerts = Path.Combine(Application.persistentDataPath, "vertices.json");
        string filePathTris = Path.Combine(Application.persistentDataPath, "triangles.json");

        Vector3ArrayWrapper wrapperVerts = new Vector3ArrayWrapper { vectors = vertices };
        string jsonVerts = JsonUtility.ToJson(wrapperVerts, true);
        File.WriteAllText(filePathVerts, jsonVerts);

        IntegersArrayWrapper wrapperTris = new IntegersArrayWrapper { ints = triangles };
        string jsonTris = JsonUtility.ToJson(wrapperTris, true);
        File.WriteAllText(filePathTris, jsonTris);

    }

    Mesh LoadMesh()
    {


        string filePathVerts = Path.Combine(Application.persistentDataPath, "verticesVive.json");
        string filePathTris = Path.Combine(Application.persistentDataPath, "trianglesVive.json");

        string jsonVerts = File.ReadAllText(filePathVerts);
        Vector3ArrayWrapper wrapperVerts = JsonUtility.FromJson<Vector3ArrayWrapper>(jsonVerts);

        string jsonTris = File.ReadAllText(filePathTris);
        IntegersArrayWrapper wrapperTris = JsonUtility.FromJson<IntegersArrayWrapper>(jsonTris);


        Mesh mesh =
      new()
      {
          indexFormat = IndexFormat.UInt32,
          vertices = wrapperVerts.vectors,
          triangles = wrapperTris.ints
      };
        return mesh;
    }
    public static Vector3[] GetSyntheticQuad(List<Vector3> vertices)
    {
        // Step 1: Centroid
        Vector3 centroid = Vector3.zero;
        foreach (var vert in vertices) centroid += vert;
        centroid /= vertices.Count;

        // Step 2: Covariance matrix
        float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
        foreach (var vert in vertices)
        {
            Vector3 d = vert - centroid;
            xx += d.x * d.x;
            xy += d.x * d.y;
            xz += d.x * d.z;
            yy += d.y * d.y;
            yz += d.y * d.z;
            zz += d.z * d.z;
        }

        Matrix4x4 cov = new Matrix4x4();
        cov[0, 0] = xx; cov[0, 1] = xy; cov[0, 2] = xz; cov[0, 3] = 0;
        cov[1, 0] = xy; cov[1, 1] = yy; cov[1, 2] = yz; cov[1, 3] = 0;
        cov[2, 0] = xz; cov[2, 1] = yz; cov[2, 2] = zz; cov[2, 3] = 0;
        cov[3, 3] = 1;

        // Step 3: Approximate plane normal
        // (Quick hack: cross of first two rows; for robustness use eigen-decomposition)
        Vector3 normal = Vector3.Cross(
            new Vector3(cov[0, 0], cov[0, 1], cov[0, 2]),
            new Vector3(cov[1, 0], cov[1, 1], cov[1, 2])
        ).normalized;

        // Step 4: Build orthonormal basis (u,v) in plane
        Vector3 u = Vector3.Cross(normal, Vector3.up);
        if (u.sqrMagnitude < 1e-6f)
            u = Vector3.Cross(normal, Vector3.right);
        u.Normalize();
        Vector3 v = Vector3.Cross(normal, u);
        // Step 5: Project vertices into 2D
        List<Vector2> proj = vertices.Select(p =>
        {
            Vector3 d = p - centroid;
            return new Vector2(Vector3.Dot(d, u), Vector3.Dot(d, v));
        }).ToList();

        float minX = proj.Min(p => p.x);
        float maxX = proj.Max(p => p.x);
        float minY = proj.Min(p => p.y);
        float maxY = proj.Max(p => p.y);

        // Step 6: Synthetic corners in plane space
        Vector2[] corners2D = new Vector2[]
        {
            new Vector2(minX, minY),
            new Vector2(minX, maxY),
            new Vector2(maxX, minY),
            new Vector2(maxX, maxY)
        };

        // Step 7: Map back to 3D
        Vector3[] corners3D = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            corners3D[i] = centroid + u * corners2D[i].x + v * corners2D[i].y;
        }

        return corners3D;
    }
    public static Vector3[] FindRepresentativeQuad(List<Vector3> vertices)
    {
        // Step 1: Compute centroid
        Vector3 centroid = Vector3.zero;
        foreach (var vert in vertices) centroid += vert;
        centroid /= vertices.Count;

        // Step 2: Covariance matrix for PCA
        float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
        foreach (var vert in vertices)
        {
            Vector3 d = vert - centroid;
            xx += d.x * d.x;
            xy += d.x * d.y;
            xz += d.x * d.z;
            yy += d.y * d.y;
            yz += d.y * d.z;
            zz += d.z * d.z;
        }
        // Build covariance
        Matrix4x4 cov = new Matrix4x4();
        cov[0, 0] = xx; cov[0, 1] = xy; cov[0, 2] = xz; cov[0, 3] = 0;
        cov[1, 0] = xy; cov[1, 1] = yy; cov[1, 2] = yz; cov[1, 3] = 0;
        cov[2, 0] = xz; cov[2, 1] = yz; cov[2, 2] = zz; cov[2, 3] = 0;
        cov[3, 3] = 1;

        // Step 3: Approximate normal (cross product of largest covariance directions)
        // Simpler than full eigen decomposition:
        Vector3 normal = Vector3.Cross(
            new Vector3(cov[0, 0], cov[0, 1], cov[0, 2]),
            new Vector3(cov[1, 0], cov[1, 1], cov[1, 2])
        ).normalized;

        // Step 4: Build plane axes (u,v)
        Vector3 u = Vector3.Cross(normal, Vector3.up);
        if (u.magnitude < 0.001f) u = Vector3.Cross(normal, Vector3.right);
        u.Normalize();
        Vector3 v = Vector3.Cross(normal, u);

        // Step 5: Project points into plane coordinates
        List<Vector2> proj = vertices.Select(p =>
        {
            Vector3 d = p - centroid;
            return new Vector2(Vector3.Dot(d, u), Vector3.Dot(d, v));
        }).ToList();

        // Step 6: Find extremes in 2D
        float minX = proj.Min(p => p.x);
        float maxX = proj.Max(p => p.x);
        float minY = proj.Min(p => p.y);
        float maxY = proj.Max(p => p.y);

        // Step 7: Pick closest vertices to each corner
        Vector2[] corners = {
            new Vector2(minX, minY),
            new Vector2(minX, maxY),
            new Vector2(maxX, minY),
            new Vector2(maxX, maxY)
        };

        Vector3[] chosen = new Vector3[4];
        for (int i = 0; i < 4; i++)
        {
            float bestDist = float.MaxValue;
            int bestIdx = -1;
            for (int j = 0; j < proj.Count; j++)
            {
                float d2 = (proj[j] - corners[i]).sqrMagnitude;
                if (d2 < bestDist)
                {
                    bestDist = d2;
                    bestIdx = j;
                }
            }
            chosen[i] = vertices[bestIdx];
        }

        return chosen;
    }

    Bounds GetWorldAABBFromMesh(MeshFilter mf)
    {
        var mesh = mf.sharedMesh;
        if (!mesh) throw new System.Exception("MeshFilter has no mesh.");

        var lb = mesh.bounds;                         // local AABB (mesh space)
        var l2w = mf.transform.localToWorldMatrix;

        // 8 local corners
        Vector3 c = lb.center;
        Vector3 e = lb.extents;
        Vector3[] local = new Vector3[8];
        int i = 0;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    local[i++] = c + Vector3.Scale(e, new Vector3(sx, sy, sz));

        // Transform corners to world and encapsulate
        Vector3 w0 = l2w.MultiplyPoint3x4(local[0]);
        Bounds wb = new Bounds(w0, Vector3.zero);
        for (i = 1; i < 8; i++)
            wb.Encapsulate(l2w.MultiplyPoint3x4(local[i]));
        return wb;
    }
    bool CheckCollision(MeshFilter a, MeshFilter b)
    {
        Bounds A = GetWorldAABBFromMesh(a);  // or GetWorldAABB(a) using Renderer
        Bounds B = GetWorldAABBFromMesh(b);
        return A.Intersects(B);
    }
}
