using System;
using System.Runtime.InteropServices;
using UnityEngine;

public class OpenDriveRenderer : MonoBehaviour
{
    private const string DllName = "libReplicantDriveSim";

    // Import native functions
    [DllImport("libdl", EntryPoint = "dlopen")]
    private static extern IntPtr dlopen(string filename, int flags);

    [DllImport("libdl", EntryPoint = "dlerror")]
    private static extern IntPtr dlerror();

    [DllImport(DllName)]
    private static extern IntPtr LoadOpenDriveMap(string filePath);

    [DllImport(DllName)]
    private static extern IntPtr GetRoadVertices(IntPtr map, out int vertexCount);

    [DllImport(DllName)]
    private static extern IntPtr GetRoadIndices(IntPtr map, out int indexCount);

    [DllImport(DllName)]
    private static extern void FreeOpenDriveMap(IntPtr map);

    [DllImport(DllName)]
    private static extern void FreeVertices(IntPtr vertices);

    [DllImport(DllName)]
    private static extern void FreeIndices(IntPtr indices);

    void Start()
    {
        // Check the DLL path
        string dllPath = System.IO.Path.Combine(Application.dataPath, "Plugins", "TrafficSimulation", "build", "libReplicantDriveSim.dylib");
        Debug.Log("DLL Path: " + dllPath + ", Exists: " + System.IO.File.Exists(dllPath));

        IntPtr handle = dlopen(dllPath, 2); // RTLD_NOW = 2
        if (handle == IntPtr.Zero)
        {
            IntPtr errorPtr = dlerror();
            string error = Marshal.PtrToStringAnsi(errorPtr);
            Debug.LogError("Failed to load libReplicantDriveSim.dylib: " + error);
        }
        else
        {
            Debug.Log("Successfully loaded libReplicantDriveSim.dylib");
        }

        // Path to data.xodr file
        string filePath = System.IO.Path.Combine(Application.dataPath, "Maps", "A10-IN-17-31KM_HW_AC_DE_BER_RELEASE_20210510.xodr");
        Debug.Log("Map file: " + filePath);
        Debug.Log("File Exists: " + System.IO.File.Exists(filePath));

        // Load OpenDRIVE map
        IntPtr map = LoadOpenDriveMap(filePath);
        if (map == IntPtr.Zero)
        {
            Debug.LogError("Failed to load OpenDRIVE map");
            return;
        }

        // Get road vertices
        int vertexCount;
        IntPtr verticesPtr = GetRoadVertices(map, out vertexCount);
        if (verticesPtr == IntPtr.Zero || vertexCount <= 0)
        {
            Debug.LogError("Failed to get road vertices or vertex count is invalid");
            FreeOpenDriveMap(map);
            return;
        }

        // Copy native float array to managed array
        float[] vertices = new float[vertexCount];
        Marshal.Copy(verticesPtr, vertices, 0, vertexCount);

        // Get road indices
        int indexCount;
        IntPtr indicesPtr = GetRoadIndices(map, out indexCount);
        if (indicesPtr == IntPtr.Zero || indexCount <= 0)
        {
            Debug.LogError("Failed to get road indices or index count is invalid");
            FreeVertices(verticesPtr);
            FreeOpenDriveMap(map);
            return;
        }

        // Copy native int array to managed array
        int[] triangles = new int[indexCount];
        Marshal.Copy(indicesPtr, triangles, 0, indexCount);

        Debug.Log("Vertex count: " + (vertexCount / 3));
        Debug.Log("Index count: " + indexCount);

        // Create a mesh
        Mesh mesh = new Mesh();
        Vector3[] unityVertices = new Vector3[vertexCount / 3];

        /*
         OpenDRIVE uses a right-handed coordinate system (X: east, Y: north, Z: up).
         Unity uses a left-handed coordinate system (X: right, Y: up, Z: forward).
         Transform vertices: X -> X, Y -> -Z, Z -> Y
        */
        for (int i = 0; i < unityVertices.Length; i++)
        {
            unityVertices[i] = new Vector3(
                vertices[i * 3 + 0],   // X (east -> X)
                vertices[i * 3 + 2],   // Z (up -> Y)
                -vertices[i * 3 + 1]   // -Y (north -> -Z)
            );
        }

        mesh.vertices = unityVertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        // Attach mesh to GameObject
        GameObject roadObject = new GameObject("RoadMesh");
        MeshFilter filter = roadObject.AddComponent<MeshFilter>();
        MeshRenderer renderer = roadObject.AddComponent<MeshRenderer>();
        filter.mesh = mesh;
        renderer.material = new Material(Shader.Find("Standard"));

        // Clean up
        FreeIndices(indicesPtr);
        FreeVertices(verticesPtr);
        FreeOpenDriveMap(map);
    }
}
