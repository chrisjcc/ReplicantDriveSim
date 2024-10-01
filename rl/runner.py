import os
import yaml
import ray
import argparse
from environment import CustomUnityMultiAgentEnv
from unity_env_resource import create_unity_env

def load_config(config_path, config_schema_path):
    """Load and validate configuration files."""
    with open(config_path, "r") as config_file:
        config_data = yaml.safe_load(config_file)
    # Optionally add schema validation here if needed
    return config_data

def create_env(config_data, unity_executable_path):
    """Create a Unity environment using the given configuration."""

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
    
    env = CustomUnityMultiAgentEnv(config=env_config, unity_env_handle=unity_env_handle)
    return env

def run_episodes(env, num_episodes):
    """Run a defined number of episodes with the environment."""
    for episode in range(num_episodes):
        print(f"Starting episode {episode + 1}")

        observations, _ = env.reset()
        done = False

        while not done:
            actions = env.action_space_sample()

            # Modify the actions for all agents
            for agent in actions:
                discrete_action, continuous_actions = actions[agent]
 
                # Modify the continuous actions
                modified_continuous_actions = continuous_actions.copy()
                #modified_continuous_actions[0] = 0.0  # Set steering to zero
                #modified_continuous_actions[1] = 0.1  # Some other modification
                #modified_continuous_actions[2] = 0.0  # Another modification

                actions[agent] = (discrete_action, modified_continuous_actions)

            # Print the modified actions
            print(actions)

            # Step the environment
            observations, rewards, terminateds, truncateds, infos = env.step(actions)
            print("rewards: ", rewards)

            # Check if the episode is done
            done = terminateds.get("__all__", False) or truncateds.get("__all__", False)

        print(f"Episode {episode + 1} finished")

def main():

    # Set up argument parser
    parser = argparse.ArgumentParser(description="Run a Unity environment simulation.")
    parser.add_argument(
        "--num_episodes",
        type=int, 
        default=10, 
        help="Number of episodes to run (default: 10)"
    )

    # Parse command-line arguments
    args = parser.parse_args()

    num_episodes = args.num_episodes

    # Determine the current directory where the script is running
    current_dir = os.path.dirname(os.path.abspath(__file__))

    # Set YAML file paths
    config_path = os.path.join(current_dir, "configs", "config.yaml")
    config_schema_path = os.path.join(current_dir, "configs", "config_schema.yaml")

    # Load configuration from YAML
    config_data = load_config(config_path, config_schema_path)

    # Get the base directory by moving up one level (assuming the script is in 'rl' folder)
    base_dir = os.path.dirname(current_dir)

    # Construct the full path to the Unity executable
    unity_executable_path = os.path.join(
        base_dir, "Builds/macOS/libReplicantDriveSim.app"
    )

    # Initialize Ray
    ray.init()

    # Create the Unity environment
    env = create_env(config_data, unity_executable_path)

    # Run the episodes
    run_episodes(env, num_episodes)

    # Close the environment
    env.close()

if __name__ == "__main__":
    main()
