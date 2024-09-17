import os
from typing import Any, Dict, Optional, Tuple, Union
import uuid
import gymnasium as gym
import mlflow
import numpy as np

from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mlagents_envs.side_channel.float_properties_channel import FloatPropertiesChannel

import ray
from ray import tune
from ray.tune import Tuner
from ray.train import RunConfig
from ray.air.integrations.mlflow import MLflowLoggerCallback
from ray.rllib.algorithms.ppo import PPO
from ray.rllib.env.env_context import EnvContext
from ray.rllib.env.multi_agent_env import MultiAgentEnv
from ray.rllib.utils.typing import MultiAgentDict, PolicyID, AgentID
from ray.tune.registry import register_env
from ray.air.integrations.mlflow import MLflowLoggerCallback
from ray.rllib.algorithms.ppo import PPO
from ray.rllib.env.env_context import EnvContext
from ray.rllib.env.multi_agent_env import MultiAgentEnv
from ray.tune.registry import register_env

# Suppress DeprecationWarnings from output
os.environ["PYTHONWARNINGS"] = "ignore::DeprecationWarning"

@ray.remote
class UnityEnvResource:
    """
    A resource class that manages the Unity environment, providing methods to interact with it.

    Attributes:
        channel_id (uuid.UUID): Unique identifier for the float properties channel.
        engine_configuration_channel (EngineConfigurationChannel): Channel for configuring the Unity engine.
        float_props_channel (FloatPropertiesChannel): Channel for setting float properties in Unity.
        unity_env (UnityEnvironment): The Unity environment instance.
    """
    def __init__(self, file_name: str, worker_id: int = 0, base_port: int = 5004, no_graphics: bool = False):
        """
        Initializes the Unity environment with specified configuration channels.

        Args:
            file_name (str): Path to the Unity executable.
            worker_id (int): Worker ID for parallel environments.
            base_port (int): Base port for communication with Unity.
        """
        self.channel_id = uuid.UUID("621f0a70-4f87-11ea-a6bf-784f4387d1f7")
        self.engine_configuration_channel = EngineConfigurationChannel()
        self.float_props_channel = FloatPropertiesChannel(self.channel_id)

        # Initialize the Unity environment with communication channels
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
            no_graphics=no_graphics,
            seed=42,
        )

    def set_float_property(self, key: str, value: float):
        """
        Sets a float property in the Unity environment.

        Args:
            key (str): The key for the float property.
            value (float): The value to set for the property.
        """
        self.float_props_channel.set_property(key, float(value))

    def get_behavior_specs(self) -> Dict[str, Any]:
        """
        Retrieves the behavior specifications from the Unity environment.

        Returns:
            Dict[str, Any]: The behavior specifications.
        """
        return self.unity_env.behavior_specs

    def get_steps(self, behavior_name: str) -> Tuple:
        """
        Gets the current steps (observations) from the Unity environment.

        Args:
            behavior_name (str): The behavior name to get steps for.

        Returns:
            Tuple: Decision steps and terminal steps.
        """
        return self.unity_env.get_steps(behavior_name)

    def set_actions(self, behavior_name: str, action: ActionTuple):
        """
        Sends actions to the Unity environment.

        Args:
            behavior_name (str): The behavior name to set actions for.
            action (ActionTuple): The actions to apply.
        """
        return self.unity_env.set_actions(behavior_name, action)

    def reset(self):
        """
        Resets the Unity environment.

        Returns:
            None
        """
        return self.unity_env.reset()

    def step(self):
        """
        Advances the Unity environment by one step.

        Returns:
            None
        """
        return self.unity_env.step()

    def close(self):
        """
        Closes the Unity environment to free resources.

        Returns:
            None
        """
        if hasattr(self, "unity_env") and self.unity_env is not None:
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
    """
    Custom multi-agent environment for Unity.

    This environment simulates a scenario with multiple agents. Each agent can perform
    both high-level and low-level actions within the Unity simulation.
    """
    def __init__(self, config: EnvContext, *args, **kwargs):
        """
        Initializes the multi-agent environment.

        Args:
            config (EnvContext): Configuration dictionary containing environment settings.
        """
        super().__init__()
        self.initial_agent_count = config.get("initial_agent_count", 2)
        self.unity_env_handle = config["unity_env_handle"]
        self.current_agent_count = self.initial_agent_count

        # Set the initial agent count in the Unity environment
        ray.get(
            self.unity_env_handle.set_float_property.remote(
                "initialAgentCount", self.initial_agent_count
            )
        )

        # Start the Unity environment
        print(f"Initializing with {self.initial_agent_count} agents")

        # Reset the Unity environment
        ray.get(self.unity_env_handle.reset.remote())

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

        # Add the private attribute `_agent_ids`, which is a set containing the ids of agents supported by the environment
        self._agent_ids = {f"agent_{i}" for i in range(self.num_agents)}

        # Establish observation and action spaces
        self._update_spaces()

    def _update_spaces(self):
        """
        Updates the observation and action spaces for all agents.

        Returns:
            None
        """
        # Create the observation space for a single agent
        single_agent_obs_space = gym.spaces.Box(
            low=-np.inf,
            high=np.inf,
            shape=(self.size_of_single_agent_obs,),
            dtype=np.float32,
        )

        # Create the action space for a single agent
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

        # Populate observation_spaces and action_spaces for each agent
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

    def reset(self, *, seed: Optional[int] = None, options: Optional[dict] = None) -> Union[Any, Tuple[Any, Dict]]:
        """
        Resets the environment to an initial state and returns the initial observation.

        This method is used to reset the environment at the beginning of an episode.
        If a seed is provided, it ensures the environment's behavior is deterministic 
        by starting from a reproducible state. Additional options can be passed to 
        modify the reset behavior.

        Args:
            seed (Optional[int]): An optional integer to seed the environment's random number generator.
            options (Optional[dict]): An optional dictionary with parameters to customize the reset behavior.

        Returns:
            Union[Any, Tuple[Any, dict]]: 
                - Any: The initial observation of the environment after resetting.
                - Tuple[Any, Dict]: A tuple containing the initial observation and an optional info dictionary.
        """
        # Handle seed if provided
        if seed is not None:
            np.random.seed(seed)

        # Check if options contain a new agent count
        if options and "new_agent_count" in options:
            new_agent_count = options["new_agent_count"]

            if new_agent_count != self.current_agent_count:
                ray.get(
                    self.unity_env_handle.set_float_property.remote(
                        "initialAgentCount", new_agent_count
                    )
                )
                print(f"Setting new agent count to: {new_agent_count}")

        # Reset the environment only once (ideally)
        ray.get(self.unity_env_handle.reset.remote())
        # ray.get(self.unity_env_handle.reset.remote())

        # Get decision steps after the reset
        decision_steps, _ = ray.get(
            self.unity_env_handle.get_steps.remote(self.behavior_name)
        )
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
        ray.get(self.unity_env_handle.set_actions.remote(self.behavior_name, action_tuple))

        # Step the Unity environment
        ray.get(self.unity_env_handle.step.remote())

        # Get the new state
        decision_steps, terminal_steps = ray.get(
            self.unity_env_handle.get_steps.remote(self.behavior_name)
        )

        # Process observations, rewards, and done flags
        obs_dict = {}
        rewards_dict = {}
        terminateds_dict = {}
        truncateds_dict = {}
        infos_dict = {}

        # Alternative, decision_steps.agent_id_to_index
        for agent_id in decision_steps.agent_id:
            agent_key = f"agent_{agent_id}"
            obs_dict[agent_key] = decision_steps[agent_id].obs[0].astype(np.float32)
            rewards_dict[agent_key] = decision_steps[agent_id].reward
            terminateds_dict[agent_key] = False
            truncateds_dict[agent_key] = False  # Assume not truncated if in decision_steps
            infos_dict[agent_key] = {}

        for agent_id in terminal_steps.agent_id:
            agent_key = f"agent_{agent_id}"
            obs_dict[agent_key] = terminal_steps[agent_id].obs[0].astype(np.float32)
            rewards_dict[agent_key] = terminal_steps[agent_id].reward
            terminateds_dict[agent_key] = True
            truncateds_dict[agent_key] = terminal_steps[agent_id].interrupted
            infos_dict[agent_key] = {}

        # Check if all agents are terminated
        terminateds_dict["__all__"] = all(terminateds_dict.values())

        # Check if all agents are truncated
        truncateds_dict["__all__"] = any(truncateds_dict.values())

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


