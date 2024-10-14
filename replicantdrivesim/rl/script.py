import os
import yaml
import ray
import replicantdrivesim

def get_unity_executable_path():
    # Get the package's directory and locate the Unity executable
    package_dir = os.path.dirname(replicantdrivesim.__file__)
    system = platform.system()
        
    if system == 'Darwin':  # macOS
        unity_executable_path = os.path.join(package_dir, 'Builds', 'macOS', 'libReplicantDriveSim.app')
    elif system == 'Windows':
        unity_executable_path = os.path.join(package_dir, 'Builds', 'Windows', 'libReplicantDriveSim.exe')
    elif system == 'Linux':
        unity_executable_path = os.path.join(package_dir, 'Builds', 'Linux', 'libReplicantDriveSim.x86_64')
    else:
        raise NotImplementedError(f"Unity executable not available for platform {system}")
    
    if not os.path.exists(unity_executable_path):
        raise FileNotFoundError(f"Unity executable not found at {unity_executable_path}")

    return unity_executable_path



def make(env_name):
    # Load configuration (this could be more flexible based on env_name)
    config_path = os.path.join(os.path.dirname(__file__), "configs", "config.yaml")
    config_data = load_config(config_path)
    
    # Automatically get the Unity executable path
    unity_executable_path = replicantdrivesim.get_unity_executable_path()
    
    # Create the Unity environment
    env = create_env(config_data, unity_executable_path)
    
    return env


env = replicantdrivesim.make("replicantdrivesim-v0")
