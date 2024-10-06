import os
import json
import gymnasium as gym
import numpy as np
import ray
import yaml
from environment import CustomUnityMultiAgentEnv
from ray.rllib.algorithms.marwil import MARWILConfig
from ray.rllib.policy.sample_batch import SampleBatch, MultiAgentBatch
from ray.tune import Tuner
from ray.tune.registry import register_env
from unity_env_resource import create_unity_env

# Suppress DeprecationWarnings from output
os.environ["PYTHONWARNINGS"] = "ignore::DeprecationWarning"
os.environ["RAY_AIR_NEW_OUTPUT"] = "0"
os.environ["RAY_AIR_RICH_LAYOUT"] = "0"
gym.logger.set_level(gym.logger.DISABLED)

class CustomJsonReader:
    def __init__(self, filepath):
        with open(filepath, 'r') as f:
            self.episodes = json.load(f)
        self.current_episode = 0

    def next(self):
        if self.current_episode >= len(self.episodes):
            raise StopIteration

        episode_data = self.episodes[self.current_episode]
        self.current_episode += 1

        batch_data = {}
        for agent_id, agent_data in episode_data.items():
            batch_data[agent_id] = SampleBatch({
                SampleBatch.OBS: np.array(agent_data['obs']),
                SampleBatch.ACTIONS: {
                    'discrete': np.array(agent_data['actions']['discrete']),
                    'continuous': np.array(agent_data['actions']['continuous'])
                },
                SampleBatch.REWARDS: np.array(agent_data['rewards']),
                SampleBatch.NEXT_OBS: np.array(agent_data['new_obs']),
                SampleBatch.DONES: np.array(agent_data['dones']),
                #"action_prob": np.ones_like(agent_data['rewards']),  # Placeholder for action probabilities
            })

        #env_steps = sum(len(data[SampleBatch.ACTIONS]['discrete']) for data in batch_data.values())
        env_steps = sum(len(data[SampleBatch.ACTIONS]) for data in batch_data.values())

        return MultiAgentBatch(
            batch_data,
            env_steps,
            _custom_type=SampleBatch.AGENT_STEP
        )

# Load configuration
current_dir = os.path.dirname(os.path.abspath(__file__))
config_path = os.path.join(current_dir, "configs", "config.yaml")
with open(config_path, "r", encoding="utf-8") as config_file:
    config_data = yaml.safe_load(config_file)

# Initialize Ray
ray.init(ignore_reinit_error=True, num_cpus=config_data["ray"]["num_cpus"])

def env_creator(env_config):
    return CustomUnityMultiAgentEnv(env_config)

def policy_mapping_fn(agent_id, episode, worker, **kwargs):
    return "shared_policy"

# Register the environment with RLlib
env_name = "CustomUnityMultiAgentEnv"
register_env(env_name, env_creator)

# Create Unity environment
base_dir = os.path.dirname(current_dir)
unity_executable_path = os.path.join(base_dir, "Builds", "StandaloneOSX", "libReplicantDriveSim.app")
unity_env_handle = create_unity_env(
    file_name=unity_executable_path,
    worker_id=0,
    base_port=config_data["unity_env"]["base_port"],
    no_graphics=config_data["unity_env"]["no_graphics"],
)

# Environment configuration
env_config = {
    "initial_agent_count": config_data["env_config"]["initial_agent_count"],
    "unity_env_handle": unity_env_handle,
    "episode_horizon": config_data["env_config"]["episode_horizon"],
}

# Create an instance of the environment for configuration
env = CustomUnityMultiAgentEnv(config=env_config, unity_env_handle=unity_env_handle)

# MARWIL configuration
config = MARWILConfig()
config = config.environment(env=env_name, env_config=env_config)
config = config.multi_agent(
    policies={
        "shared_policy": (None, env.single_agent_obs_space, env.single_agent_action_space, {})
    },
    policy_mapping_fn=policy_mapping_fn,
)
config = config.training(beta=1.0, lr=1e-4)

# Set up offline data
demo_file = "/Users/christiancontrerascampana/Desktop/project/unity_traffic_simulation/reduce_git_lfs/minor_update/ReplicantDriveSim/multi_agent_demonstrations.json"
config = config.offline_data(input_=demo_file)

# Custom input reader
config = config.evaluation(
    evaluation_config=config.overrides(off_policy_estimation_methods={})
)

config.input_config = {"input_files": [demo_file]}
#config.input = lambda ioctx: CustomJsonReader(ioctx.config["input_files"][0])
config.input = CustomJsonReader

# Set up the tuner
tuner = Tuner(
    "MARWIL",
    param_space=config.to_dict(),
    run_config=ray.air.RunConfig(
        name="MARWIL_Highway_Experiment",
        local_dir="./ray_results",
        checkpoint_config=ray.air.CheckpointConfig(
            num_to_keep=1,
            checkpoint_frequency=1,
            checkpoint_at_end=True,
        ),
    ),
    tune_config=ray.tune.TuneConfig(
        num_samples=1,
        max_concurrent_trials=1,
        metric="episode_reward_mean",
        mode="max",
    ),
)

# Run the tuner
results = tuner.fit()

ray.shutdown()
