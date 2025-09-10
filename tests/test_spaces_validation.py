"""
Tests for observation and action space validation in Unity MultiAgent environments.
"""
import unittest
from unittest.mock import MagicMock, patch
import numpy as np
import gymnasium as gym

from replicantdrivesim.envs.environment import CustomUnityMultiAgentEnv


class TestSpacesValidation(unittest.TestCase):
    """Test observation and action space validation."""

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
        
        # Mock realistic decision steps
        self.mock_decision_steps = MagicMock()
        self.mock_decision_steps.agent_id = [0, 1]
        
        # Create a function to return appropriate observations
        def mock_getitem(agent_id):
            mock_agent_step = MagicMock()
            mock_agent_step.obs = [np.random.randn(10).astype(np.float32)]
            mock_agent_step.reward = 0.0
            return mock_agent_step
            
        self.mock_decision_steps.__getitem__ = mock_getitem
        
        self.mock_unity_handle.get_steps = MagicMock(return_value=(
            self.mock_decision_steps,
            MagicMock(agent_id=[])  # Empty terminal steps
        ))
        self.mock_unity_handle.get_api_version = MagicMock(return_value="1.0.0")
        self.mock_unity_handle.get_field_value = MagicMock(return_value={"FramesPerSecond": 25})
        
        self.config = {
            "env_config": {
                "initial_agent_count": 2,
                "episode_horizon": 1000
            },
            "unity_env_handle": self.mock_unity_handle
        }

    def test_observation_space_matches_unity_output(self):
        """Test that observation space matches actual Unity observations."""
        with patch('ray.get', return_value=True):
            env = CustomUnityMultiAgentEnv(self.config)
            obs, _ = env.reset()
            
            # Check that all observations match their spaces
            for agent_id, observation in obs.items():
                self.assertTrue(
                    env.observation_spaces[agent_id].contains(observation),
                    f"Observation for {agent_id} does not match its observation space"
                )
                self.assertEqual(observation.dtype, np.float32)

    def test_action_space_sampling_valid(self):
        """Test that sampled actions are valid for the environment."""
        with patch('ray.get', return_value=True):
            env = CustomUnityMultiAgentEnv(self.config)
            
            # Sample actions from action space
            sampled_actions = env.action_space_sample()
            
            # All sampled actions should be valid
            for agent_id, action in sampled_actions.items():
                self.assertTrue(
                    env.action_spaces[agent_id].contains(action),
                    f"Sampled action for {agent_id} is not valid for its action space"
                )

    def test_observation_space_contains_method(self):
        """Test the observation_space_contains method."""
        with patch('ray.get', return_value=True):
            env = CustomUnityMultiAgentEnv(self.config)
            
            # Valid observation
            valid_obs = {
                "agent_0": np.random.randn(10).astype(np.float32),
                "agent_1": np.random.randn(10).astype(np.float32)
            }
            self.assertTrue(env.observation_space_contains(valid_obs))
            
            # Invalid observation (wrong shape)
            invalid_obs = {
                "agent_0": np.random.randn(5).astype(np.float32)  # Wrong shape
            }
            self.assertFalse(env.observation_space_contains(invalid_obs))

    def test_action_space_contains_method(self):
        """Test the action_space_contains method."""
        with patch('ray.get', return_value=True):
            env = CustomUnityMultiAgentEnv(self.config)
            
            # Valid action
            valid_action = {
                "agent_0": (0, np.array([0.1, 0.2, 0.3], dtype=np.float32))
            }
            self.assertTrue(env.action_space_contains(valid_action))
            
            # Invalid action (discrete out of range)
            invalid_action = {
                "agent_0": (5, np.array([0.1, 0.2, 0.3], dtype=np.float32))  # Discrete > 1
            }
            self.assertFalse(env.action_space_contains(invalid_action))

    def test_action_bounds_clipping(self):
        """Test that continuous actions are clipped to bounds."""
        with patch('ray.get', return_value=True):
            env = CustomUnityMultiAgentEnv(self.config)
            
            # Get action space bounds
            action_space = env.single_agent_action_space[1]  # Continuous part
            low_bounds = action_space.low
            high_bounds = action_space.high
            
            # Create out-of-bounds actions
            out_of_bounds_actions = {
                "agent_0": (0, np.array([100.0, -100.0, 1000.0], dtype=np.float32))
            }
            
            # Convert to ActionTuple (this should clip internally)
            action_tuple = env._convert_to_action_tuple(out_of_bounds_actions)
            
            # Check that continuous actions are clipped
            clipped_action = action_tuple.continuous[0]
            self.assertTrue(np.all(clipped_action >= low_bounds))
            self.assertTrue(np.all(clipped_action <= high_bounds))

    def test_observation_dtype_consistency(self):
        """Test that all observations have consistent dtype."""
        with patch('ray.get', return_value=True):
            env = CustomUnityMultiAgentEnv(self.config)
            obs, _ = env.reset()
            
            for agent_id, observation in obs.items():
                self.assertEqual(observation.dtype, np.float32,
                               f"Observation for {agent_id} has incorrect dtype")

    def test_spaces_dict_structure(self):
        """Test that observation and action spaces are properly structured as Dicts."""
        with patch('ray.get', return_value=True):
            env = CustomUnityMultiAgentEnv(self.config)
            
            # Check observation_spaces
            self.assertIsInstance(env.observation_spaces, gym.spaces.Dict)
            for agent_id in env.possible_agents:
                self.assertIn(agent_id, env.observation_spaces.spaces)
                self.assertIsInstance(env.observation_spaces[agent_id], gym.spaces.Box)
            
            # Check action_spaces
            self.assertIsInstance(env.action_spaces, gym.spaces.Dict)
            for agent_id in env.possible_agents:
                self.assertIn(agent_id, env.action_spaces.spaces)
                self.assertIsInstance(env.action_spaces[agent_id], gym.spaces.Tuple)

    def test_single_agent_spaces_properties(self):
        """Test single agent space properties."""
        with patch('ray.get', return_value=True):
            env = CustomUnityMultiAgentEnv(self.config)
            
            # Test single agent observation space
            obs_space = env.single_agent_obs_space
            self.assertIsInstance(obs_space, gym.spaces.Box)
            self.assertEqual(obs_space.shape, (10,))
            self.assertEqual(obs_space.dtype, np.float32)
            
            # Test single agent action space  
            action_space = env.single_agent_action_space
            self.assertIsInstance(action_space, gym.spaces.Tuple)
            self.assertEqual(len(action_space.spaces), 2)  # Discrete + Continuous


if __name__ == "__main__":
    unittest.main()