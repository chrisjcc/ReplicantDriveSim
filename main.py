from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mlagents_envs.side_channel.float_properties_channel import FloatPropertiesChannel

import  mlagents_envs

mlagents_envs.env_utils.DEFAULT_EDITOR_PORT_TIMEOUT = 60  # Increase to 60 seconds


# Create channels
engine_configuration_channel = EngineConfigurationChannel()
print(engine_configuration_channel)

float_properties_channel = FloatPropertiesChannel()
print(float_properties_channel)

# Initialize the environment
env = UnityEnvironment(file_name="libReplicantDriveSim.app", 
                       side_channels=[engine_configuration_channel, float_properties_channel])

behavior_names = env.behavior_specs.keys()
print("behavior_names: ", behavior_names)

num_episodes = 5


# Your training loop
for episode in range(num_episodes):
    print(f"{episode}")

    try:
        # Start interacting with the environment.
        env.reset()
    except mlagents_envs.exception.UnityCommunicatorStoppedException as e:
        print(f"Unity communicator stopped: {e}")
        print(f"Last known communicator status: {env._communicator.worker_id}")

    done = False
    while not done:
        # Get the current state
        decision_steps, terminal_steps = env.get_steps(behavior_names)
        
        # Your agent's decision-making logic here
        #action = ...

        # Take a step in the environment
        env.step()

# Close the environment
env.close()

# Close the envi
