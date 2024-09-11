from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mlagents_envs.side_channel.float_properties_channel import FloatPropertiesChannel
import mlagents_envs
import time

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
time.sleep(5)  # Adjust this delay as needed

# Get the behavior names
behavior_names = list(env.behavior_specs.keys())
print("Behavior names:", behavior_names)

max_attempts = 5
attempt = 0

while not behavior_names and attempt < max_attempts:
    print(f"No behaviors found. Retrying... (Attempt {attempt + 1}/{max_attempts})")
    time.sleep(2)
    env.reset()
    behavior_names = list(env.behavior_specs.keys())
    attempt += 1

if not behavior_names:
    raise ValueError("No behaviors found in the environment after multiple attempts")

# Use the first behavior name (you may need to adjust this based on your specific environment)
behavior_name = behavior_names[0]

num_episodes = 5

# Your training loop
for episode in range(num_episodes):
    print(f"Episode: {episode}")

    try:
        # Reset the environment
        env.reset()
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

        # Your agent's decision-making logic here
        # For now, we'll just use a dummy action (adjust based on your action space)
        action = [0] * env.behavior_specs[behavior_name].action_spec.continuous_size

        # Take a step in the environment
        env.set_actions(behavior_name, action)
        env.step()
        episode_steps += 1

# Close the environment
env.close()
