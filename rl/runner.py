import os
import yaml
import ray
import argparse
import json
import numpy as np
from environment import CustomUnityMultiAgentEnv
from unity_env_resource import create_unity_env
from ray.rllib.policy.sample_batch import SampleBatch

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

def run_episodes(env, num_episodes, output_file):
    """Run a defined number of episodes with the environment."""
    all_episodes_data = []

    for episode in range(num_episodes):
        print(f"Starting episode {episode + 1}")

        episode_data = {}
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
                if agent_id not in episode_data:
                    episode_data[str(agent_id)] = {
                        SampleBatch.OBS: [],
                        SampleBatch.ACTIONS: {
                            "discrete": [],
                            "continuous": []
                        },
                        SampleBatch.REWARDS: [],
                        SampleBatch.NEXT_OBS: [],
                        SampleBatch.DONES: [],
                        "t": [],
                        "action_prob": [],  # Placeholder for action probabilities
                    }
                episode_data[agent_id][SampleBatch.OBS].append(agent_obs.tolist())
                discrete_action, continuous_actions = actions[agent_id]
                episode_data[agent_id][SampleBatch.ACTIONS]["discrete"].append(discrete_action)
                episode_data[agent_id][SampleBatch.ACTIONS]["continuous"].append(continuous_actions)

            # Step the environment
            new_obs, rewards, terminateds, truncateds, infos = env.step(actions)

            print(f"Step {step} rewards: ", rewards)

            # Collect post-step data
            for agent_id in obs.keys():
                episode_data[agent_id][SampleBatch.REWARDS].append(float(rewards[agent_id]))
                agent_done = terminateds.get(agent_id, False) or truncateds.get(agent_id, False)
                episode_data[agent_id][SampleBatch.DONES].append(agent_done)
                episode_data[agent_id][SampleBatch.NEXT_OBS].append(new_obs[agent_id].tolist())
                episode_data[agent_id]["t"].append(step)
                episode_data[agent_id]["action_prob"].append(1.0)

            # Check if the episode is done
            done = terminateds.get("__all__", False) or truncateds.get("__all__", False)
            obs = new_obs
            step += 1

        # Convert lists to numpy arrays and add to all_episodes_data
        for agent_id, agent_data in episode_data.items():
            for key in agent_data:
                if key == SampleBatch.ACTIONS:
                    agent_data[key]["discrete"] = np.array(agent_data[key]["discrete"])
                    agent_data[key]["continuous"] = np.array(agent_data[key]["continuous"])
                else:
                    agent_data[key] = np.array(agent_data[key])
            agent_data["type"] = "MultiAgentBatch"  # Add the 'type' field
            agent_data["policy_id"] = "shared_policy"  # Add the 'policy_id' field


        all_episodes_data.append(episode_data)
        print(f"Episode {episode + 1} finished")

    # Save all_episodes_data to a JSON file
    with open(output_file, "w", encoding="utf-8") as f:
        json.dump(all_episodes_data, f, cls=NumpyEncoder)
    print(f"Data saved to {output_file}")


class NumpyEncoder(json.JSONEncoder):
    def default(self, obj):
        if isinstance(obj, np.ndarray):
            return obj.tolist()
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
