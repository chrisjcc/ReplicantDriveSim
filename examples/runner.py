import os
import yaml
import ray
import argparse
from collections import OrderedDict
from mlagents_envs.exception import UnityCommunicatorStoppedException

import torch

from ray.rllib.policy.policy import Policy
from ray.rllib.policy.sample_batch import SampleBatch

import replicantdrivesim


def run_episodes(env, num_episodes):
    """Run a defined number of episodes with the environment."""
    # Print available behavior names
    behavior_names = list(env.behavior_specs.keys())
    print(f"Available behavior names: {behavior_names}")


    # Load the trained DQN policy
    base_directory = "/Users/christiancontrerascampana/ray_results"
    #run = "PPO_2024-06-06_12-52-47/PPO_highway_traffic_e9492_00000_0_2024-06-06_12-52-47"
    #checkpoint = "checkpoint_000000"
    #checkpoint_path = os.path.join(base_directory, run, checkpoint)
    #print(f"CHECKPOINT PATH: {checkpoint_path}")

    # Get the policy object, use the `from_checkpoint` utility of the Policy class:
    #print(Policy.from_checkpoint(checkpoint_path))
    #policy = Policy.from_checkpoint(checkpoint_path)["policy_1"]
    #policy.export_model(f"{checkpoint}_saved_model")

    # Get the TensorFlow model
    #model =  policy.model
    #print(f"{model.base_model.summary()}")

    try:
        for episode in range(num_episodes):
            print(f"Starting episode {episode + 1}")

            observations, _ = env.reset()
            done = False

            while not done:

                # Preprocess observations
                #preprocessed_observations = {}
                #for agent, observation in observations.items():
                #    preprocessed_observations[agent] = torch.tensor(observation, dtype=torch.float32)

                # Flatten the observations dictionary into a single tensor
                #flattened_observations = torch.cat(list(preprocessed_observations.values()), dim=0)

                # Reshape the flattened observations to match the expected input shape
                #expected_input_shape = (2, 8)  # Replace with the expected input shape
                #reshaped_observations = flattened_observations.view(expected_input_shape)

                # Create a SampleBatch from the preprocessed observations
                #input_dict = SampleBatch({
                #    "obs": reshaped_observations,
                #    # Add any other necessary keys here
                #})

                # Compute actions for all agents using compute_actions_from_input_dict
                #action_dict, _, _ = policy.compute_actions_from_input_dict(input_dict)

                actions = env.action_space_sample()

                # Modify the actions for all agents
                for agent in actions:
                    discrete_action, continuous_actions = actions[agent]

                    # Breakdown of continuous actions
                    # continuous_actions[0] = 0.0  # Set steering to zero
                    # continuous_actions[1] = 0.1  # Some other modification
                    # continuous_actions[2] = 0.0  # Another modification
                    actions[agent] = (discrete_action, continuous_actions)

                print(f"actions: {actions}")

                # Step the environment
                observations, rewards, terminateds, truncateds, infos = env.step(actions)

                for agent in env.agents:
                    total_reward = rewards[agent]
                    print(f"Rewards for {agent}: {total_reward}")
                    print(f"Reward components for {agent}: {infos[agent]}")

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