def create_unity_env(file_name: str, worker_id: int = 0, base_port: int = 5004, no_graphics: bool = False) -> UnityEnvResource:
    """
    Creates a Unity environment resource for use with RLlib.

    Args:
        file_name (str): Path to the Unity environment binary.
        worker_id (int): Worker ID for parallel environments.
        base_port (int): Base port for communication with Unity.
        no_graphics (bool): Whether to run the Unity environment in no-graphics mode.

    Returns:
        UnityEnvResource: The Unity environment resource.
    """
    return UnityEnvResource.remote(file_name, worker_id, base_port, no_graphics)


def main():
    """
    Main function to initialize the Unity environment and start the training process.

    Returns:
        None
    """

    # Determine the current directory where the script is running
    current_dir = os.path.dirname(os.path.abspath(__file__))

    # Get the base directory by moving up one level (assuming the script is in 'rl' folder)
    base_dir = os.path.dirname(current_dir)

    # Construct the full path to the Unity executable
    unity_executable_path = os.path.join(base_dir, "libReplicantDriveSim.app")

    # Initialize Ray
    ray.init(ignore_reinit_error=True)

    unity_env_handle = create_unity_env(
        file_name=unity_executable_path,  # Path to your Unity executable
        worker_id=0,
        base_port=5004,
        no_graphics=False,
    )

    # Register the environment with RLlib
    env_name = "CustomUnityMultiAgentEnv"
    register_env(
        env_name,
        lambda config: CustomUnityMultiAgentEnv(config, unity_env_handle=unity_env_handle),
    )

    # Define the configuration for the PPO algorithm
    config = PPO.get_default_config()
    config = config.environment(env=env_name, env_config={"initial_agent_count": 2, "unity_env_handle": unity_env_handle})
    config = config.framework("torch")
    config = config.resources(num_gpus=0)

    # Multi-agent configuration
    config = config.multi_agent(
        policies={
            # Define our policies here (e.g., "default_policy")
            "shared_policy": (
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
        policy_mapping_fn=lambda agent_id, episode, worker, **kwargs: "shared_policy",
    )

    # Rollout configuration
    # Setting num_env_runners=0 will only create the local worker, in which case both sample collection 
    # and training will be done by the local worker. On the other hand, setting num_env_runners=5 
    # will create the local worker (responsible for training updates)
    # and 5 remote workers (responsible for sample collection).
    config = config.rollouts(
        num_rollout_workers=2,
        num_envs_per_worker=1,
        rollout_fragment_length=200,
        batch_mode="truncate_episodes"
    )

    # Training configuration
    config = config.training(
        train_batch_size=4000,
        sgd_minibatch_size=128,
        num_sgd_iter=30,
        lr=3e-4,
        gamma=0.99,
        lambda_=0.95,
        clip_param=0.2,
        vf_clip_param=10.0,
        entropy_coeff=0.01,
        kl_coeff=0.5,
        vf_loss_coeff=1.0,
    )

    # Source: https://discuss.ray.io/t/agent-ids-that-are-not-the-names-of-the-agents-in-the-env/6964/3
    config = config.environment(disable_env_checking= True)

    # Set up MLflow logging
    mlflow.set_experiment("MARLExperiment")

    # Initialize PPO trainer
#    trainer = PPO(config=config)

    # Training loop
#    for i in range(1):
        # Generate a new agent count
#        new_agent_count = np.random.randint(
#            1, 5
#        )  # Choose a random number of agents between 1 and 10
#        print(f"Episode {i} number of agents: ", new_agent_count)

        # Update the env_config with the new agent count
#        trainer.config["env_config"]["initial_agent_count"] = new_agent_count

        # Update all worker environments
#        def update_env(env):
#            if hasattr(env, 'reset') and callable(env.reset):
#                env.reset(options={"new_agent_count": new_agent_count})

#        trainer.workers.foreach_worker(lambda worker: worker.foreach_env(update_env))

        # Train for one iteration
#        result = trainer.train()
#        print(f"Iteration {i}: reward_mean={result['episode_reward_mean']}")

#        if i % 10 == 0:  # Save a checkpoint every 10 iterations
#            checkpoint = trainer.save()
#            print(f"Checkpoint saved at {checkpoint}")

    results = tune.run(
        PPO,
        config=config,
        checkpoint_freq=5,  # Set checkpoint frequency here
        num_samples=1,      # Number of times to repeat the experiment
        max_failures=1,     # Maximum number of failures before stopping the experiment
        verbose=1,  # Verbosity level for logging
        callbacks=[MLflowLoggerCallback(
            experiment_name="MARLExperiment",
            tracking_uri=mlflow.get_tracking_uri(),
            #tracking_uri="file://" + os.path.abspath("./mlruns"),
            save_artifact=True,
        )],
        stop={"training_iteration": 1}, # storage_path=
        local_dir="./ray_results", # default ~/ray_results
        name="PPO_Highway_Experiment",
    )

    # Print the results dictionary of the training to inspect the structure
    print("Training results: ", results)

    # Get the best trial based on a metric (e.g., 'episode_reward_mean')
    best_trial = results.get_best_trial("episode_reward_mean", mode="max")

    if best_trial:
        best_checkpoint = best_trial.checkpoint

        if best_checkpoint:
            # Load the model from the best checkpoint
            best_model = PPO.from_checkpoint(best_checkpoint)

            # Register the model
            with mlflow.start_run(run_name="model_registration") as run:
                # Log model
                mlflow.pytorch.log_model(
                    best_model.get_policy().model,
                    "ppo_model",
                    registered_model_name="PPO_Highway_Model",
                )
                print(f"Model registered with run ID: {run.info.run_id}")
        else:
            print("No best trial found. Model registration skipped.")

    # Make sure to close the Unity environment at the end
    ray.get(unity_env_handle.close.remote())

    ray.shutdown()


if __name__ == "__main__":
    main()
