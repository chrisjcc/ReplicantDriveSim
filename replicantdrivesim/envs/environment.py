from typing import Any, Dict, Optional, Tuple, Union

import gymnasium as gym
import numpy as np
import ray
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfig

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
        self.current_agent_count = config.get("env_config", {}).get("initial_agent_count", 2)

        # Initialize agents list and possible_agents list
        self.possible_agents = [f"agent_{i}" for i in range(self.current_agent_count)]
        self.agents = self.possible_agents.copy()  # Start with all possible agents active

        # Keep track of how many times we have called `step` so far.
        self.episode_timesteps = 0

        self.unity_env_handle = config["unity_env_handle"]

        # Set Unity environment configuration
        ray.get(self.unity_env_handle.set_configuration.remote(EngineConfig(
            width=1920,            # Configure resolution width  (default: 1920)
            height=1080,           # Configure resolution height  (default: 1080)
            quality_level=5,       # Adjust quality level (0 = fastest, 5 = highest quality)
            time_scale=20.0,       # Set time scale (speed of simulation)
            target_frame_rate=60,  # The target frame rate (default: 60)
            capture_frame_rate=60  # The capture frame rate (default: 60)
        )))

        # Set the max number steps per episode
        self.max_episode_steps = config.get("env_config", {}).get("episode_horizon", 1000)

        # Set the max number steps per episode
        ray.get(
            self.unity_env_handle.set_float_property.remote(
                "MaxSteps", self.max_episode_steps
            )
        )

        # Start the Unity environment
        print(f"Initializing with {self.current_agent_count} active agents")

        # Reset the Unity environment
        try:
            ray.get(self.unity_env_handle.reset.remote())
        except Exception as e:
            # Handle the error appropriately, maybe re-initialize the environment
            print(f"Error resetting Unity environment: {e}")

        api_version_string = ray.get(self.unity_env_handle.get_api_version.remote())
        print(f"API Version: {api_version_string}")

        # Access the behavior specifications
        self.behavior_specs = ray.get(self.unity_env_handle.get_behavior_specs.remote())
        print(f"Behavior specs: {list(self.behavior_specs.keys())}")

        # Store the behavior name for later use
        self._behavior_name = next(iter(self.behavior_specs.keys()))
        print(f"Initialized with behavior name: {self._behavior_name}")

        self.behavior_spec = self.behavior_specs[self._behavior_name]
        print(f"Action spec - Continuous: {self.behavior_spec.action_spec.continuous_size}, Discrete: {self.behavior_spec.action_spec.discrete_branches}")

        # Initialize observation and action spaces
        self.observation_spaces = {}
        self.action_spaces = {}
        self.action_tuple = ActionTuple()

        # Get the actual number of agents after environment reset
        decision_steps, _ = ray.get(
            self.unity_env_handle.get_steps.remote(self._behavior_name)
        )

        # Get the number of active agents
        self.num_active_agents = len(decision_steps.agent_id)
        print(f"Initial number of agents: {self.num_active_agents}")

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
                    # Source: https://highway-env.farama.org/_modules/highway_env/envs/common/action/#ContinuousAction
                    low=np.array([-np.pi/4, 0.0, -5.0]),  # -45 degrees
                    high=np.array([np.pi/4, 5.0, 0.0]),   # +45 degrees

                    shape=(self.behavior_spec.action_spec.continuous_size,),
                    dtype=np.float32,
                ),
            )
        )

        # Establish observation and action spaces
        self._update_spaces()

        # ML-Agents API version.
        self.api_version = ray.get(self.unity_env_handle.get_api_version.remote())
        print(f"API Version: {self.api_version}")

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
        self.observation_spaces = gym.spaces.Dict({
            agent_id: self.single_agent_obs_space
            for agent_id in self.possible_agents
        })

        self.action_spaces = gym.spaces.Dict({
            agent_id: self.single_agent_action_space
            for agent_id in self.possible_agents
        })

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
            for agent_id, space in self.observation_spaces.items()
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
            for agent_idx, act_sample in self.action_spaces.items()
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
        # Extract bounds for clipping continuous actions
        low_bounds = self._single_agent_action_space[1].low
        high_bounds = self._single_agent_action_space[1].high

        # Split the actions into continuous and discrete actions
        continuous_actions = []
        discrete_actions = []

        for agent_id, agent_actions in actions_dict.items():
            # Agent actions is a tuple where the first element is discrete and the rest are continuous
            discrete_action, continuous_action = agent_actions

            # Clip continuous actions to their defined range
            continuous_action = np.clip(continuous_action, low_bounds, high_bounds)

            discrete_actions.append([discrete_action])
            continuous_actions.append(list(continuous_action))

        # Convert to numpy arrays
        discrete_actions = np.array(discrete_actions, dtype=np.int32)
        continuous_actions = np.array(continuous_actions, dtype=np.float32)

        # Alternative use of ActionTuple.add_discrete(discrete_actions) and ActionTuple.add_continuous(continuous_actions)
        return ActionTuple(continuous=continuous_actions, discrete=discrete_actions)

    def reset(
        self, *, seed: Optional[int] = None, options: Optional[dict] = None
    ) -> Union[Any, Tuple[Any, Dict]]:
        """
        Resets the environment to an initial state and returns the initial observation.

        Args:
            seed (Optional[int]): Optional integer to seed the environment's RNG.
            options (Optional[dict]): Optional parameters to customize reset behavior.

        Returns:
            Union[Any, Tuple[Any, dict]]: Initial observation and info dictionary.

        Raises:
            EnvironmentError: If the Unity environment fails to reset properly.
        """
        self.episode_timesteps = 0

        # Handle seed if provided
        if seed is not None:
            np.random.seed(seed)

        # Update agent count if specified in options
        if self._should_update_agent_count(options):
            self._update_agent_count(options["new_agent_count"])

        # Reset Unity environment and get initial state
        try:
            ray.get(self.unity_env_handle.reset.remote())
            decision_steps, terminal_steps = ray.get(
                self.unity_env_handle.get_steps.remote(self._behavior_name)
            )
        except Exception as e:
            raise EnvironmentError(f"Failed to reset Unity environment: {e}")

        # Validate environment state
        if not self._is_valid_environment_state(decision_steps):
            return self._create_default_observations(), {}

        # Update spaces and reset agent list
        self._update_spaces()
        self.agents = self.possible_agents.copy()

        # Create and validate observations
        obs_dict = self._create_observations(decision_steps)
        if not obs_dict:
            return self._create_default_observations(), {}

        self._validate_observations(obs_dict)

        return obs_dict, {}

    def _should_update_agent_count(self, options: Optional[dict]) -> bool:
        """Determines if agent count should be updated based on options."""
        if not options or "new_agent_count" not in options:
            return False
        return options["new_agent_count"] != self.current_agent_count

    def _update_agent_count(self, new_agent_count: int) -> None:
        """Updates the environment's agent count and related properties."""
        ray.get(
            self.unity_env_handle.set_float_property.remote(
                "initialAgentCount", new_agent_count
            )
        )
        print(f"Setting new agent count to: {new_agent_count}")
        self.possible_agents = [f"agent_{i}" for i in range(new_agent_count)]
        self.agents = self.possible_agents.copy()

    def _is_valid_environment_state(self, decision_steps) -> bool:
        """Validates the environment state after reset."""
        if decision_steps is None:
            print("Warning: No decision steps returned, the environment might not be initialized.")
            return False

        if not any(agent_id in decision_steps for agent_id in self.possible_agents):
            print("Warning: No active agents found after reset.")
            return False

        return True

    def _create_observations(self, decision_steps) -> Dict[str, np.ndarray]:
        """Creates observation dictionary from decision steps."""
        obs_dict = {}
        for i, agent_id in enumerate(decision_steps.agent_id):
            obs = decision_steps[agent_id].obs[0].astype(np.float32)
            agent_key = f"agent_{i}"
            if agent_key in self.agents:
                obs_dict[agent_key] = obs
        return obs_dict

    def _validate_observations(self, obs_dict: Dict[str, np.ndarray]) -> None:
        """Validates observations against their defined spaces."""
        for agent_id, obs in obs_dict.items():
            if not self.observation_spaces[agent_id].contains(obs):
                print(f"Warning: Observation for {agent_id} is out of bounds:")
                print(f"Observation: {obs}")
                print(f"Observation space: {self.observation_spaces[agent_id]}")

    def _create_default_observations(self) -> Dict[str, np.ndarray]:
        """Creates default zero-filled observations for all agents."""
        return {
            agent_id: np.zeros(self.observation_spaces[agent_id].shape, dtype=np.float32)
            for agent_id in self.possible_agents
        }

    def step(
        self, action_dict: MultiAgentDict
    ) -> Tuple[
        MultiAgentDict, MultiAgentDict, MultiAgentDict, MultiAgentDict, MultiAgentDict
    ]:
        """
        Steps the environment with the given actions.

        Args:
            action_dict (Dict[str, Any]): A dictionary mapping agent IDs to actions.

        Returns:
            Tuple[Dict[str, Any], Dict[str, float], Dict[str, bool], Dict[str, Any]]:
                A tuple containing observations, rewards, done flags, and additional info.
        """
        action_tuple = self._convert_to_action_tuple(action_dict)
        ray.get(self.unity_env_handle.set_actions.remote(self._behavior_name, action_tuple))

        # Step the Unity environment
        ray.get(self.unity_env_handle.step.remote())

        obs_dict, rewards_dict, terminateds_dict, truncateds_dict, infos_dict = (
            self._get_step_results()
        )

        # Global horizon reached? -> Return __all__ truncated=True, so user
        # can reset. Set all agents' individual `truncated` to True as well.
        self.episode_timesteps += 1

        if self.episode_timesteps > self.max_episode_steps:
            truncateds_dict = dict(
                {"__all__": True}, **{agent_id: True for agent_id in self.agents}
            )

        # Check if all agents are terminated
        terminateds_dict["__all__"] = all(terminateds_dict.values())

        # Check if all agents are truncated
        truncateds_dict["__all__"] = any(truncateds_dict.values())

        return obs_dict, rewards_dict, terminateds_dict, truncateds_dict, infos_dict

    def _get_step_results(self):
        """Collects those agents' obs/rewards that have to act in next `step`.
        Returns:
            Tuple:
                obs: Multi-agent observation dict.
                    Only those observations for which to get new actions are
                    returned.
                rewards: Rewards dict matching `obs`.
                terminateds: Terminated dict with only an __all__ multi-agent entry in it.
                    __all__=True, if episode is done for all agents.
                truncateds: Truncated dict.
                infos: Info dict for additional information.
        """
        # Process observations, rewards, and done flags
        obs_dict = {}
        rewards_dict = {}
        terminateds_dict = {}
        truncateds_dict = {}
        infos_dict = {}

        # Get the new state for our single behavior
        decision_steps, terminal_steps = ray.get(self.unity_env_handle.get_steps.remote(self._behavior_name))

        # Check if decision steps is None
        if decision_steps is None:
            print("Warning: No decision steps returned, the environment might not be initialized.")
            return {}, {}, {}, {}, {}

        # Process agents in decision steps (active agents)
        for agent_id in decision_steps.agent_id:
            agent_key = f"agent_{agent_id}"
            obs_dict[agent_key] = decision_steps[agent_id].obs[0].astype(np.float32)
            rewards_dict[agent_key] = decision_steps[agent_id].reward
            terminateds_dict[agent_key] = False
            truncateds_dict[agent_key] = False  # Assume not truncated if in decision_steps
            infos_dict[agent_key] = {}

        # Process agents in terminal steps (terminated agents)
        for agent_id in terminal_steps.agent_id:
            agent_key = f"agent_{agent_id}"
            obs_dict[agent_key] = terminal_steps[agent_id].obs[0].astype(np.float32)
            rewards_dict[agent_key] = terminal_steps[agent_id].reward
            terminateds_dict[agent_key] = True
            truncateds_dict[agent_key] = terminal_steps[agent_id].interrupted
            infos_dict[agent_key] = {}

        # All Agents Done Check: Only use dones if all agents are done
        terminateds_dict["__all__"] = len(terminal_steps) == self.num_active_agents
        truncateds_dict["__all__"] = all(truncateds_dict.values())

        return obs_dict, rewards_dict, terminateds_dict, truncateds_dict, infos_dict

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
        ray.get(self.unity_env_handle.close.remote())
