import os
import platform
import sys

import yaml

# Add the package root directory to sys.path
package_root = os.path.abspath(os.path.dirname(__file__))
if package_root not in sys.path:
    sys.path.insert(0, package_root)

# Dynamically get the version
try:
    from importlib.metadata import PackageNotFoundError, version

    try:
        __version__ = version("ReplicantDriveSim")
    except PackageNotFoundError:
        # package is not installed
        __version__ = "unknown"
except ImportError:
    # Fallback for Python < 3.8
    try:
        from importlib_metadata import PackageNotFoundError, version

        try:
            __version__ = version("ReplicantDriveSim")
        except PackageNotFoundError:
            __version__ = "unknown"
    except ImportError:
        __version__ = "unknown"

try:
    from .replicantdrivesim import Traffic, Vehicle  # Import C++ bindings
except ImportError as e:
    print(f"Error importing C++ bindings: {e}")
    print(f"Current sys.path: {sys.path}")
    print(f"Current working directory: {os.getcwd()}")
    raise

from .envs.environment import CustomUnityMultiAgentEnv
from .envs.unity_env_resource import create_unity_env


def get_unity_executable_path():
    # Get the package's directory and locate the Unity executable
    package_dir = os.path.dirname(__file__)
    system = platform.system()

    # Construct the full path to the Unity executable
    if system == "Darwin":  # macOS
        unity_executable_path = os.path.join(
            package_dir, "Builds", "StandaloneOSX", "libReplicantDriveSim.app"
        )
    elif system == "Windows":
        unity_executable_path = os.path.join(
            package_dir, "Builds", "Windows", "libReplicantDriveSim.exe"
        )
    elif system == "Linux":
        unity_executable_path = os.path.join(
            package_dir, "Builds", "Linux", "libReplicantDriveSim.x86_64"
        )
    else:
        raise NotImplementedError(
            f"Unity executable not available for platform {system}"
        )

    if not os.path.exists(unity_executable_path):
        raise FileNotFoundError(
            f"Unity executable not found at {unity_executable_path}"
        )

    return unity_executable_path


def make(env_name, config: dict):
    """Create a Unity environment using the given configuration."""
    if env_name == "replicantdrivesim-v0":
        # Automatically get the Unity executable path
        unity_executable_path = get_unity_executable_path()

        config.update({"file_name": unity_executable_path})

        unity_env_handle = create_unity_env(config=config)

        # Update configuration to include unity environment handler
        config.update({"unity_env_handle": unity_env_handle})

        return CustomUnityMultiAgentEnv(config=config)
    else:
        raise ValueError(f"Unknown environment: {env_name}")


# Explicitly add 'make' to __all__
__all__ = [
    "Traffic",
    "Vehicle",
    "get_unity_executable_path",
    "make",
    "CustomUnityMultiAgentEnv",
    "__version__",
]

# Print a message to confirm this file is being executed
print(f"replicantdrivesim (version {__version__})")
