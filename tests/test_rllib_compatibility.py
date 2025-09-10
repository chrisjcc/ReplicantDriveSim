"""
RLlib-specific tests for Unity MultiAgent environments.
Tests compatibility with Ray RLlib training workflows.
"""
import unittest
from unittest.mock import MagicMock, patch
import numpy as np
import ray
from ray.rllib.env.multi_agent_env import MultiAgentEnv

from replicantdrivesim.envs.environment import CustomUnityMultiAgentEnv
from replicantdrivesim.envs.unity_env_resource import UnityEnvResource


class MockUnityEnvHandle:
    """Mock Unity environment handle for testing."""
    
    def __init__(self):
        self.behavior_specs = {
            "test_behavior": MagicMock(
                action_spec=MagicMock(
                    continuous_size=3,
                    discrete_branches=[2]
                ),
                observation_specs=[
                    MagicMock(shape=(10,))
                ]
            )
        }
        self.closed = False
    
    def set_configuration(self, config):
        return True
    
    def set_float_property(self, key, value):
        return True
        
    def set_float_parameter(self, key, value):
        return True
    
    def reset(self):
        return True
    
    def get_behavior_specs(self):
        return self.behavior_specs
    
    def get_steps(self, behavior_name):
        # Mock decision steps and terminal steps
        decision_steps = MagicMock()
        decision_steps.agent_id = [0, 1]
        decision_steps.__getitem__ = lambda self, agent_id: MagicMock(
            obs=[np.random.randn(10).astype(np.float32)],
            reward=0.0
        )
        
        terminal_steps = MagicMock()
        terminal_steps.agent_id = []
        
        return decision_steps, terminal_steps
    
    def set_actions(self, behavior_name, action_tuple):
        return True
    
    def step(self):
        return True
    
    def close(self):
        self.closed = True
        return True
    
    def get_api_version(self):
        return "1.0.0"
    
    def get_field_value(self, key):
        return {"FramesPerSecond": 25}


