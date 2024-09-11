from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mlagents_envs.side_channel.float_properties_channel import FloatPropertiesChannel
import mlagents_envs
from mlagents_envs.base_env import ActionTuple
import time
import numpy as np

# Increase timeout to 60 seconds
mlagents_envs.env_utils.DEFAULT_EDITOR_PORT_TIMEOUT = 60

# Create channels
engine_configuration_channel = EngineConfigurationChannel()
float_properties_channel = FloatPropertiesChannel()

# Initialize the environment
env = UnityEnvironment(file_name="libReplicantDriveSim.app",
                       side_channels=[engine_configuration_channel, float_properties_channel])

# Wait for agents to spawn
print("Waiting for agents to spawn...")
time.sleep(1)  # Adjust this delay as needed

num_episodes = 5

# Your training loop
for episode in range(num_episodes):
    print(f"Episode: {episode}")

    try:
        # Reset the environment
        env.reset()

        # Get the behavior names
        behavior_names = list(env.behavior_specs.keys())
        print("Behavior names:", behavior_names)

        behavior_name = behavior_names[0]
        print("Using behavior name:", behavior_name)

        # Get the action spec
        action_spec = env.behavior_specs[behavior_name].action_spec

    except mlagents_envs.exception.UnityCommunicatorStoppedException as e:
        print(f"Unity communicator stopped: {e}")
        print(f"Last known communicator status: {env._communicator.worker_id}")
        break

    episode_steps = 0

    while True:
        # Get the current state
        decision_steps, terminal_steps = env.get_steps(behavior_name)
        
        if len(terminal_steps) > 0:
            print(f"Episode finished after {episode_steps} steps")
            break

        num_agents = len(decision_steps)

        # Print rewards for current decision steps
        rewards = decision_steps.reward
        print(f"Rewards for this step: {rewards}")

        # Create the discrete action
        discrete_action = np.random.randint(0, action_spec.discrete_branches[0], size=(num_agents, 1), dtype=np.int32)
        print(f"Discrete action: {discrete_action}")

        # Create the continuous action
        continuous_action = np.random.uniform(-1, 1, size=(num_agents, action_spec.continuous_size))
        print(f"Continuous action: {continuous_action}")

        # Combine them into an ActionTuple
        action = ActionTuple(continuous=continuous_action, discrete=discrete_action)

        # Take a step in the environment
        env.set_actions(behavior_name, action)
        env.step()
        episode_steps += 1

# Close the environment
env.close()
