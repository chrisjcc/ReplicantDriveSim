import os
import uuid
from typing import Any, Dict, Optional, Tuple

import ray
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mlagents_envs.side_channel.float_properties_channel import FloatPropertiesChannel

from utils import CustomSideChannel 

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
    def __init__(
        self,
        file_name: str,
        worker_id: int = 0,
        base_port: int = 5004,
        no_graphics: bool = False,
    ) -> None:
        """
        Initializes the Unity environment with specified configuration channels.

        Args:
            file_name (str): Path to the Unity executable.
            worker_id (int): Worker ID for parallel environments.
            base_port (int): Base port for communication with Unity.
            no_graphics (bool): Whether to launch Unity without graphics.
        """
        self.channel_id = uuid.UUID("621f0a70-4f87-11ea-a6bf-784f4387d1f7")
        self.engine_configuration_channel = EngineConfigurationChannel()
        self.float_props_channel = FloatPropertiesChannel(self.channel_id)
        self.field_value_channel = CustomSideChannel()

        # Note: Communication Layer: ML-Agents uses a communication protocol (gRPC) to transfer data between Unity and Python.
        # This protocol serializes the observation data into a format that can be sent over the network.
        # When we create a UnityEnvironment object, it establishes a connection with the Unity.
        # When we call e.g env.resent() or env.step(), it triggers Unity to advance its simulation by one step.
        # After this, Unity sends the new observations back to Python.
        # We can get the observations using the get_steps() method.
        # The decision_steps and terminal_steps objects contain the observations for agents that are still active
        # and those that have terminated, respectively.

        # Initialize the Unity environment with communication channels
        self.unity_env = UnityEnvironment(
            file_name=file_name,
            worker_id=worker_id,
            base_port=base_port,
            side_channels=[self.engine_configuration_channel, self.float_props_channel, self.field_value_channel],
            no_graphics=no_graphics,
            seed=42,
        )

    def set_float_property(self, key: str, value: float) -> None:
        """
        Sets a float property in the Unity environment.

        Args:
            key (str): The key for the float property.
            value (float): The value to set for the property.
        """
        self.float_props_channel.set_property(key, value)

    def get_field_value(self, key_field: str = "FramesPerSecond") -> Dict[str, Optional[float]]:
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
        return self.unity_env.get_steps(behavior_name)

    def set_actions(self, behavior_name: str, action: ActionTuple) -> None:
        """
        Sends actions to the Unity environment.

        Args:
            behavior_name (str): The behavior name to set actions for.
            action (ActionTuple): The actions to apply.
        """
        self.unity_env.set_actions(behavior_name, action)

    def reset(self) -> None:
        """
        Resets the Unity environment.

        Returns:
            None
        """
        if not self.unity_env:
            raise ValueError("Unity environment is not initialized.")
        self.unity_env.reset()

    def step(self) -> None:
        """
        Advances the Unity environment by one step.

        Returns:
            None
        """
        self.unity_env.step()

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
        else:
            print("Unity environment is already closed or was not properly initialized.")

    def __enter__(self) -> 'UnityEnvResource':
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


def create_unity_env(
    file_name: str, worker_id: int = 0, base_port: int = 5004, no_graphics: bool = False
) -> UnityEnvResource:
    """
    Creates a Unity environment resource for use with RLlib.

    Args:
        file_name (str): Path to the Unity environment binary.
        worker_id (int): Worker ID for parallel environments.
        base_port (int): Base port for communication with Unity.
        no_graphics (bool): Whether to launch Unity without graphics.

    Returns:
        Optional[UnityEnvResource]: The Unity environment resource, or None if there is an error.
    """
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

    return UnityEnvResource.remote(
        file_name=file_name,
        worker_id=worker_id,
        base_port=base_port,
        no_graphics=no_graphics,
    )