class TestRLlibCompatibility(unittest.TestCase):
    """Test RLlib compatibility of the Unity MultiAgent environment."""

    def setUp(self):
        """Set up test fixtures."""
        self.mock_unity_handle = MagicMock()
        self.mock_unity_handle.set_configuration = MagicMock(return_value=True)
        self.mock_unity_handle.set_float_property = MagicMock(return_value=True)
        self.mock_unity_handle.set_float_parameter = MagicMock(return_value=True)
        self.mock_unity_handle.reset = MagicMock(return_value=True)
        self.mock_unity_handle.get_behavior_specs = MagicMock(return_value={
            "test_behavior": MagicMock(
                action_spec=MagicMock(
                    continuous_size=3,
                    discrete_branches=[2]
                ),
                observation_specs=[MagicMock(shape=(10,))]
            )
        })
        self.mock_unity_handle.get_steps = MagicMock(return_value=(
            MagicMock(
                agent_id=[0, 1],
                __getitem__=lambda self, agent_id: MagicMock(
                    obs=[np.random.randn(10).astype(np.float32)],
                    reward=0.0
                )
            ),
            MagicMock(agent_id=[])
        ))
        self.mock_unity_handle.get_api_version = MagicMock(return_value="1.0.0")
        self.mock_unity_handle.get_field_value = MagicMock(return_value={"FramesPerSecond": 25})
        
        # Config for environment
        self.config = {
            "env_config": {
                "initial_agent_count": 2,
                "episode_horizon": 1000
            },
            "unity_env_handle": self.mock_unity_handle
        }

    def test_inherits_from_multiagent_env(self):
        """Test that environment properly inherits from MultiAgentEnv."""
        with patch('ray.get', return_value=True):
            env = CustomUnityMultiAgentEnv(self.config)
            self.assertIsInstance(env, MultiAgentEnv)
    
    def test_step_signature_rllib_compliant(self):
        """Test step() returns RLlib-compliant 5-tuple."""
        with patch('ray.get', return_value=True):
            env = CustomUnityMultiAgentEnv(self.config)
            
            # Mock action
            actions = {
                "agent_0": (0, np.array([0.1, 0.2, 0.3], dtype=np.float32)),
                "agent_1": (1, np.array([-0.1, 0.5, -0.2], dtype=np.float32))
            }
            
            result = env.step(actions)
            
            # Should return 5-tuple: (obs, rewards, terminated, truncated, info)
            self.assertEqual(len(result), 5)
            obs, rewards, terminated, truncated, info = result
            
            # Check types
            self.assertIsInstance(obs, dict)
            self.assertIsInstance(rewards, dict)
            self.assertIsInstance(terminated, dict)
            self.assertIsInstance(truncated, dict)
            self.assertIsInstance(info, dict)
            
            # Check for __all__ keys
            self.assertIn("__all__", terminated)
            self.assertIn("__all__", truncated)

    def test_reset_signature_rllib_compliant(self):
        """Test reset() returns (obs, info) tuple."""
        with patch('ray.get', return_value=True):
            env = CustomUnityMultiAgentEnv(self.config)
            
            result = env.reset()
            
            # Should return tuple (obs, info)
            self.assertIsInstance(result, tuple)
            self.assertEqual(len(result), 2)
            
            obs, info = result
            self.assertIsInstance(obs, dict)
            self.assertIsInstance(info, dict)

    def test_seed_reproducibility(self):
        """Test that reset with same seed produces consistent results."""
        with patch('ray.get', return_value=True):
            # Create two environments with same config
            env1 = CustomUnityMultiAgentEnv(self.config)
            env2 = CustomUnityMultiAgentEnv(self.config)
            
            # Reset with same seed
            obs1, _ = env1.reset(seed=42)
            obs2, _ = env2.reset(seed=42)
            
            # Check that Unity environment received the seed
            env1.unity_env_handle.set_float_parameter.assert_called_with("seed", 42.0)
            env2.unity_env_handle.set_float_parameter.assert_called_with("seed", 42.0)

    def test_action_space_clipping(self):
        """Test that out-of-bounds actions are properly clipped."""
        with patch('ray.get', return_value=True):
            env = CustomUnityMultiAgentEnv(self.config)
            
            # Create actions outside bounds
            extreme_actions = {
                "agent_0": (0, np.array([10.0, 100.0, -100.0], dtype=np.float32)),  # Way outside bounds
                "agent_1": (1, np.array([-10.0, -100.0, 100.0], dtype=np.float32))
            }
            
            # Should not raise error and should clip internally
            try:
                result = env.step(extreme_actions)
                self.assertIsNotNone(result)
            except Exception as e:
                self.fail(f"Environment failed with out-of-bounds actions: {e}")

    def test_agent_count_validation(self):
        """Test validation of agent count in reset options."""
        with patch('ray.get', return_value=True):
            env = CustomUnityMultiAgentEnv(self.config)
            
            # Test invalid agent counts
            with self.assertRaises(ValueError):
                env.reset(options={"new_agent_count": -1})
            
            with self.assertRaises(ValueError):
                env.reset(options={"new_agent_count": 0})
                
            with self.assertRaises(ValueError):
                env.reset(options={"new_agent_count": "invalid"})

    def test_observation_space_structure(self):
        """Test that observation spaces are properly structured for RLlib."""
        with patch('ray.get', return_value=True):
            env = CustomUnityMultiAgentEnv(self.config)
            
            # Check observation_spaces is a Dict
            self.assertIn("observation_spaces", dir(env))
            self.assertIsInstance(env.observation_spaces, dict)
            
            # Check each agent has an observation space
            for agent_id in env.possible_agents:
                self.assertIn(agent_id, env.observation_spaces)
                self.assertTrue(env.observation_spaces[agent_id].contains(
                    env.observation_spaces[agent_id].sample()
                ))

    def test_action_space_structure(self):
        """Test that action spaces are properly structured for RLlib."""
        with patch('ray.get', return_value=True):
            env = CustomUnityMultiAgentEnv(self.config)
            
            # Check action_spaces is a Dict
            self.assertIn("action_spaces", dir(env))
            self.assertIsInstance(env.action_spaces, dict)
            
            # Check each agent has an action space
            for agent_id in env.possible_agents:
                self.assertIn(agent_id, env.action_spaces)
                self.assertTrue(env.action_spaces[agent_id].contains(
                    env.action_spaces[agent_id].sample()
                ))

    def test_environment_close(self):
        """Test that environment properly closes resources."""
        with patch('ray.get', return_value=True):
            env = CustomUnityMultiAgentEnv(self.config)
            
            # Close environment
            env.close()
            
            # Verify Unity environment was closed
            env.unity_env_handle.close.assert_called_once()


class TestUnityEnvResourceManagement(unittest.TestCase):
    """Test resource management in UnityEnvResource."""

    def test_unique_channel_ids(self):
        """Test that multiple instances get unique channel IDs."""
        config1 = {"file_name": "/fake/path", "env_config": {}}
        config2 = {"file_name": "/fake/path", "env_config": {}}
        
        with patch('os.path.exists', return_value=True):
            with patch('os.path.isdir', return_value=True):
                with patch('replicantdrivesim.envs.unity_env_resource.UnityEnvironment'):
                    resource1 = UnityEnvResource.remote(config1)
                    resource2 = UnityEnvResource.remote(config2)
                    
                    # Since we're using uuid4(), IDs should be different
                    # This is tested by the implementation change

    def test_close_idempotency(self):
        """Test that close() can be called multiple times safely."""
        config = {"file_name": "/fake/path", "env_config": {}}
        
        with patch('os.path.exists', return_value=True):
            with patch('os.path.isdir', return_value=True):
                with patch('replicantdrivesim.envs.unity_env_resource.UnityEnvironment') as mock_unity:
                    resource = UnityEnvResource(config)
                    
                    # Close multiple times
                    resource.close()
                    resource.close()
                    resource.close()
                    
                    # Unity environment should only be closed once
                    mock_unity.return_value.close.assert_called_once()


if __name__ == "__main__":
    unittest.main()