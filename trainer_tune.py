import os
from typing import Any, Dict, Tuple

os.environ["PYTHONWARNINGS"] = "ignore::DeprecationWarning"

import uuid

import gymnasium as gym
import numpy as np

from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import (
    EngineConfigurationChannel,
)
from mlagents_envs.side_channel.float_properties_channel import FloatPropertiesChannel

import ray
from ray.tune.registry import register_env
from ray.rllib.env.env_context import EnvContext
from ray import tune
from ray.rllib.algorithms.ppo import PPO
from ray.rllib.env.multi_agent_env import MultiAgentEnv
# from ray.rllib.env.external_env import ExternalEnv


@ray.remote
class UnityEnvResource:
    def __init__(self, file_name, worker_id=0, base_port=5004):
        self.channel_id = uuid.UUID("621f0a70-4f87-11ea-a6bf-784f4387d1f7")
        self.engine_configuration_channel = EngineConfigurationChannel()
        self.float_props_channel = FloatPropertiesChannel(self.channel_id)

        # Note: Communication Layer: ML-Agents uses a communication protocol (gRPC) to transfer data between Unity and Python.
        # This protocol serializes the observation data into a format that can be sent over the network.
        # When we create a UnityEnvironment object, it establishes a connection with the Unity.
        # When we call e.g env.resent() or env.step(), it triggers Unity to advance its simulation by one step.
        # After this, Unity sends the new observations back to Python.
        # We can get the observations using the get_steps() method.
        # The decision_steps and terminal_steps objects contain the observations for agents that are still active
        # and those that have terminated, respectively.        
        self.unity_env = UnityEnvironment(
            file_name=file_name,
            worker_id=worker_id,
            base_port=base_port,
            side_channels=[self.engine_configuration_channel, self.float_props_channel],
            seed=42,
        )

    def set_float_property(self, key, value):
        self.float_props_channel.set_property(key, float(value))

    def get_behavior_specs(self):
        return self.unity_env.behavior_specs

    def get_steps(self, behavior_name):
        return self.unity_env.get_steps(behavior_name)

    def set_actions(self, behavior_name, action):
        return self.unity_env.set_actions(behavior_name, action)

    def reset(self):
        return self.unity_env.reset()

    def step(self):
        return self.unity_env.step()

    def close(self):
        if hasattr(self, 'unity_env') and self.unity_env is not None:
            try:
                self.unity_env.close()
            except Exception as e:
                print(f"Error closing Unity environment: {e}")
            finally:
                self.unity_env = None
        else:
            print("Unity environment is already closed or was not properly initialized.")

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.close()


