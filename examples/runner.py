import os
import yaml
import ray
import argparse
import replicantdrivesim
from mlagents_envs.exception import UnityCommunicatorStoppedException

def run_episodes(env, num_episodes):
    """Run a defined number of episodes with the environment."""
    try:
        for episode in range(num_episodes):
            print(f"Starting episode {episode + 1}")

            # Print available behavior names
            behavior_names = list(env.behavior_specs.keys())
            print(f"Available behavior names: {behavior_names}")

            observations, _ = env.reset()
            done = False

            while not done:
                actions = env.action_space_sample()
                print(f"Raw sampled actions: {actions}")

                # Modify the actions for all agents
                for agent in actions:
                    discrete_action, continuous_actions = actions[agent]

                    # Breakdown of continuous actions
                    continuous_actions[0] = 0.0  # Set steering to zero
                    # continuous_actions[1] = 0.1  # Some other modification
                    # continuous_actions[2] = 0.0  # Another modification
                    actions[agent] = (discrete_action, continuous_actions)

                #print(f"actions: {actions}")

                # Print action structure details
                for agent_id, (discrete_action, continuous_actions) in actions.items():
                    print(f"""
                    Agent ID: {agent_id}
                    Discrete action: {discrete_action}
                    Continuous actions: {continuous_actions}
                    Continuous actions shape: {continuous_actions.shape}
                    Continuous actions dtype: {continuous_actions.dtype}
                    """)

                # Step the environment
                print("About to step environment with actions")
                observations, rewards, terminateds, truncateds, infos = env.step(actions)
                print(f"Step completed. Rewards: {rewards}")

                # Check if the episode is done
                done = terminateds.get("__all__", False) or truncateds.get("__all__", False)

            print(f"Episode {episode + 1} finished")

    except UnityCommunicatorStoppedException:
        print("Unity environment was closed. Terminating gracefully.")
    except Exception as e:
        print(f"An unexpected error occurred: {e}")
    finally:
        print("Closing the environment.")
        env.close()

def main():
    # Set up argument parser
    parser = argparse.ArgumentParser(description="Run a Unity environment simulation.")
    parser.add_argument(
        "--num-episodes",
        type=int, 
        default=5,
        help="Number of episodes to run (default: 10)"
    )
    parser.add_argument(
       "--config-path",
       type=str,
       default=os.path.join("examples", "configs", "config.yaml"),
       help="Environment configuration."
    )
    # Parse command-line arguments
    args = parser.parse_args()

    num_episodes = int(args.num_episodes)
    config_path = str(args.config_path)

    # Load configuration from YAML file
    with open(config_path, "r") as config_file:
        config_data = yaml.safe_load(config_file)

    # Initialize Ray
    ray.init()

    try:
        # Create the Unity environment
        env = replicantdrivesim.make(env_name="replicantdrivesim-v0", config=config_data)

        # Run the episodes
        run_episodes(env, num_episodes)
    except Exception as e:
        print(f"An error occurred while setting up or running the environment: {e}")
    finally:
        # Shutdown Ray
        ray.shutdown()

if __name__ == "__main__":
    main()
