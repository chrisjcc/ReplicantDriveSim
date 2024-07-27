using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class RenderData
{
    public List<AgentInfo> agents;
}

[System.Serializable]
public class AgentInfo //AgentData
{
    //public int agent_id;
    public string agent_id;
    //public Vector3 position;
    public List<float> position;
}