class CustomUnityMultiAgentEnv(MultiAgentEnv):
    def __init__(self, config: EnvContext, *args, **kwargs):
        super().__init__()
        self.initial_agent_count = config.get("initial_agent_count", 2)
        self.unity_env_handle = config["unity_env_handle"]
        self.current_agent_count = self.initial_agent_count

        # Set the initial agent count
        ray.get(self.unity_env_handle.set_float_property.remote("initialAgentCount", self.initial_agent_count))

        # Start the environment
        print(f"Initializing with {self.initial_agent_count} agents")
        # Reset the environment
        ray.get(self.unity_env_handle.reset.remote())

        # Access the behavior spects
        behavior_specs = ray.get(self.unity_env_handle.get_behavior_specs.remote())
        self.behavior_name = list(behavior_specs.keys())[0]
        self.behavior_spec = behavior_specs[self.behavior_name]

        # Initialize observation and action spaces
        self.observation_space = None
        self.action_space = None

        self.observation_spaces = {}
        self.action_spaces = {}

        # Get the actual number of agents after environment reset
        decision_steps, _ = ray.get(self.unity_env_handle.get_steps.remote(self.behavior_name))
        self.num_agents = len(decision_steps)
        print(f"Initial number of agents: {self.num_agents}")

        # Get observation size
        self.size_of_single_agent_obs = self.behavior_spec.observation_specs[0].shape[0]

        # You can access continuous and discrete action sizes like this:
        print(
            f"Continuous action size: {self.behavior_spec.action_spec.continuous_size}"
        )
        print(
            f"Discrete action branches: {self.behavior_spec.action_spec.discrete_branches}"
        )
        # Add the private attribute `_agent_ids` to or env, which is a set containing the ids of agents supported by our env.

        decision_steps, terminal_steps = ray.get(self.unity_env_handle.get_steps.remote(self.behavior_name))
        self._agent_ids = {f"agent_{i}" for i in range(self.num_agents)}
        # self._agent_ids = {f"agent_{i}" for i in decision_steps.agent_id}

        # Establish observation and action spaces
        self._update_spaces()

    def _update_spaces(self):
        # Create the observation space
        single_agent_obs_space = gym.spaces.Box(
            low=-np.inf,
            high=np.inf,
            shape=(self.size_of_single_agent_obs,),
            dtype=np.float32,
        )

        single_agent_action_space = gym.spaces.Tuple(
            (
                gym.spaces.Discrete(
                    self.behavior_spec.action_spec.discrete_branches[0]
                ),
                gym.spaces.Box(
                    low=np.array([-0.610865, 0.0, -8.0]),
                    high=np.array([0.610865, 4.5, 0.0]),
                    shape=(self.behavior_spec.action_spec.continuous_size,),
                    dtype=np.float32,
                ),
            )
        )

        # Populate observation_spaces and action_spaces
        for i in range(self.num_agents):
            agent_key = f"agent_{i}"
            self.observation_spaces[agent_key] = single_agent_obs_space
            self.action_spaces[agent_key] = single_agent_action_space

        self.observation_space = {
            f"agent_{i}": single_agent_obs_space for i in range(self.num_agents)
        }
        self.action_space = {
            f"agent_{i}": single_agent_action_space for i in range(self.num_agents)
        }

        #self.observation_space = self.observation_spaces
        #self.action_space = self.action_spaces

    def __str__(self):
        return f"<{type(self).__name__} with custom behavior spec>"

    def set_observation_space(self, agent_id, obs_space):
        self.observation_spaces[agent_id] = obs_space

    def set_action_space(self, agent_id, act_space):
        self.action_spaces[agent_id] = act_space

    def observation_space_contains(self, observation):
        # Check if the given observation is valid for any agent
        for agent_id, obs_space in self.observation_spaces.items():
            if obs_space.contains(observation.get(agent_id, None)):
                return True
        return False

    def action_space_contains(self, action):
        # Check if the given action is valid for any agent's action space
        for agent_id, act_space in self.action_spaces.items():
            if act_space.contains(action.get(agent_id, None)):
                return True
        return False

    def observation_space_sample(self):
        # Return a sample observation for each agent
        return {
            agent_id: space.sample()
            for agent_id, space in self.observation_space.items()
        }

    def action_space_sample(self, agent_id: list = None):
        return {
            agent_idx: act_sample.sample()
            for agent_idx, act_sample in self.action_space.items()
            if agent_id is None or agent_idx in agent_id
        }

    def reset(self, *, seed=None, options=None):
        # Handle seed if provided
        if seed is not None:
            np.random.seed(seed)

        # Check if options contain a new agent count
        if options and "new_agent_count" in options:
            new_agent_count = options["new_agent_count"]

            if new_agent_count != self.current_agent_count:
                ray.get(self.unity_env_handle.set_float_property.remote("initialAgentCount", new_agent_count))
                print(f"Setting new agent count to: {new_agent_count}")

        # Reset the environment only once (ideally)
        ray.get(self.unity_env_handle.reset.remote())
        #ray.get(self.unity_env_handle.reset.remote())

        # Get decision steps after the reset
        decision_steps, _ = ray.get(self.unity_env_handle.get_steps.remote(self.behavior_name))
        self.num_agents = len(decision_steps)

        # Update num_agents and observation_space
        self._update_spaces()

        obs_dict = {}
        for i, agent_id in enumerate(decision_steps.agent_id):
            obs = decision_steps[agent_id].obs[0].astype(np.float32)
            agent_key = f"agent_{i}"
            obs_dict[agent_key] = obs

        # Checking if the observations are within the bounds of the observation space
        for agent_id, obs in obs_dict.items():
            if not self.observation_space[agent_id].contains(obs):
                print(f"Warning: Observation for {agent_key} is out of bounds:")
                print(f"Observation: {obs}")
                print(f"Observation space: {self.observation_space[agent_id]}")

        # Returning the observations and an empty info dict
        return obs_dict, {}

    def step(
        self, action_dict: Dict[Any, Any]
    ) -> Tuple[
        Dict[Any, Any], Dict[Any, Any], Dict[Any, Any], Dict[Any, Any], Dict[Any, Any]
    ]:
        # Initialize lists to store discrete and continuous actions
        discrete_actions = []
        continuous_actions = []

        # Loop over the actions to separate discrete and continuous actions
        for agent_id, action in action_dict.items():
            # Assuming action is of type gym.spaces.Tuple(gym.spaces.Discrete, gym.spaces.Box)
            discrete, continuous = action

            # Append the actions to the respective lists
            discrete_actions.append([discrete])
            continuous_actions.append(continuous)

        # Convert lists to numpy arrays
        discrete_actions = np.array(discrete_actions)
        continuous_actions = np.array(continuous_actions)

        action_tuple = ActionTuple(
            discrete=discrete_actions, continuous=continuous_actions
        )

        ray.get(self.unity_env_handle.set_actions.remote(self.behavior_name, action_tuple))

        ray.get(self.unity_env_handle.step.remote())

        decision_steps, terminal_steps = ray.get(self.unity_env_handle.get_steps.remote(self.behavior_name))

        obs, rewards, terminateds, truncateds, infos = {}, {}, {}, {}, {}
        for i, agent_id in enumerate(decision_steps.agent_id):
            agent_key = f"agent_{agent_id}"
            obs[agent_key] = decision_steps[agent_id].obs[0].astype(np.float32)
            rewards[agent_key] = decision_steps[agent_id].reward
            terminateds[agent_key] = False
            truncateds[agent_key] = False  # Assume not truncated if in decision_steps
            infos[agent_key] = {}

        for i, agent_id in enumerate(terminal_steps.agent_id):
            agent_key = f"agent_{agent_id}"
            obs[agent_key] = terminal_steps[agent_id].obs[0].astype(np.float32)
            rewards[agent_key] = terminal_steps[agent_id].reward
            terminateds[agent_key] = True
            truncateds[agent_key] = terminal_steps[agent_id].interrupted
            infos[agent_key] = {}

        # Check if all agents are terminated
        terminateds["__all__"] = all(terminateds.values())

        # Check if all agents are truncated
        truncateds["__all__"] = any(truncateds.values())

        return obs, rewards, terminateds, truncateds, infos

    def close(self):
        ray.get(self.unity_env_handle.close.remote())

