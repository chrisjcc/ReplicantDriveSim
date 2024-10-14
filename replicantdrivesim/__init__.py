# replicantdrivesim/__init__.py

import os
import yaml
from .replicantdrivesim import Traffic, Vehicle  # Import C++ bindings
from .rl.environment import CustomUnityMultiAgentEnv
from .rl.unity_env_resource import create_unity_env


def load_config(config_path: str, config_schema_path: str):
    """Load and validate configuration files."""
    with open(config_path, "r") as config_file:
        config_data = yaml.safe_load(config_file)

    # Optionally add schema validation here if needed
    return config_data

def make(env_name):
    """Create a Unity environment using the given configuration."""
    if env_name == "replicantdrivesim-v0":
        # Determine the current directory where the script is running
        current_dir = os.path.dirname(os.path.abspath(__file__))

        # Set YAML file paths
        config_path = os.path.join(current_dir, "configs", "config.yaml")
        config_schema_path = os.path.join(current_dir, "configs", "config_schema.yaml")

        # Load configuration from YAML
        config_data = load_config(config_path, config_schema_path)

        # Get the base directory by moving up one level (assuming the script is in 'replicantdrivesim' folder)
        base_dir = os.path.dirname(current_dir)

        # Construct the full path to the Unity executable
        unity_executable_path = os.path.join(
            base_dir,  os.path.join("Builds", "StandaloneOSX", "libReplicantDriveSim.app")
        )

        unity_env_handle = create_unity_env(
            file_name=unity_executable_path,
            worker_id=0,
            base_port=config_data["unity_env"]["base_port"],
            no_graphics=config_data["unity_env"]["no_graphics"],
        )

        env_config = {
            "initial_agent_count": config_data["env_config"]["initial_agent_count"],
            "unity_env_handle": unity_env_handle,
            "episode_horizon": config_data["env_config"]["episode_horizon"],
        }

        return CustomUnityMultiAgentEnv(config=env_config, unity_env_handle=unity_env_handle)
    else:
        raise ValueError(f"Unknown environment: {env_name}")

# Explicitly add 'make' to __all__
__all__ = ['Traffic', 'Vehicle', 'make', 'CustomUnityMultiAgentEnv']

# Print a message to confirm this file is being executed
print("replicantdrivesim __init__.py executed")
