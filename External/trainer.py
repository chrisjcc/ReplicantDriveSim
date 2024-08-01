import gymnasium as gym
import numpy as np
import pygame

import ray
from ray import tune
from ray.tune.registry import register_env
from ray.tune.logger import pretty_print
from ray.rllib.algorithms.ppo import PPOConfig
from ray.rllib.algorithms.callbacks import DefaultCallbacks

from simulacrum import HighwayEnv

# Constants for the Pygame simulation
SCREEN_WIDTH = 600
SCREEN_HEIGHT = 400
LANE_WIDTH = 100
VEHICLE_WIDTH = 50
VEHICLE_HEIGHT = 20


class RenderCallback(DefaultCallbacks):
    def on_episode_step(self, *, worker, base_env, policies, episode, env_index, **kwargs):
        # Access the underlying environment from base_env
        env = base_env.get_sub_environments()[0]

        if hasattr(env, "render") and env.render_mode:
            env.render()


# Create the environment instance
def create_env(configs=None):
    """
    Create an instance of the HighwayEnv environment.

    Parameters:
    - configs (dict): Configuration dictionary with possible keys 'num_agents' and 'render_mode'.

    Returns:
    - env: An instance of HighwayEnv.
    """
    return HighwayEnv(configs=configs)

# Register the custom environment
register_env("highway_traffic", create_env)

# Define the policy mapping function
def policy_mapping_fn(agent_id, episode, worker, **kwargs):
    # Example: Use policy_1 for agent_0, and policy_2 for others
    if agent_id.startswith("agent_0"):
        return "policy_1"
    else:
        return "policy_2"

# Configure parameters
configs = {
    "progress": True,
    "collision": True,
    "safety_distance": True,
    "max_episode_steps": 1000,
    "num_agents": 2,
    "render_mode": "human"
}

# Define the configuration
CONFIG = {
    "env": "highway_traffic",
    "framework": "torch",
    "num_gpus": 0,
    "num_workers": 2,
    "rollout_fragment_length": 200,  # Adjust this value to match the high-level action update frequency
    "lr": 1e-3,
    "train_batch_size": 4000,
    "sgd_minibatch_size": 128,
    "num_sgd_iter": 10,
    "multiagent": {
        "policies": {
            "policy_1": (
                None, # This represents the policy class. By passing None, RLlib will use the default policy class for the specified algorithm (in this case, PPO).
                create_env(configs=configs).observation_space, # This is the observation space of the environment. It specifies the shape and data type of the observations that the agent will receive from the environment.
                create_env(configs=configs).action_space, # This is the action space of the environment. It specifies the shape and data type of the actions that the agent can take in the environment.
                {} # This represents the policy configuration dictionary. By passing None, you are not providing any additional configuration for the policy.
            ),
            "policy_2": (
                None,
                create_env(configs=configs).observation_space,
                create_env(configs=configs).action_space,
                {}
            ),

        },
        "policy_mapping_fn": lambda agent_id, episode, worker, **kwargs: "policy_1", # policy_mapping_fn,
    },
    "callbacks": RenderCallback,  # Include the callback class here
    "_disable_preprocessor_api": True,
}

# Update stop criteria to include correct path for episode reward mean
stop_criteria = {
    "training_iteration": 50,
    "episode_reward_mean": 100,
    "episodes_total": 1000  # Stop after the specified number of episodes
}

# Start the training process
ray.init(ignore_reinit_error=True)

results=tune.run(
    "PPO",
    config=CONFIG,
    checkpoint_freq=5,  # Set checkpoint frequency here
    num_samples=1,      # Number of times to repeat the experiment
    max_failures=1,     # Maximum number of failures before stopping the experiment
    verbose=1,          # Verbosity level for logging
    stop={"episode_reward_mean": 200}
)

# Print the results dictionary of the training to inspect the structure
print("Training results: ", results)
