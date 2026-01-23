using System;
using System.Collections.Generic;
using UnityEngine;

// Generates Unity mesh geometry from OpenDRIVE road data
// Handles lines, arcs, and clothoid spirals to create accurate road networks
public static class OpenDriveGeometryGenerator
{
    public static Mesh GenerateRoadMesh(List<OpenDriveRoad> roads, float meshResolution = 1.0f)
    {
        List<Vector3> allVertices = new List<Vector3>();
        List<int> allIndices = new List<int>();

        Debug.Log($"OpenDriveGeometryGenerator: Generating mesh for {roads.Count} roads");

        foreach (OpenDriveRoad road in roads)
        {
            if (road.geometries.Count == 0) continue;

            // Generate road centerline points
            List<Vector3> centerlinePoints = GenerateRoadCenterline(road, meshResolution);

            if (centerlinePoints.Count < 2) continue;

            // Generate road mesh from centerline
            GenerateRoadStrip(centerlinePoints, road, allVertices, allIndices);
        }

        Debug.Log($"OpenDriveGeometryGenerator: Generated {allVertices.Count} vertices, {allIndices.Count/3} triangles");

        // Create Unity mesh
        Mesh mesh = new Mesh();
        mesh.name = "OpenDriveRoadNetwork";

        if (allVertices.Count > 65535)
        {
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        mesh.vertices = allVertices.ToArray();
        mesh.triangles = allIndices.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Debug information
        Debug.Log($"OpenDriveGeometryGenerator: Final mesh bounds: {mesh.bounds}");
        if (mesh.vertexCount > 0)
        {
            Vector3 firstVertex = mesh.vertices[0];
            Vector3 lastVertex = mesh.vertices[mesh.vertexCount - 1];
            Debug.Log($"OpenDriveGeometryGenerator: First vertex: {firstVertex}, Last vertex: {lastVertex}");
        }

        return mesh;
    }

    private static List<Vector3> GenerateRoadCenterline(OpenDriveRoad road, float resolution)
    {
        List<Vector3> points = new List<Vector3>();

        float currentS = 0f;
        float totalLength = road.length;

        while (currentS <= totalLength)
        {
            Vector3 point = GetPointAtS(road, currentS);
            points.Add(point);

            currentS += resolution;
        }

        // Always add the end point
        if (currentS - resolution < totalLength)
        {
            Vector3 endPoint = GetPointAtS(road, totalLength);
            points.Add(endPoint);
        }

        return points;
    }

    private static Vector3 GetPointAtS(OpenDriveRoad road, float s)
    {
        // Find the geometry segment that contains this s value
        OpenDriveGeometry currentGeom = null;
        foreach (var geom in road.geometries)
        {
            if (s >= geom.s && s <= geom.s + geom.length)
            {
                currentGeom = geom;
                break;
            }
        }

        if (currentGeom == null)
        {
            // Use the last geometry if s is beyond the road
            currentGeom = road.geometries[road.geometries.Count - 1];
            s = currentGeom.s + currentGeom.length;
        }

        // Calculate local s within this geometry
        float localS = s - currentGeom.s;

        // Get point based on geometry type
        Vector2 localPoint = GetLocalPointInGeometry(currentGeom, localS);

        // Transform to world coordinates
        Vector2 worldPoint = TransformToWorld(localPoint, currentGeom);

        // Convert OpenDRIVE coordinates to Unity coordinates
        // OpenDRIVE: X=east, Y=north, Z=up
        // Unity: X=right, Y=up, Z=forward
        // Raise roads slightly above ground level to make them visible
        return new Vector3(worldPoint.x, 0.1f, worldPoint.y);
    }

    private static Vector2 GetLocalPointInGeometry(OpenDriveGeometry geom, float s)
    {
        switch (geom.type)
        {
            case GeometryType.Line:
                return new Vector2(s, 0f);

            case GeometryType.Arc:
                return GetArcPoint(s, geom.curvature);

            case GeometryType.Spiral:
                return GetSpiralPoint(s, geom.curvStart, geom.curvEnd, geom.length);

            default:
                return new Vector2(s, 0f);
        }
    }

    private static Vector2 GetArcPoint(float s, float curvature)
    {
        if (Mathf.Abs(curvature) < 1e-10f)
        {
            // Nearly straight line
            return new Vector2(s, 0f);
        }

        float radius = 1f / Mathf.Abs(curvature);
        float angle = s * curvature;

        if (curvature > 0)
        {
            // Left turn
            return new Vector2(
                radius * Mathf.Sin(angle),
                radius * (1f - Mathf.Cos(angle))
            );
        }
        else
        {
            // Right turn
            return new Vector2(
                radius * Mathf.Sin(-angle),
                -radius * (1f - Mathf.Cos(-angle))
            );
        }
    }

    private static Vector2 GetSpiralPoint(float s, float curvStart, float curvEnd, float length)
    {
        // Simplified clothoid spiral calculation
        // For more accuracy, use Fresnel integrals, but this approximation works for most cases

        float curvRate = (curvEnd - curvStart) / length;
        float curvature = curvStart + curvRate * s;

        // Use small step integration for spiral
        int steps = Mathf.Max(10, (int)(s * 10));
        float stepSize = s / steps;

        Vector2 position = Vector2.zero;
        float heading = 0f;

        for (int i = 0; i < steps; i++)
        {
            float stepS = i * stepSize;
            float stepCurvature = curvStart + curvRate * stepS;

            position.x += stepSize * Mathf.Cos(heading);
            position.y += stepSize * Mathf.Sin(heading);

            heading += stepCurvature * stepSize;
        }

        return position;
    }

    private static Vector2 TransformToWorld(Vector2 localPoint, OpenDriveGeometry geom)
    {
        // Rotate by heading
        float cos_hdg = Mathf.Cos(geom.hdg);
        float sin_hdg = Mathf.Sin(geom.hdg);

        Vector2 rotated = new Vector2(
            localPoint.x * cos_hdg - localPoint.y * sin_hdg,
            localPoint.x * sin_hdg + localPoint.y * cos_hdg
        );

        // Translate to world position
        return new Vector2(geom.x + rotated.x, geom.y + rotated.y);
    }

    private static void GenerateRoadStrip(List<Vector3> centerlinePoints, OpenDriveRoad road,
                                        List<Vector3> allVertices, List<int> allIndices)
    {
        if (centerlinePoints.Count < 2) return;

        // Default lane width if no lanes defined
        float laneWidth = 3.5f;
        if (road.lanes.Count > 0)
        {
            laneWidth = road.lanes[0].width;
        }

        float halfWidth = laneWidth / 2f;
        int baseVertexIndex = allVertices.Count;

        // Generate vertices along the road strip
        for (int i = 0; i < centerlinePoints.Count; i++)
        {
            Vector3 center = centerlinePoints[i];
            Vector3 forward = Vector3.forward;

            if (i < centerlinePoints.Count - 1)
            {
                forward = (centerlinePoints[i + 1] - centerlinePoints[i]).normalized;
            }
            else if (i > 0)
            {
                forward = (centerlinePoints[i] - centerlinePoints[i - 1]).normalized;
            }

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            // Add vertices for left and right sides of the road
            allVertices.Add(center + right * halfWidth);  // Right side
            allVertices.Add(center - right * halfWidth);  // Left side
        }

        // Generate triangles
        for (int i = 0; i < centerlinePoints.Count - 1; i++)
        {
            int v0 = baseVertexIndex + i * 2;     // Right vertex at current point
            int v1 = baseVertexIndex + i * 2 + 1; // Left vertex at current point
            int v2 = baseVertexIndex + (i + 1) * 2;     // Right vertex at next point
            int v3 = baseVertexIndex + (i + 1) * 2 + 1; // Left vertex at next point

            // First triangle (counter-clockwise for upward normal)
            allIndices.Add(v0);
            allIndices.Add(v1);
            allIndices.Add(v2);

            // Second triangle (counter-clockwise for upward normal)
            allIndices.Add(v1);
            allIndices.Add(v3);
            allIndices.Add(v2);
        }
    }
}