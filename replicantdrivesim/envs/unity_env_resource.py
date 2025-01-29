import os
import uuid
from typing import Any, Dict, Optional, Tuple

import ray
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.exception import UnityCommunicatorStoppedException
from mlagents_envs.side_channel.engine_configuration_channel import (
    EngineConfig,
    EngineConfigurationChannel,
)
from mlagents_envs.side_channel.environment_parameters_channel import (
    EnvironmentParametersChannel,
)
from mlagents_envs.side_channel.float_properties_channel import FloatPropertiesChannel

from .utils import CustomSideChannel

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

    def __init__(self, config: dict = None) -> None:
        """
        Initializes the Unity environment with specified configuration channels.

        Args:
            config (dict): Unity environment configuration parameters (e.g. number of agents).
        """
        self.channel_id = uuid.UUID("621f0a70-4f87-11ea-a6bf-784f4387d1f7")
        self.engine_configuration_channel = EngineConfigurationChannel()
        self.env_parameters = EnvironmentParametersChannel()
        self.float_properties_channel = FloatPropertiesChannel(self.channel_id)
        self.field_value_channel = CustomSideChannel()

        # Extract Unity and Gym-based environment configuration
        self.env_config = config.get("env_config", {})
        self.unity_env_config = config.get("unity_env", {})

        # Set the number of desired active agents
        self.initial_agent_count = self.env_config.get("initial_agent_count", 2)

        # Send initial agent count to Unity
        self.env_parameters.set_float_parameter(
            "initialAgentCount", self.initial_agent_count
        )

        # Prepare reward signal configuration
        self.rewards = self.env_config.get("rewards", {})

        # Parse and send reward configurations
        self._set_reward_parameters()

        # Note: Communication Layer: ML-Agents uses a communication protocol (gRPC) to transfer data between Unity and Python.
        # This protocol serializes the observation data into a format that can be sent over the network.
        # When we create a UnityEnvironment object, it establishes a connection with the Unity.
        # When we call e.g env.resent() or env.step(), it triggers Unity to advance its simulation by one step.
        # After this, Unity sends the new observations back to Python.
        # We can get the observations using the get_steps() method.
        # The decision_steps and terminal_steps objects contain the observations for agents that are still active
        # and those that have terminated, respectively.

        # Create the Unity environment with communication channels
        self.unity_env = UnityEnvironment(
            file_name=config[
                "file_name"
            ],  # Path to the Unity environment binary executable
            worker_id=self.unity_env_config.get(
                "worker_id", 0
            ),  # Worker ID for parallel environments
            base_port=self.unity_env_config.get(
                "base_port", 5004
            ),  # Base port for communication with Unity
            side_channels=[
                self.engine_configuration_channel,
                self.env_parameters,
                self.float_properties_channel,
                self.field_value_channel,
            ],
            no_graphics=self.unity_env_config.get(
                "no_graphics", False
            ),  # Whether to launch Unity without graphics
            log_folder=self.unity_env_config.get("log_folder", "./Logs"), # Directory to write the Unity Player log file into
            seed=self.unity_env_config.get("seed", 42),  # Environment random seed value
        )

    def set_configuration(self, engine_config: EngineConfig) -> None:
        """
        Sets an engine configuration in the Unity environment.
        """
        self.engine_configuration_channel.set_configuration(engine_config)

    def set_float_parameter(self, key: str, value: float) -> None:
        """
        Sets a float parameter in the Unity environment.

        Args:
            key (str): The key for the float property.
            value (float): The value to set for the property.
        """
        self.env_parameters.set_float_parameter(key, value)

    def set_float_property(self, key: str, value: float) -> None:
        """
        Sets a float property in the Unity environment.

        Args:
            key (str): The key for the float property.
            value (float): The value to set for the property.
        """
        self.float_properties_channel.set_property(key, value)

    def get_field_value(
        self, key_field: str = "FramesPerSecond"
    ) -> Dict[str, Optional[float]]:
        """
        Retrieves a field value from the Unity environment, if available.

        Args:
            key_field (str): The name of the field.

        Returns:
            Dict[str, Optional[float]]: A dictionary containing the field name and its value, or None if not available.
        """
        field_value = self.field_value_channel.get_field_value(key_field)

        if field_value is not None:
            print(f"Received {key_field} value: {field_value}")
        else:
            print(f"No value received for {key_field} yet.")

        return {key_field: field_value}

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
            Tuple: A tuple containing decision steps and terminal steps.
        """
        if not self.unity_env:
            print(
                "Unity environment is not initialized or already closed. Cannot get steps."
            )
            return None, None  # Return empty tuples as a fallback

        try:
            return self.unity_env.get_steps(behavior_name)
        except Exception as e:
            print(f"Error getting steps: {str(e)}")
            self.close()
            raise

    def set_actions(self, behavior_name: str, action: ActionTuple) -> None:
        """
        Sends actions to the Unity environment.

        Args:
            behavior_name (str): The behavior name to set actions for.
            action (ActionTuple): The actions to apply.
        """
        if not self.unity_env:
            print("Unity environment is not initialized or already closed.")
            return

        try:
            self.unity_env.set_actions(behavior_name, action)
        except Exception as e:
            print(f"Error during set_actions: {e}")
            self.close()
            raise

    def reset(self) -> None:
        """
        Resets the Unity environment.

        Returns:
            None
        """
        if not self.unity_env:
            print("Unity environment is not initialized or already closed.")
            return

        try:
            self.unity_env.reset()
        except Exception as e:
            print(f"Error during reset: {e}")
            self.close()
            raise

    def step(self) -> None:
        """
        Advances the Unity environment by one step.

        Handles user-initiated shutdowns gracefully.
        """
        if self.unity_env is None:
            print(
                "Unity environment is not initialized. Please start the environment before stepping."
            )
            self.close()
            return

        try:
            self.unity_env.step()
        except UnityCommunicatorStoppedException:
            print("Unity application was closed by the user. Shutting down gracefully.")
            self.close()
        except AttributeError as e:
            print(
                "Unity environment was unexpectedly set to None. Please check initialization."
            )
            self.close()
            raise ValueError(
                "Unity environment is None. Initialization error likely occurred."
            ) from e
        except Exception as e:
            print(f"Unexpected error during step: {str(e)}")
            self.close()
            raise

    def _set_reward_parameters(self):
        # Default values for rewards
        default_rewards = {
            "offRoadPenalty": -0.5,
            "onRoadReward": 0.01,
            "collisionWithOtherAgentPenalty": -1.0,
            "medianCrossingPenalty": -1.0,
        }

        # Merge defaults with YAML configuration
        for key, default_value in default_rewards.items():
            reward_value = self.rewards.get(key, default_value)
            self.env_parameters.set_float_parameter(key, reward_value)
            print(f"Set {key} to {reward_value}")

    def close(self) -> None:
        """
        Closes the Unity environment to free resources.

        Returns:
            None
        """
        if self.unity_env:
            try:
                self.unity_env.close()
            except Exception as e:
                print(f"Error closing Unity environment: {e}")
            finally:
                self.unity_env = None
                print("Unity environment closed successfully.")
        else:
            print(
                "Unity environment is already closed or was not properly initialized."
            )

    def __enter__(self) -> "UnityEnvResource":
        return self

    def __exit__(self, exc_type, exc_val, exc_tb) -> None:
        self.close()

    def get_api_version(self) -> str:
        """
        Retrieves the API version of the Unity environment.

        Returns:
            str: The API version.
        """
        return self.unity_env.API_VERSION


def create_unity_env(config: dict = None) -> UnityEnvResource:
    """
    Creates a Unity environment resource for use with RLlib.

    Args:
        config (dict): Unity environment configuration parameters (e.g. number of agents).

    Returns:
        Optional[UnityEnvResource]: The Unity environment resource, or None if there is an error.
    """
    # Path to the Unity environment binary
    file_name = config["file_name"]

    # Check if the file exists
    if not os.path.exists(file_name):
        print(f"\033[91mCheck\033[0m: The file '{file_name}' does not exist.")
        return None

    # Check if it's a directory
    if os.path.isdir(file_name):
        print(f"\033[92mChecked\033[0m: '{file_name}' is a directory.")
    else:
        print(f"\033[91mError\033[0m: '{file_name}' is not a directory.")
        return None

    return UnityEnvResource.remote(config=config)