def env_creator(env_config):
    return CustomUnityMultiAgentEnv(env_config)

# Register the environment with RLlib
tune.register_env("CustomUnityMultiAgentEnv", env_creator)

# Create the shared Unity environment resource
file_name = "libReplicantDriveSim.app" # Path to your Unity executable
unity_env_handle = UnityEnvResource.remote(file_name=file_name)

# Initialize Ray
ray.init(ignore_reinit_error=True)

# Define PPO configuration
config = {
    "env": "CustomUnityMultiAgentEnv",
    "env_config": {
        "unity_env_handle": unity_env_handle,
        "initial_agent_count": 5,  # Set your desired initial agent count here
    },
    "multiagent": {
        "policies": {
            "default_policy": (
                None,
                gym.spaces.Box(-np.inf, np.inf, (19,)),
                gym.spaces.Tuple(
                    (
                        gym.spaces.Discrete(2),
                        gym.spaces.Box(
                            low=np.array([-0.610865, 0.0, -8.0]),
                            high=np.array([0.610865, 4.5, 0.0]),
                            dtype=np.float32,
                        ),
                    )
                ),
                {},
            ),
        },
        "policy_mapping_fn": lambda agent_id, episode, worker, **kwargs: "default_policy",
    },
    # ... other config options ...
    "num_workers": 1,
    "framework": "torch",
    "train_batch_size": 4000,
    "sgd_minibatch_size": 128,
    "num_sgd_iter": 30,
    "lr": 3e-4,
    "gamma": 0.99,
    "lambda": 0.95,
    "clip_param": 0.2,
    "num_envs_per_worker": 1,
    "rollout_fragment_length": 200,
    "disable_env_checking": True,  # Source: https://discuss.ray.io/t/agent-ids-that-are-not-the-names-of-the-agents-in-the-env/6964/3
}

# Run the training
results=tune.run(
    "PPO",
    config=config,
    checkpoint_freq=5,  # Set checkpoint frequency here
    num_samples=1,      # Number of times to repeat the experiment
    max_failures=1,     # Maximum number of failures before stopping the experiment
    verbose=1,          # Verbosity level for logging
    stop={
        "training_iteration": 1, # Number of training iterations
        "episode_reward_mean": 200
    }
)

# Print the results dictionary of the training to inspect the structure
print("Training results: ", results)

# Make sure to close the Unity environment at the end
ray.get(unity_env_handle.close.remote())

# Close Ray
ray.shutdown()
