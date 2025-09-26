using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Caches agent state data to reduce expensive native calls and improve performance.
/// This cache operates on a time-based invalidation system to balance performance with data freshness.
/// </summary>
public class AgentStateCache
{
    private readonly Dictionary<string, CachedAgentState> _cache = new Dictionary<string, CachedAgentState>();
    private float _lastUpdateTime;
    private const float CACHE_DURATION = 0.016f; // 60 FPS equivalent

    /// <summary>
    /// Represents cached state data for an agent.
    /// </summary>
    private class CachedAgentState
    {
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public Vector3 Velocity { get; set; }
        public float LastUpdateTime { get; set; }
    }

    /// <summary>
    /// Gets the cached position for the specified agent, updating the cache if necessary.
    /// </summary>
    /// <param name="agentId">The agent identifier</param>
    /// <param name="positionProvider">Function to retrieve current position from native code</param>
    /// <returns>The agent's cached position</returns>
    public Vector3 GetAgentPosition(string agentId, System.Func<string, Vector3> positionProvider)
    {
        if (ShouldUpdateCache() || !_cache.ContainsKey(agentId))
        {
            UpdateAgentPosition(agentId, positionProvider);
        }

        return _cache.TryGetValue(agentId, out var state) ? state.Position : Vector3.zero;
    }

    /// <summary>
    /// Gets the cached rotation for the specified agent, updating the cache if necessary.
    /// </summary>
    /// <param name="agentId">The agent identifier</param>
    /// <param name="rotationProvider">Function to retrieve current rotation from native code</param>
    /// <returns>The agent's cached rotation</returns>
    public Quaternion GetAgentRotation(string agentId, System.Func<string, Quaternion> rotationProvider)
    {
        if (ShouldUpdateCache() || !_cache.ContainsKey(agentId))
        {
            UpdateAgentRotation(agentId, rotationProvider);
        }

        return _cache.TryGetValue(agentId, out var state) ? state.Rotation : Quaternion.identity;
    }

    /// <summary>
    /// Clears all cached data for the specified agent.
    /// </summary>
    /// <param name="agentId">The agent identifier</param>
    public void InvalidateAgent(string agentId)
    {
        _cache.Remove(agentId);
    }

    /// <summary>
    /// Clears all cached data.
    /// </summary>
    public void InvalidateAll()
    {
        _cache.Clear();
        _lastUpdateTime = 0f;
    }

    /// <summary>
    /// Determines if the cache should be updated based on elapsed time.
    /// </summary>
    /// <returns>True if cache needs updating</returns>
    private bool ShouldUpdateCache()
    {
        return Time.time - _lastUpdateTime > CACHE_DURATION;
    }

    /// <summary>
    /// Updates the cached position for a specific agent.
    /// </summary>
    private void UpdateAgentPosition(string agentId, System.Func<string, Vector3> positionProvider)
    {
        if (!_cache.ContainsKey(agentId))
        {
            _cache[agentId] = new CachedAgentState();
        }

        _cache[agentId].Position = positionProvider(agentId);
        _cache[agentId].LastUpdateTime = Time.time;
        _lastUpdateTime = Time.time;
    }

    /// <summary>
    /// Updates the cached rotation for a specific agent.
    /// </summary>
    private void UpdateAgentRotation(string agentId, System.Func<string, Quaternion> rotationProvider)
    {
        if (!_cache.ContainsKey(agentId))
        {
            _cache[agentId] = new CachedAgentState();
        }

        _cache[agentId].Rotation = rotationProvider(agentId);
        _cache[agentId].LastUpdateTime = Time.time;
        _lastUpdateTime = Time.time;
    }
}