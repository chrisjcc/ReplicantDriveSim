using UnityEngine;

// Helper script to automatically find and assign road materials
[System.Serializable]
public static class RoadMaterialLoader
{
    public static Material LoadRoadMaterial()
    {
        // Try to find RoadMaterial first, then RoadMaterial as fallback
        Material roadMaterial = null;

        // Method 1: Try Resources folder - Look for RoadMaterial first
        roadMaterial = Resources.Load<Material>("RoadMaterial");
        if (roadMaterial != null)
        {
            Debug.Log("RoadMaterialLoader: Found RoadMaterial in Resources");
            return roadMaterial;
        }

        roadMaterial = Resources.Load<Material>("Materials/RoadMaterial");
        if (roadMaterial != null)
        {
            Debug.Log("RoadMaterialLoader: Found RoadMaterial in Resources/Materials");
            return roadMaterial;
        }

        // Fallback to RoadMaterial
        roadMaterial = Resources.Load<Material>("RoadMaterial");
        if (roadMaterial != null)
        {
            Debug.Log("RoadMaterialLoader: Found RoadMaterial material in Resources");
            return roadMaterial;
        }

        roadMaterial = Resources.Load<Material>("Materials/RoadMaterial");
        if (roadMaterial != null)
        {
            Debug.Log("RoadMaterialLoader: Found RoadMaterial material in Resources/Materials");
            return roadMaterial;
        }

#if UNITY_EDITOR
        // Method 2: In Editor, try direct asset path
        string[] assetPaths = {
            "Assets/Materials/RoadMaterial.mat",
            "Assets/RoadMaterial.mat",
            "Assets/Textures/RoadMaterial.mat",
            "Assets/Textures/Road007_2K-JPG/Materials/Road007.mat",
            "Assets/Materials/Road007.mat",
            "Assets/Road007.mat"
        };

        foreach (string path in assetPaths)
        {
            roadMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            if (roadMaterial != null)
            {
                Debug.Log($"RoadMaterialLoader: Found RoadMaterial material at {path}");
                return roadMaterial;
            }
        }
#endif

        // Method 3: Search for any material with "Road" in the name
        Material[] allMaterials = Resources.FindObjectsOfTypeAll<Material>();

        // First pass: Look for exact "RoadMaterial" match
        foreach (Material mat in allMaterials)
        {
            if (mat.name == "RoadMaterial")
            {
                Debug.Log($"RoadMaterialLoader: Found exact RoadMaterial: {mat.name}");
                return mat;
            }
        }

        // Second pass: Look for any road material
        foreach (Material mat in allMaterials)
        {
            if (mat.name.Contains("RoadMaterial") || mat.name.Contains("RoadMaterial") || mat.name.Contains("Road"))
            {
                Debug.Log($"RoadMaterialLoader: Found road material: {mat.name}");
                return mat;
            }
        }

        Debug.LogWarning("RoadMaterialLoader: Could not find RoadMaterial material, will use default");
        return null;
    }

    public static Material CreateDefaultRoadMaterial()
    {
        Material roadMaterial = new Material(Shader.Find("Standard"));

        // Create a more road-like appearance
        roadMaterial.color = new Color(0.2f, 0.2f, 0.2f, 1f); // Dark asphalt color
        roadMaterial.SetFloat("_Metallic", 0.1f);              // Slightly reflective
        roadMaterial.SetFloat("_Smoothness", 0.3f);            // Some roughness
        roadMaterial.name = "DefaultRoadMaterial";

        Debug.Log("RoadMaterialLoader: Created default road material");
        return roadMaterial;
    }
}
