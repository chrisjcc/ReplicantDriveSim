import time
import uuid

import gymnasium as gym
import mlagents
import numpy as np
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import (
    EngineConfigurationChannel,
)
from mlagents_envs.side_channel.float_properties_channel import FloatPropertiesChannel

np.random.seed(42)

# Set up the Unity environment with the desired executable
env_path = "libReplicantDriveSim.app"  # Replace with your Unity environment path
engine_configuration_channel = EngineConfigurationChannel()

channel_id = uuid.UUID("621f0a70-4f87-11ea-a6bf-784f4387d1f7")
float_props_channel = FloatPropertiesChannel(channel_id)

env = UnityEnvironment(
    file_name=env_path,
    side_channels=[
        engine_configuration_channel,
        float_props_channel,
    ],
)

# Start the environment
env.reset()

# Get the behavior name from the environment (usually there's only one behavior)
behavior_name = list(env.behavior_specs.keys())[0]
spec = env.behavior_specs[behavior_name]
print(f"BehaviorSpec: {spec}")

# Define the single agent action space
single_agent_action_space = gym.spaces.Tuple(
    (
        gym.spaces.Discrete(spec.action_spec.discrete_branches[0]),
        gym.spaces.Box(
            low=np.array([-0.610865, 0.0, -8.0]),
            high=np.array([0.610865, 4.5, 0.0]),
            shape=(spec.action_spec.continuous_size,),
            dtype=np.float32,
        ),
    )
)

# Training loop
for episode in range(2):
    # Generate a new agent count
    new_agent_count = np.random.randint(
        1, 10
    )  # Choose a random number of agents between 1 and 10
    print(f"Episode {episode}, number of agents: {new_agent_count}")

    # Set the new initial agent count property
    float_props_channel.set_property("initialAgentCount", float(new_agent_count))

    # Reset the environment to apply the new agent count
    env.reset()
    env.reset()

    # Get the initial state
    decision_steps, terminal_steps = env.get_steps(behavior_name)

    # After the reset, get the updated number of agents
    num_agents = len(decision_steps)
    print(f"Number of agents after the second reset: {num_agents}")

    if num_agents != new_agent_count:
        print(f"\033[93mWARNING\033[0m: Mismatch between set agent count ({new_agent_count}) and actual agent count ({num_agents})")


    # Episode loop
    episode_rewards = {f'agent_{i}': 0 for i in decision_steps.agent_id}
    print(f"List of agents: {episode_rewards}")

    obs = np.array(decision_steps.obs[0], dtype=np.float32)
    print(f"Observation shape: {obs.shape}")

    episode_steps = 0
    episode_max_steps = 100  # Adjust as needed

    while episode_steps < episode_max_steps:
        # Sample random actions for each agent
        discrete_actions = []
        continuous_actions = []

        for agent_id in decision_steps.agent_id:
            discrete, continuous = single_agent_action_space.sample()
            discrete_actions.append([discrete])
            continuous_actions.append(continuous)

        # Convert to numpy arrays
        discrete_actions = np.array(discrete_actions, dtype=np.int32)
        continuous_actions = np.array(continuous_actions, dtype=np.float32)

        # Create ActionTuple
        action_tuple = ActionTuple(
            discrete=discrete_actions, continuous=continuous_actions
        )

        # Set actions in the environment
        env.set_actions(behavior_name, action_tuple)

        # Advance the simulation (first step with both actions)
        env.step()

        # Execute the continuous actions for the next 25 frames (without changing discrete actions)
        for _ in range(25):
            # Only update the continuous actions (discrete and continous action remains unchanged)
            env.set_actions(behavior_name, action_tuple)
            env.step()

        # Get the new state, rewards, and done flags
        decision_steps, terminal_steps = env.get_steps(behavior_name)

        # Update episode rewards (decision_steps.agent_id)
        for agent_id, reward in zip(decision_steps.agent_id, decision_steps.reward):
            episode_rewards[f"agent_{agent_id}"] += reward

        # Check for completed agents
        for agent_id in terminal_steps.agent_id:
            print(f"Agent {agent_id} finished with reward: {episode_rewards[f'agent_{agent_id}']}")

        episode_steps += 25  # Count 25 frames for each decision step

        # If all agents are done, end the episode
        if len(terminal_steps) == new_agent_count:
            break

    print(f"Episode {episode} completed. Average reward: {np.mean(list(episode_rewards.values()))}")

# Close the environment
env.close()
