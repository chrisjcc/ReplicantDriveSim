from typing import Any, Dict, Optional, Tuple, Union

import gymnasium as gym
import numpy as np
import signal
import sys

import ray
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.exception import UnityCommunicatorStoppedException

from ray.rllib.env.env_context import EnvContext
from ray.rllib.env.multi_agent_env import MultiAgentEnv
from ray.rllib.utils.typing import AgentID, MultiAgentDict, PolicyID


class CustomUnityMultiAgentEnv(MultiAgentEnv):
    """
    Custom multi-agent environment for Unity.

    This environment simulates a scenario with multiple agents. Each agent can perform
    both high-level and low-level actions within the Unity simulation.

    Source: http://www.gitpp.com/xray/ray/-/blob/master/rllib/env/wrappers/unity3d_env.py
    """

    def __init__(self, config: EnvContext, *args, **kwargs):
        """
        Initializes the multi-agent environment.

        Args:
            config (EnvContext): Configuration dictionary containing environment settings.
        """
        super().__init__()
        self.initial_agent_count = config.get("env_config", {}).get("initial_agent_count", 2)

        # Set up signal handler for graceful shutdown
        signal.signal(signal.SIGINT, self._signal_handler)
        signal.signal(signal.SIGTERM, self._signal_handler)

        # Reset entire env every this number of step calls.
        self.max_episode_steps = config.get("env_config", {}).get("episode_horizon", 1000)

        # Keep track of how many times we have called `step` so far.
        self.episode_timesteps = 0

        self.unity_env_handle = config["unity_env_handle"]
        self.env_is_closed = False

        self.current_agent_count = self.initial_agent_count

        # Set the initial agent count in the Unity environment
        ray.get(
            self.unity_env_handle.set_float_property.remote(
                "initialAgentCount", self.initial_agent_count
            )
        )

        # Set the max number steps per episode
        ray.get(
            self.unity_env_handle.set_float_property.remote(
                "MaxSteps", self.max_episode_steps
            )
        )

        # Start the Unity environment
        print(f"Initializing with {self.initial_agent_count} agents")

        # Reset the Unity environment
        try:
            ray.get(self.unity_env_handle.reset.remote())
        except ray.exceptions.RayActorError:
            print("Unity environment has been closed unexpectedly. Returning empty observation.")
            self.env_is_closed = True
            return self._get_empty_reset_results()

        # Access the behavior specifications
        behavior_specs = ray.get(self.unity_env_handle.get_behavior_specs.remote())
        self.behavior_name = list(behavior_specs.keys())[0]
        self.behavior_spec = behavior_specs[self.behavior_name]

        # Initialize observation and action spaces
        self.observation_space = None
        self.action_space = None
        self.observation_spaces = {}
        self.action_spaces = {}
        self.action_tuple = ActionTuple()

        # Get the actual number of agents after environment reset
        decision_steps, _ = ray.get(
            self.unity_env_handle.get_steps.remote(self.behavior_name)
        )
        self.num_agents = len(decision_steps)
        print(f"Initial number of agents: {self.num_agents}")

        # Get observation size
        self.size_of_single_agent_obs = self.behavior_spec.observation_specs[0].shape[0]

        # Log the continuous and discrete action sizes
        print(
            f"Continuous action size: {self.behavior_spec.action_spec.continuous_size}"
        )
        print(
            f"Discrete action branches: {self.behavior_spec.action_spec.discrete_branches}"
        )

        # Create the observation space for a single agent
        self._single_agent_obs_space = gym.spaces.Box(
            low=-np.inf,
            high=np.inf,
            shape=(self.size_of_single_agent_obs,),
            dtype=np.float32,
        )

        # Create the action space for a single agent
        self._single_agent_action_space = gym.spaces.Tuple(
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

        # Add the private attribute `_agent_ids`, which is a set containing the ids of agents supported by the environment
        self._agent_ids = {f"agent_{i}" for i in range(self.num_agents)}

        # Establish observation and action spaces
        self._update_spaces()

        # ML-Agents API version.
        api_version_string = ray.get(self.unity_env_handle.get_api_version.remote())
        self.api_version = api_version_string.split(".")
        self.api_version = [int(s) for s in self.api_version]

        # Get the simulation time step (i.e., frames per second)
        self.fps = int(ray.get(self.unity_env_handle.get_field_value.remote("FramesPerSecond")).get("FramesPerSecond", 25))

    @property
    def single_agent_obs_space(self):
        # Define and return the observation space for a single agent
        return self._single_agent_obs_space

    @property
    def single_agent_action_space(self):
        # Define and return the action space for a single agent
        return self._single_agent_action_space

    def _update_spaces(self):
        """
        Updates the observation and action spaces for all agents.

        Returns:
            None
        """
        # Populate observation_spaces and action_spaces for each agent
        for i in range(self.num_agents):
            agent_key = f"agent_{i}"
            self.observation_spaces[agent_key] = self.single_agent_obs_space
            self.action_spaces[agent_key] = self.single_agent_action_space

        self.observation_space = {
            f"agent_{i}": self.single_agent_obs_space for i in range(self.num_agents)
        }
        self.action_space = {
            f"agent_{i}": self.single_agent_action_space for i in range(self.num_agents)
        }

    def __str__(self):
        return f"<{type(self).__name__} with custom behavior spec>"

    def set_observation_space(self, agent_id: str, obs_space: gym.Space):
        """
        Sets the observation space for a specific agent.

        Args:
            agent_id (str): The ID of the agent.
            obs_space (gym.Space): The observation space to set.
        """
        self.observation_spaces[agent_id] = obs_space

    def set_action_space(self, agent_id: str, act_space: gym.Space):
        """
        Sets the action space for a specific agent.

        Args:
            agent_id (str): The ID of the agent.
            act_space (gym.Space): The action space to set.
        """
        self.action_spaces[agent_id] = act_space

    def observation_space_contains(self, observation: Dict[Any, Any]) -> bool:
        """
        Checks if the given observation is valid for any agent.

        Args:
            observation (Dict[Any, Any]): The observation to check.

        Returns:
            bool: True if valid for any agent, False otherwise.
        """
        for agent_id, obs_space in self.observation_spaces.items():
            if obs_space.contains(observation.get(agent_id, None)):
                return True
        return False

    def action_space_contains(self, action: Dict[Any, Any]) -> bool:
        """
        Checks if the given action is valid for any agent.

        Args:
            action (Dict[Any, Any]): The action to check.

        Returns:
            bool: True if valid for any agent, False otherwise.
        """
        for agent_id, act_space in self.action_spaces.items():
            if act_space.contains(action.get(agent_id, None)):
                return True
        return False

    def observation_space_sample(self):
        """
        Samples observations from the observation space for each agent.

        This method provides a random sample from the observation space for each agent.
        It can be useful for testing or initializing agent observations.

        Returns:
            dict: A dictionary where the keys are agent IDs and the values are sampled observations
            from the corresponding agent's observation space.
        """
        return {
            agent_id: space.sample()
            for agent_id, space in self.observation_space.items()
        }

    def action_space_sample(self, agent_id: list = None):
        """
        Samples actions from the action space for each or specified agents.

        This method provides a random sample from the action space for each agent.
        If `agent_id` is provided, only samples actions for the specified agents.
        Useful for testing or initializing agent actions.

        Args:
            agent_id (list, optional): A list of agent IDs to sample actions for.
            If None, samples actions for all agents. Defaults to None.

        Returns:
            dict: A dictionary where the keys are agent IDs and the values are sampled actions
            from the corresponding agent's action space.
        """
        return {
            agent_idx: act_sample.sample()
            for agent_idx, act_sample in self.action_space.items()
            if agent_id is None or agent_idx in agent_id
        }

    def _convert_to_action_tuple(self, actions_dict: Dict[str, Any]) -> ActionTuple:
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

    def reset(
        self, *, seed: Optional[int] = None, options: Optional[dict] = None
    ) -> Tuple[Dict[str, np.ndarray], Dict[str, Any]]:
        if self.env_is_closed:
            print("Unity environment is closed. Returning empty observation.")
            return self._get_empty_reset_results()

        if seed is not None:
            np.random.seed(seed)

        if options and "new_agent_count" in options:
            new_agent_count = options["new_agent_count"]
            if new_agent_count != self.current_agent_count:
                try:
                    ray.get(
                        self.unity_env_handle.set_float_property.remote(
                            "initialAgentCount", new_agent_count
                        )
                    )
                    print(f"Setting new agent count to: {new_agent_count}")
                    self.current_agent_count = new_agent_count
                except ray.exceptions.RayActorError:
                    print("Unity environment has been closed unexpectedly. Returning empty reset results.")
                    self.env_is_closed = True
                    return self._get_empty_reset_results()

        self.episode_timesteps = 0

        try:
            ray.get(self.unity_env_handle.reset.remote())
            print("Environment reset successfully.")

            decision_steps, _ = ray.get(
                self.unity_env_handle.get_steps.remote(self.behavior_name)
            )
            self.num_agents = len(decision_steps)

            self._update_spaces()

            obs_dict = {
                f"agent_{i}": decision_steps[agent_id].obs[0].astype(np.float32)
                for i, agent_id in enumerate(decision_steps.agent_id)
            }

            return obs_dict, {}
        except UnityCommunicatorStoppedException:
            print("Unity environment was closed. Exiting the Python program.")
            self.close()
            return self._get_empty_reset_results()
        except Exception as e:
            print(f"Error during reset: {str(e)}")
            self.close()
            return self._get_empty_reset_results()

    def step(
        self, action_dict: Dict[str, Any]
    ) -> Tuple[
        Dict[str, np.ndarray],
        Dict[str, float],
        Dict[str, bool],
        Dict[str, bool],
        Dict[str, Dict[str, Any]]
    ]:
        if self.env_is_closed:
            print("Unity environment is closed. Returning empty step results.")
            return self._get_empty_step_results()

        try:
            action_tuple = self._convert_to_action_tuple(action_dict)
            ray.get(
                self.unity_env_handle.set_actions.remote(self.behavior_name, action_tuple)
            )

            for _ in range(self.fps):
                ray.get(self.unity_env_handle.step.remote())

            obs_dict, rewards_dict, terminateds_dict, truncateds_dict, infos_dict = (
                self._get_step_results()
            )

            self.episode_timesteps += 1
            if self.episode_timesteps > self.max_episode_steps:
                truncateds_dict = {agent_id: True for agent_id in self._agent_ids}
                truncateds_dict["__all__"] = True

            terminateds_dict["__all__"] = all(terminateds_dict.values())
            truncateds_dict["__all__"] = any(truncateds_dict.values())

            return obs_dict, rewards_dict, terminateds_dict, truncateds_dict, infos_dict

        except UnityCommunicatorStoppedException:
            print("Unity environment was closed unexpectedly. Returning empty step results.")
            self.env_is_closed = True
            return self._get_empty_step_results()
        except Exception as e:
            print(f"An error occurred during step: {e}")
            self.close()
            return self._get_empty_step_results()

    def _get_empty_reset_results(self):
        empty_obs = {
            agent_id: np.zeros(self.single_agent_obs_space.shape, dtype=np.float32)
            for agent_id in self._agent_ids
        }
        return empty_obs, {}

    def _get_empty_step_results(self):
        empty_dict = {agent_id: None for agent_id in self._agent_ids}
        return (
            {agent_id: np.zeros(self.single_agent_obs_space.shape, dtype=np.float32) for agent_id in self._agent_ids},
            {agent_id: 0.0 for agent_id in self._agent_ids},
            {agent_id: True for agent_id in self._agent_ids},
            {agent_id: True for agent_id in self._agent_ids},
            {agent_id: {} for agent_id in self._agent_ids}
        )

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

    def _signal_handler(self, sig, frame):
        print('Received shutdown signal. Closing environment gracefully.')
        self.close()
        sys.exit(0)

    def close(self):
        """
        Close the Unity environment and release any resources associated with it.

        This method sends a remote call to the Unity environment handle to close
        the environment. It ensures that any resources or connections used by
        the Unity environment are properly cleaned up. The `ray.get()` method
        is used to ensure the closure operation is completed before proceeding.

        Note:
            This method should be called to properly shut down the environment
            after training or when it is no longer needed.

        Example:
            # Close the Unity environment
            env.close()
        """
        if self.unity_env_handle:
            try:
                ray.get(self.unity_env_handle.close.remote())
            except Exception as e:
                print(f"Error closing Unity environment: {e}")
            finally:
                self.unity_env_handle = None
                self.env_is_closed = True
        print("Environment closed.")
