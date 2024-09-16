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

# Set up side channels
# Note : A side channel will only send/receive messages when env.step or env.reset() is called.
engine_configuration_channel = EngineConfigurationChannel() # Can modify the time-scale, resolution, and graphics quality of the environment. 
#engine_configuration_channel.set_configuration_parameters(target_frame_rate=25, time_scale = 2.0)
channel_id = uuid.UUID("621f0a70-4f87-11ea-a6bf-784f4387d1f7")
float_props_channel = FloatPropertiesChannel(channel_id)
#env_params_channel = EnvironmentParametersChannel()
#channel.set_float_parameter("initialAgentCount", 3.0)

# This is a non-blocking call that only loads the environment.
unity_env = UnityEnvironment(
    file_name=env_path,
    side_channels=[
        engine_configuration_channel,
        float_props_channel,
    ],
    seed=42,
)

# Start interacting with the environment.
unity_env.reset()

# Get the behavior names
behavior_names = list(unity_env.behavior_specs.keys())

# Get the behavior name from the environment (usually there's only one behavior)
behavior_name = behavior_names[0]
spec = unity_env.behavior_specs[behavior_name]
print(f"BehaviorSpec: {spec}")

# Extract observation shapes
observation_shapes = [obs_spec.shape for obs_spec in spec.observation_specs]

# Extract action spec
action_spec = spec.action_spec

# Print the information
print(f"Behavior name: {behavior_name}")
print(f"Observation shapes: {observation_shapes}")
print(f"Continuous action size: {action_spec.continuous_size}")
print(f"Discrete action branches: {action_spec.discrete_branches}")

exit()

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
    unity_env.reset()
    unity_env.reset()

    # Get the initial state
    decision_steps, terminal_steps = unity_env.get_steps(behavior_name)

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
        unity_env.set_actions(behavior_name, action_tuple)

        # Advance the simulation (first step with both actions)
        # env.step() Sends a signal to step the environment. Returns None. 
        # Note that a "step" for Python does not correspond to either Unity `Update` nor `FixedUpdate`.
        # When step() or reset() is called, the Unity simulation will move forward until an Agent in the simulation needs a input from Python to act.
        unity_env.step()

        # Execute the continuous actions for the next 25 frames (without changing discrete actions)
        for _ in range(25):
            # Only update the continuous actions (discrete and continous action remains unchanged)
            unity_env.set_actions(behavior_name, action_tuple)
            unity_env.step()

        # Get the new state, rewards, and done flags
        decision_steps, terminal_steps = unity_env.get_steps(behavior_name)

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
unity_env.close()
