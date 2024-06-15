import os
import gymnasium as gym
import numpy as np
import pygame

from collections import OrderedDict

import torch

from ray.rllib.policy.policy import Policy
from ray.rllib.policy.sample_batch import SampleBatch

from simulacrum import HighwayEnv
#from highway_simulation import HighwayEnv

# Constants for the Pygame simulation
SCREEN_WIDTH = 600
SCREEN_HEIGHT = 400
LANE_WIDTH = 100
VEHICLE_WIDTH = 50
VEHICLE_HEIGHT = 20


# Enable all reward components
configs = {
    "progress": True,
    "collision": True,
    "safety_distance": True,
    "max_episode_steps": 1000,
    "num_agents": 2, 
    "render_mode": True,
}   

# Create the environment
env = HighwayEnv(configs=configs)  # Set render_mode to True to enable rendering

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

num_episodes = 10  # Define the number of episodes

for episode in range(num_episodes):
    print(f"Starting episode {episode + 1}")

    observations, _ = env.reset()
    done = False

    while not done:
        env.render()

        """
        # Preprocess observations
        preprocessed_observations = {}
        for agent, observation in observations.items():
            preprocessed_observations[agent] = torch.tensor(observation, dtype=torch.float32)

        # Flatten the observations dictionary into a single tensor
        flattened_observations = torch.cat(list(preprocessed_observations.values()), dim=0)

        # Reshape the flattened observations to match the expected input shape
        expected_input_shape = (2, 8)  # Replace with the expected input shape
        reshaped_observations = flattened_observations.view(expected_input_shape)

        # Create a SampleBatch from the preprocessed observations
        input_dict = SampleBatch({
            "obs": reshaped_observations,
            # Add any other necessary keys here
        })

        # Compute actions for all agents using compute_actions_from_input_dict
        action_dict, _, _ = policy.compute_actions_from_input_dict(input_dict)

        actions = {
            agent: OrderedDict([
                ('continuous', action_dict['continuous'][i]),
                ('discrete', action_dict['discrete'][i])
            ])
            for i, agent in enumerate(env.agents)
        }
        """

        # Take random actions for each agent
        actions = {agent: env.action_space.sample() for agent in env.agents}

        # Step the environment
        observations, rewards, terminateds, truncateds, infos = env.step(actions)

        for agent in env.agents:
            total_reward = rewards[agent]
            print(f"Rewards for {agent}: {total_reward}")
            print(f"Reward components for {agent}: {infos[agent]}")

        done = terminateds.get("__all__", False) or truncateds.get("__all__", False)

    print(f"Episode {episode + 1} finished")

# Close the environment
env.close()
