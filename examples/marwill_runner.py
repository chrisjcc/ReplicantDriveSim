import os
import yaml
import ray
import argparse
import json
import numpy as np
from environment import CustomUnityMultiAgentEnv
from unity_env_resource import create_unity_env
from ray.rllib.policy.sample_batch import SampleBatch, MultiAgentBatch

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
        "_disable_action_flattening": True,
    }
    
    env = CustomUnityMultiAgentEnv(config=env_config, unity_env_handle=unity_env_handle)
    return env

def run_episodes(env, num_episodes, output_file):
    """Run a defined number of episodes with the environment."""
    all_episodes_data = []
    total_count = 0  # Initialize total step count

    for episode in range(num_episodes):
        print(f"Starting episode {episode + 1}")

        episode_data = {
            "type": "MultiAgentBatch",
            "policy_batches": {
                "shared_policy": {
                    "obs": [],
                    "actions": {
                        "discrete": [],
                        "continuous": []
                    },
                    "rewards": [],
                    "new_obs": [],
                    "dones": [],
                    "t": [],
                    "action_prob": [],
                }
            }
        }

        obs, _ = env.reset()
        done = False
        step = 0

        while not done:
            actions = env.action_space_sample()

            # Modify actions if needed
            for agent in actions:
                discrete_action, continuous_actions = actions[agent]
                actions[agent] = (int(discrete_action), continuous_actions.tolist())

            print(f"Step {step} actions: ", actions)

           # Collect pre-step data
            for agent_id, agent_obs in obs.items():
                episode_data["policy_batches"]["shared_policy"]["obs"].append(agent_obs.tolist())
                discrete_action, continuous_actions = actions[agent_id]
                episode_data["policy_batches"]["shared_policy"]["actions"]["discrete"].append(discrete_action)
                episode_data["policy_batches"]["shared_policy"]["actions"]["continuous"].append(continuous_actions)

            # Step the environment
            new_obs, rewards, terminateds, truncateds, infos = env.step(actions)

            print(f"Step {step} rewards: ", rewards)

            # Collect post-step data
            for agent_id in obs.keys():
                episode_data["policy_batches"]["shared_policy"]["rewards"].append(float(rewards[agent_id]))
                agent_done = terminateds.get(agent_id, False) or truncateds.get(agent_id, False)
                episode_data["policy_batches"]["shared_policy"]["dones"].append(agent_done)
                episode_data["policy_batches"]["shared_policy"]["new_obs"].append(new_obs[agent_id].tolist())
                episode_data["policy_batches"]["shared_policy"]["t"].append(step)
                episode_data["policy_batches"]["shared_policy"]["action_prob"].append(1.0)  # Placeholder for action probabilities

            # Check if the episode is done
            done = terminateds.get("__all__", False) or truncateds.get("__all__", False)
            obs = new_obs
            step += 1

        # Add count of environment steps
        episode_data["count"] = step
        total_count += step  # Add the episode step count to the total count

        all_episodes_data.append(episode_data)
        print(f"Episode {episode + 1} finished")

    # Combine all data from episodes into a single batch
    combined_data = {
        "type": "MultiAgentBatch",
        "policy_batches": {
            "shared_policy": {
                SampleBatch.OBS: [],
                SampleBatch.ACTIONS: {
                    'discrete': [],
                    'continuous': []
                },
                SampleBatch.REWARDS: [],
                SampleBatch.NEXT_OBS: [],
                SampleBatch.DONES: [],
                SampleBatch.ACTION_PROB: [],
                "t": []
            }
        },
        "count": total_count  # Add the total count of steps
    }

    # Aggregate episode data into combined_data
    for episode in all_episodes_data:
        policy_data = episode["policy_batches"]["shared_policy"]
        combined_policy_data = combined_data["policy_batches"]["shared_policy"]
        combined_policy_data[SampleBatch.OBS].extend(policy_data['obs'])
        combined_policy_data[SampleBatch.ACTIONS]['discrete'].extend(policy_data['actions']['discrete'])
        combined_policy_data[SampleBatch.ACTIONS]['continuous'].extend(policy_data['actions']['continuous'])
        combined_policy_data[SampleBatch.REWARDS].extend(policy_data['rewards'])
        combined_policy_data[SampleBatch.NEXT_OBS].extend(policy_data['new_obs'])
        combined_policy_data[SampleBatch.DONES].extend(policy_data['dones'])
        combined_policy_data[SampleBatch.ACTION_PROB].extend(policy_data['action_prob'])
        combined_policy_data["t"].extend(policy_data['t'])

    # Save all_episodes_data to a JSON file
    with open(output_file, "w", encoding="utf-8") as f:
        #json.dump(all_episodes_data, f, cls=NumpyEncoder)
        json.dump(combined_data, f, cls=NumpyEncoder)
    print(f"Data saved to {output_file}")


class NumpyEncoder(json.JSONEncoder):
    def default(self, obj):
        if isinstance(obj, np.ndarray):
            return obj.tolist()
        if isinstance(obj, np.integer):
            return int(obj)
        if isinstance(obj, np.floating):
            return float(obj)
        return json.JSONEncoder.default(self, obj)


def main():
    parser = argparse.ArgumentParser(description="Run a Unity environment simulation.")
    parser.add_argument("--num_episodes", type=int, default=1, help="Number of episodes to run (default: 1)")
    parser.add_argument("--output_file", type=str, default="multi_agent_demonstrations.json", help="Output file to save agent data")
    args = parser.parse_args()

    current_dir = os.path.dirname(os.path.abspath(__file__))
    config_path = os.path.join(current_dir, "configs", "config.yaml")
    config_schema_path = os.path.join(current_dir, "configs", "config_schema.yaml")
    config_data = load_config(config_path, config_schema_path)

    base_dir = os.path.dirname(current_dir)
    unity_executable_path = os.path.join(base_dir, "Builds/StandaloneOSX/libReplicantDriveSim.app")

    ray.init()
    env = create_env(config_data, unity_executable_path)
    run_episodes(env, args.num_episodes, args.output_file)
    env.close()

if __name__ == "__main__":
    main()
