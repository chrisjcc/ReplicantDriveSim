using System;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;

// Pure C# OpenDRIVE parser - reads XODR files to extract road geometry
// This replaces native libOpenDRIVE calls and ensures Unity geometry matches C++ simulation
[System.Serializable]
public class OpenDriveRoad
{
    public string id;
    public float length;
    public string junction;
    public List<OpenDriveGeometry> geometries = new List<OpenDriveGeometry>();
    public List<OpenDriveLane> lanes = new List<OpenDriveLane>();
}

[System.Serializable]
public class OpenDriveGeometry
{
    public float s;           // Start position along road
    public float x, y;        // Start coordinates
    public float hdg;         // Heading (radians)
    public float length;      // Length of this geometry segment
    public GeometryType type;

    // For arcs
    public float curvature;

    // For spirals
    public float curvStart, curvEnd;
}

[System.Serializable]
public class OpenDriveLane
{
    public int id;
    public string type;
    public float width;
}

public enum GeometryType
{
    Line,
    Arc,
    Spiral
}

public static class OpenDriveParser
{
    public static List<OpenDriveRoad> ParseOpenDriveFile(string filePath)
    {
        List<OpenDriveRoad> roads = new List<OpenDriveRoad>();

        try
        {
            Debug.Log($"OpenDriveParser: Parsing file: {filePath}");

            if (!System.IO.File.Exists(filePath))
            {
                Debug.LogError($"OpenDriveParser: File not found: {filePath}");
                return roads;
            }

            XmlDocument doc = new XmlDocument();
            doc.Load(filePath);

            XmlNodeList roadNodes = doc.SelectNodes("//road");
            Debug.Log($"OpenDriveParser: Found {roadNodes.Count} roads");

            foreach (XmlNode roadNode in roadNodes)
            {
                OpenDriveRoad road = ParseRoad(roadNode);
                if (road != null)
                {
                    roads.Add(road);
                }
            }

            Debug.Log($"OpenDriveParser: Successfully parsed {roads.Count} roads");
            return roads;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"OpenDriveParser: Error parsing file: {e.Message}");
            return roads;
        }
    }

    private static OpenDriveRoad ParseRoad(XmlNode roadNode)
    {
        try
        {
            OpenDriveRoad road = new OpenDriveRoad();

            road.id = roadNode.Attributes["id"]?.Value ?? "";
            float.TryParse(roadNode.Attributes["length"]?.Value ?? "0", out road.length);
            road.junction = roadNode.Attributes["junction"]?.Value ?? "-1";

            // Parse planView geometries
            XmlNode planViewNode = roadNode.SelectSingleNode("planView");
            if (planViewNode != null)
            {
                XmlNodeList geometryNodes = planViewNode.SelectNodes("geometry");
                foreach (XmlNode geomNode in geometryNodes)
                {
                    OpenDriveGeometry geom = ParseGeometry(geomNode);
                    if (geom != null)
                    {
                        road.geometries.Add(geom);
                    }
                }
            }

            // Parse lanes
            XmlNode lanesNode = roadNode.SelectSingleNode("lanes");
            if (lanesNode != null)
            {
                ParseLanes(lanesNode, road);
            }

            Debug.Log($"OpenDriveParser: Parsed road {road.id} with {road.geometries.Count} geometries");
            return road;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"OpenDriveParser: Error parsing road: {e.Message}");
            return null;
        }
    }

    private static OpenDriveGeometry ParseGeometry(XmlNode geomNode)
    {
        try
        {
            OpenDriveGeometry geom = new OpenDriveGeometry();

            float.TryParse(geomNode.Attributes["s"]?.Value ?? "0", out geom.s);
            float.TryParse(geomNode.Attributes["x"]?.Value ?? "0", out geom.x);
            float.TryParse(geomNode.Attributes["y"]?.Value ?? "0", out geom.y);
            float.TryParse(geomNode.Attributes["hdg"]?.Value ?? "0", out geom.hdg);
            float.TryParse(geomNode.Attributes["length"]?.Value ?? "0", out geom.length);

            // Determine geometry type
            if (geomNode.SelectSingleNode("line") != null)
            {
                geom.type = GeometryType.Line;
            }
            else if (geomNode.SelectSingleNode("arc") != null)
            {
                geom.type = GeometryType.Arc;
                XmlNode arcNode = geomNode.SelectSingleNode("arc");
                float.TryParse(arcNode.Attributes["curvature"]?.Value ?? "0", out geom.curvature);
            }
            else if (geomNode.SelectSingleNode("spiral") != null)
            {
                geom.type = GeometryType.Spiral;
                XmlNode spiralNode = geomNode.SelectSingleNode("spiral");
                float.TryParse(spiralNode.Attributes["curvStart"]?.Value ?? "0", out geom.curvStart);
                float.TryParse(spiralNode.Attributes["curvEnd"]?.Value ?? "0", out geom.curvEnd);
            }

            return geom;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"OpenDriveParser: Error parsing geometry: {e.Message}");
            return null;
        }
    }

    private static void ParseLanes(XmlNode lanesNode, OpenDriveRoad road)
    {
        try
        {
            XmlNodeList laneSectionNodes = lanesNode.SelectNodes("laneSection");

            foreach (XmlNode sectionNode in laneSectionNodes)
            {
                // Parse right lanes (negative IDs)
                XmlNode rightNode = sectionNode.SelectSingleNode("right");
                if (rightNode != null)
                {
                    XmlNodeList laneNodes = rightNode.SelectNodes("lane");
                    foreach (XmlNode laneNode in laneNodes)
                    {
                        OpenDriveLane lane = ParseLane(laneNode);
                        if (lane != null && lane.type == "driving")
                        {
                            road.lanes.Add(lane);
                        }
                    }
                }

                // Parse left lanes (positive IDs)
                XmlNode leftNode = sectionNode.SelectSingleNode("left");
                if (leftNode != null)
                {
                    XmlNodeList laneNodes = leftNode.SelectNodes("lane");
                    foreach (XmlNode laneNode in laneNodes)
                    {
                        OpenDriveLane lane = ParseLane(laneNode);
                        if (lane != null && lane.type == "driving")
                        {
                            road.lanes.Add(lane);
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"OpenDriveParser: Error parsing lanes: {e.Message}");
        }
    }

    private static OpenDriveLane ParseLane(XmlNode laneNode)
    {
        try
        {
            OpenDriveLane lane = new OpenDriveLane();

            int.TryParse(laneNode.Attributes["id"]?.Value ?? "0", out lane.id);
            lane.type = laneNode.Attributes["type"]?.Value ?? "";

            // Get lane width
            XmlNode widthNode = laneNode.SelectSingleNode("width");
            if (widthNode != null)
            {
                float.TryParse(widthNode.Attributes["a"]?.Value ?? "3.5", out lane.width);
            }
            else
            {
                lane.width = 3.5f; // Default lane width
            }

            return lane;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"OpenDriveParser: Error parsing lane: {e.Message}");
            return null;
        }
    }
}