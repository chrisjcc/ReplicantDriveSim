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

# Combine data from all agents into a single batch o merge data from all agents into a single policy batch.
# Postprocessing of multi-agent data not implemented.
class CustomJsonReader:
    def __init__(self, filepath):
        print("TODO: CustomJsonReader")
        with open(filepath, 'r') as f:
            self.episodes = json.load(f)
        self.current_episode = 0

    def __iter__(self):
        return self

    def __next__(self):
        if self.current_episode >= len(self.episodes):
            raise StopIteration

        episode_data = self.episodes[self.current_episode]
        self.current_episode += 1

        # Combine data from all agents into a single policy batch
        combined_data = {
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

        for policy_data in episode_data["policy_batches"].values():
            combined_data[SampleBatch.OBS].extend(policy_data['obs'])
            combined_data[SampleBatch.ACTIONS]['discrete'].extend(policy_data['actions']['discrete'])
            combined_data[SampleBatch.ACTIONS]['continuous'].extend(policy_data['actions']['continuous'])
            combined_data[SampleBatch.REWARDS].extend(policy_data['rewards'])
            combined_data[SampleBatch.NEXT_OBS].extend(policy_data['new_obs'])
            combined_data[SampleBatch.DONES].extend(policy_data['dones'])
            combined_data[SampleBatch.ACTION_PROB].extend(policy_data['action_prob'])
            combined_data["t"].extend(policy_data['t'])


        #policy_batches = {}
        #for policy_id, policy_data in episode_data["policy_batches"].items():
        #    policy_batches[policy_id] = SampleBatch({
        #        SampleBatch.OBS: np.array(policy_data['obs']),
        #        SampleBatch.ACTIONS: {
        #            'discrete': np.array(policy_data['actions']['discrete']),
        #            'continuous': np.array(policy_data['actions']['continuous'])
        #        },
        #        SampleBatch.REWARDS: np.array(policy_data['rewards']),
        #        SampleBatch.NEXT_OBS: np.array(policy_data['new_obs']),
        #        SampleBatch.DONES: np.array(policy_data['dones']),
        #        SampleBatch.ACTION_PROB: np.array(policy_data['action_prob']),
        #        "t": np.array(policy_data['t'])
        #    })

        # Convert lists to numpy arrays
        for key in combined_data:
            if key == SampleBatch.ACTIONS:
                combined_data[key]['discrete'] = np.array(combined_data[key]['discrete'])
                combined_data[key]['continuous'] = np.array(combined_data[key]['continuous'])
            else:
                combined_data[key] = np.array(combined_data[key])

        print("TODO: %%%%%%%%%============")
        return SampleBatch(combined_data)

        #return MultiAgentBatch(
        #    policy_batches,
        #    episode_data["count"], # env steps
        #)


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
    #policy_mapping_fn=policy_mapping_fn,
    policy_mapping_fn=lambda agent_id, episode, worker, **kwargs: "shared_policy",
)

config = config.training(
    beta=1.0, 
    lr=1e-4,
    #train_batch_size=4000,  # Adjust this based on your data
    model={"fcnet_hiddens": [256, 256]},  # Adjust the model architecture as needed
)

config = config.experimental(_disable_action_flattening=True)

# Set up offline data
demo_file = "/Users/christiancontrerascampana/Desktop/project/unity_traffic_simulation/reduce_git_lfs/minor_update/ReplicantDriveSim/multi_agent_demonstrations.json"
config = config.offline_data(
    input_=demo_file,
    #input_evaluation=["is", "wis"],
    postprocess_inputs=True, # must be True for MARWIL
    #shuffle_buffer_size=10000,
    actions_in_input_normalized=False, # Default: True
)

# Custom input reader
#config = config.evaluation(
#    evaluation_config=config.overrides(off_policy_estimation_methods={}) # Disable Off-Policy Estimation
#)

# Enable importance sampling (IS) or weighted IS (WIS) as OPE methods
#config = config.evaluation(
#    evaluation_config=config.overrides(
#        off_policy_estimation_methods={
#            #"is": {"type": "is"},  # Importance Sampling
#            #"wis": {"type": "wis"}  # Weighted IS
#            # Disable Off-Policy Estimation
#        },
#        multiagent=None  # Ensure no multi-agent setup
#    )
#)

config = config.evaluation(
    evaluation_config=config.overrides(
        off_policy_estimation_methods={},  # Disable OPE
        #multiagent=None  # Ensure no multi-agent setup
    )
)

#config.input_config = {"input_files": [demo_file]}
#config.input = lambda ioctx: CustomJsonReader(ioctx.config["input_files"][0])
#config.input = CustomJsonReader
#config.input = lambda ioctx: CustomJsonReader(config)

# Update the input configuration to use the built-in JSON reader
config.input_config = {"input_files": [demo_file]}  # Pointing to the JSON file generated by the runner script

# Use the built-in json reader to read offline data for RLlib training
config.input = "json_reader"  # This replaces the need for CustomJsonReader

# Set up the tuner
tuner = Tuner(
    "MARWIL",
    param_space=config.to_dict(),
    run_config=ray.air.RunConfig(
        name="MARWIL_Highway_Experiment",
        local_dir="./ray_results",
        checkpoint_config=ray.air.CheckpointConfig(
            num_to_keep=2,
            checkpoint_frequency=1,
            checkpoint_at_end=True,
        ),
        #stop={"training_iteration": 1000},  # Adjust as needed
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
