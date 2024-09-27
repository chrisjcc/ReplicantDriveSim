import gymnasium as gym
import numpy as np
import ray
from ray.rllib.env.multi_agent_env import MultiAgentEnv
from ray.rllib.utils.typing import MultiAgentDict

from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import (
    EngineConfigurationChannel,
)

class HierarchicalUnityMultiAgentEnv(MultiAgentEnv):
    def __init__(self, config, *args, **kwargs):
        super().__init__()
        self.unity_env_handle = config["unity_env_handle"]
        self.initial_agent_count = config.get("initial_agent_count", 2)
        self.max_episode_steps = config.get("episode_horizon", 1000)
        self.episode_timesteps = 0

        # Set up the Unity environment
        ray.get(self.unity_env_handle.set_float_property.remote("initialAgentCount", self.initial_agent_count))
        ray.get(self.unity_env_handle.set_float_property.remote("MaxSteps", self.max_episode_steps))
        ray.get(self.unity_env_handle.reset.remote())

        # Get behavior specifications
        behavior_specs = ray.get(self.unity_env_handle.get_behavior_specs.remote())
        self.behavior_name = list(behavior_specs.keys())[0]
        self.behavior_spec = behavior_specs[self.behavior_name]

        # Get initial number of agents
        decision_steps, _ = ray.get(self.unity_env_handle.get_steps.remote(self.behavior_name))
        self.num_agents = len(decision_steps)

        # Set up agent IDs for high-level and low-level policies
        self.high_level_agent_ids = [f"high_level_agent_{i}" for i in range(self.num_agents)]
        self.low_level_agent_ids = [f"low_level_agent_{i}" for i in range(self.num_agents)]
        self._agent_ids = set(self.high_level_agent_ids + self.low_level_agent_ids)

        # Set up observation and action spaces
        self.size_of_single_agent_obs = self.behavior_spec.observation_specs[0].shape[0]
        self._update_spaces()

    def _update_spaces(self):
        # High-level policy
        high_level_obs_space = gym.spaces.Box(
            low=-np.inf, high=np.inf, shape=(self.size_of_single_agent_obs,), dtype=np.float32
        )
        high_level_action_space = gym.spaces.Discrete(5)

        # Low-level policy
        low_level_obs_space = gym.spaces.Tuple((
            gym.spaces.Box(low=-np.inf, high=np.inf, shape=(self.size_of_single_agent_obs,), dtype=np.float32),
            gym.spaces.Discrete(5)
        ))
        low_level_action_space = gym.spaces.Box(
            low=np.array([-0.67, 0.0, -8.0]),
            high=np.array([0.67, 4.5, 0.0]),
            shape=(3,),
            dtype=np.float32
        )

        self.observation_spaces = {}
        self.action_spaces = {}

        for agent_id in self.high_level_agent_ids:
            self.observation_spaces[agent_id] = high_level_obs_space
            self.action_spaces[agent_id] = high_level_action_space

        for agent_id in self.low_level_agent_ids:
            self.observation_spaces[agent_id] = low_level_obs_space
            self.action_spaces[agent_id] = low_level_action_space

    def reset(self, *, seed=None, options=None):
        if seed is not None:
            np.random.seed(seed)

        self.episode_timesteps = 0
        ray.get(self.unity_env_handle.reset.remote())

        decision_steps, _ = ray.get(self.unity_env_handle.get_steps.remote(self.behavior_name))
        self.num_agents = len(decision_steps)

        obs_dict = {}
        for i, agent_id in enumerate(decision_steps.agent_id):
            obs = decision_steps[agent_id].obs[0].astype(np.float32)
            obs_dict[f"high_level_agent_{i}"] = obs
            obs_dict[f"low_level_agent_{i}"] = (obs, 0)  # Initial high-level action is 0

        return obs_dict, {}

    def step(self, action_dict: MultiAgentDict):
        high_level_actions = {}
        low_level_actions = {}

        for agent_id, action in action_dict.items():
            if agent_id.startswith("high_level_agent_"):
                high_level_actions[agent_id] = action
            elif agent_id.startswith("low_level_agent_"):
                low_level_actions[agent_id] = action

        # Combine high-level and low-level actions
        combined_actions = {}
        for i in range(self.num_agents):
            high_level_action = high_level_actions.get(f"high_level_agent_{i}", 0)
            low_level_action = low_level_actions.get(f"low_level_agent_{i}", np.zeros(3))
            combined_actions[f"agent_{i}"] = (high_level_action, low_level_action)

        # Convert combined actions to Unity's ActionTuple
        action_tuple = self._convert_to_action_tuple(combined_actions)
        ray.get(self.unity_env_handle.set_actions.remote(self.behavior_name, action_tuple))

        # Step the Unity environment
        for _ in range(25):
            ray.get(self.unity_env_handle.step.remote())

        obs_dict, rewards_dict, terminateds_dict, truncateds_dict, infos_dict = self._get_step_results()

        # Separate results for high-level and low-level policies
        high_level_obs = {}
        low_level_obs = {}
        high_level_rewards = {}
        low_level_rewards = {}
        high_level_terminateds = {}
        low_level_terminateds = {}
        high_level_truncateds = {}
        low_level_truncateds = {}
        high_level_infos = {}
        low_level_infos = {}

        for i in range(self.num_agents):
            agent_obs = obs_dict[f"agent_{i}"]
            high_level_obs[f"high_level_agent_{i}"] = agent_obs
            low_level_obs[f"low_level_agent_{i}"] = (agent_obs, high_level_actions.get(f"high_level_agent_{i}", 0))

            reward = rewards_dict[f"agent_{i}"]
            high_level_rewards[f"high_level_agent_{i}"] = reward
            low_level_rewards[f"low_level_agent_{i}"] = reward

            terminated = terminateds_dict[f"agent_{i}"]
            high_level_terminateds[f"high_level_agent_{i}"] = terminated
            low_level_terminateds[f"low_level_agent_{i}"] = terminated

            truncated = truncateds_dict[f"agent_{i}"]
            high_level_truncateds[f"high_level_agent_{i}"] = truncated
            low_level_truncateds[f"low_level_agent_{i}"] = truncated

            info = infos_dict[f"agent_{i}"]
            high_level_infos[f"high_level_agent_{i}"] = info
            low_level_infos[f"low_level_agent_{i}"] = info

        # Combine results
        obs_dict = {**high_level_obs, **low_level_obs}
        rewards_dict = {**high_level_rewards, **low_level_rewards}
        terminateds_dict = {**high_level_terminateds, **low_level_terminateds}
        truncateds_dict = {**high_level_truncateds, **low_level_truncateds}
        infos_dict = {**high_level_infos, **low_level_infos}

        # Check for episode end
        self.episode_timesteps += 1
        if self.episode_timesteps > self.max_episode_steps:
            truncateds_dict = dict({"__all__": True}, **{agent_id: True for agent_id in self._agent_ids})

        terminateds_dict["__all__"] = all(terminateds_dict.values())
        truncateds_dict["__all__"] = any(truncateds_dict.values())

        return obs_dict, rewards_dict, terminateds_dict, truncateds_dict, infos_dict

    def _convert_to_action_tuple(self, actions_dict):
        """
        Converts the given actions dictionary to an ActionTuple for Unity.

        Args:
            actions_dict (Dict[str, Any]): The actions dictionary to convert.

        Returns:
            ActionTuple: The corresponding ActionTuple.
        """
        # Split the actions into continuous and discrete actions
        continuous_actions = []
        discrete_actions = []

        for agent_id, agent_actions in actions_dict.items():
            # Agent actions is a tuple where the first element is discrete and the rest are continuous
            discrete_action, continuous_action = agent_actions

            discrete_actions.append([discrete_action])
            continuous_actions.append(list(continuous_action))

        # Convert to numpy arrays
        discrete_actions = np.array(discrete_actions, dtype=np.int32)
        continuous_actions = np.array(continuous_actions, dtype=np.float32)

        # Alternative use of ActionTuple.add_discrete(discrete_actions) and ActionTuple.add_continuous(continuous_actions)
        return ActionTuple(continuous=continuous_actions, discrete=discrete_actions)

    def _get_step_results(self):
        """Collects those agents' obs/rewards that have to act in next `step`.

        Returns:
            Tuple:
                obs: Multi-agent observation dict.
                    Only those observations for which to get new actions are
                    returned.
                rewards: Rewards dict matching `obs`.
                dones: Done dict with only an __all__ multi-agent entry in it.
                    __all__=True, if episode is done for all agents.
        """
        # Process observations, rewards, and done flags
        obs_dict = {}
        rewards_dict = {}
        terminateds_dict = {}
        truncateds_dict = {}
        infos_dict = {}

        # Get the new state
        decision_steps, terminal_steps = ray.get(
            self.unity_env_handle.get_steps.remote(self.behavior_name)
        )

        # Alternative, decision_steps.agent_id_to_index
        for agent_id in decision_steps.agent_id:
            agent_key = f"agent_{agent_id}"
            obs_dict[agent_key] = decision_steps[agent_id].obs[0].astype(np.float32)
            rewards_dict[agent_key] = decision_steps[agent_id].reward
            terminateds_dict[agent_key] = False
            truncateds_dict[agent_key] = (
                False  # Assume not truncated if in decision_steps
            )
            infos_dict[agent_key] = {}

        for agent_id in terminal_steps.agent_id:
            agent_key = f"agent_{agent_id}"
            obs_dict[agent_key] = terminal_steps[agent_id].obs[0].astype(np.float32)
            rewards_dict[agent_key] = terminal_steps[agent_id].reward
            terminateds_dict[agent_key] = True
            truncateds_dict[agent_key] = terminal_steps[agent_id].interrupted
            infos_dict[agent_key] = {}


        # All Agents Done Check: Only use dones if all agents are done, then we should do a reset.
        terminateds_dict["__all__"] = len(terminal_steps) == self.num_agents
        truncateds_dict["__all__"] = all(truncateds_dict.values())

        return obs_dict, rewards_dict, terminateds_dict, truncateds_dict, infos_dict

    def close(self):
        ray.get(self.unity_env_handle.close.remote())
