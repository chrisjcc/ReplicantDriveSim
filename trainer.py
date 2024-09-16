import os
import gymnasium as gym
import numpy as np
<<<<<<< Updated upstream
import ray
from ray import tune
from ray.rllib.algorithms.ppo import PPO
#from ray.rllib.env.external_env import ExternalEnv
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
from mlagents_envs.side_channel.float_properties_channel import FloatPropertiesChannel
from mlagents_envs.base_env import ActionTuple

os.environ['PYTHONWARNINGS'] = 'ignore::DeprecationWarning'

class UnityToGymWrapper(gym.Env):
    def __init__(self, file_name, *args, **kwargs):
        super().__init__()
        self.engine_configuration_channel = EngineConfigurationChannel()
        self.float_properties_channel = FloatPropertiesChannel()

        self.env = UnityEnvironment(file_name=file_name,
                                    side_channels=[self.engine_configuration_channel, self.float_properties_channel])
        self.env.reset()
        
        self.behavior_name = list(self.env.behavior_specs.keys())[0]
        #self.spec = self.env.behavior_specs[self.behavior_name]

        # Access the BehaviorSpec for the behavior
        self.behavior_spec = self.env.behavior_specs[self.behavior_name]

        # Now, self.behavior_spec contains the information you need
        print(f"BehaviorSpec: {self.behavior_spec}")
=======

from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import (
    EngineConfigurationChannel,
)
from mlagents_envs.side_channel.float_properties_channel import FloatPropertiesChannel

import ray
from ray.tune.registry import register_env
from ray.rllib.env.env_context import EnvContext
from ray import tune
from ray.rllib.algorithms.ppo import PPO
from ray.rllib.env.multi_agent_env import MultiAgentEnv
# from ray.rllib.env.external_env import ExternalEnv


@ray.remote
class UnityEnvResource:
    def __init__(self, file_name, worker_id=0, base_port=5004):
        self.channel_id = uuid.UUID("621f0a70-4f87-11ea-a6bf-784f4387d1f7")
        self.engine_configuration_channel = EngineConfigurationChannel()
        self.float_props_channel = FloatPropertiesChannel(self.channel_id)
        
        self.unity_env = UnityEnvironment(
            file_name=file_name,
            worker_id=worker_id,
            base_port=base_port,
            side_channels=[self.engine_configuration_channel, self.float_props_channel],
            seed=42,
        )

    def set_float_property(self, key, value):
        self.float_props_channel.set_property(key, float(value))

    def get_behavior_specs(self):
        return self.unity_env.behavior_specs

    def get_steps(self, behavior_name):
        return self.unity_env.get_steps(behavior_name)

    def set_actions(self, behavior_name, action):
        return self.unity_env.set_actions(behavior_name, action)

    def reset(self):
        return self.unity_env.reset()

    def step(self):
        return self.unity_env.step()

    def close(self):
        if hasattr(self, 'unity_env') and self.unity_env is not None:
            try:
                self.unity_env.close()
            except Exception as e:
                print(f"Error closing Unity environment: {e}")
            finally:
                self.unity_env = None
        else:
            print("Unity environment is already closed or was not properly initialized.")

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.close()


class CustomUnityMultiAgentEnv(MultiAgentEnv):
    def __init__(self, config: EnvContext, *args, **kwargs):
        super().__init__()
        #self.file_name = config["file_name"]
        self.initial_agent_count = config.get("initial_agent_count", 2)
        self.unity_env_handle = config["unity_env_handle"]
        self.current_agent_count = self.initial_agent_count
        #self.channel_id = uuid.UUID("621f0a70-4f87-11ea-a6bf-784f4387d1f7")

        # Initialize Unity environment here
        self.engine_configuration_channel = EngineConfigurationChannel()
        #self.float_props_channel = FloatPropertiesChannel(self.channel_id)

        #self.unity_env.float_props_channel.set_property(
        #    "initialAgentCount", float(self.initial_agent_count)
        #)

        # Set the initial agent count
        ray.get(self.unity_env_handle.set_float_property.remote("initialAgentCount", self.initial_agent_count))

        # Start the environment
        print(f"Initializing with {self.initial_agent_count} agents")
        #self.unity_env.reset()
        #self.unity_env = ray.get(self.unity_env_handle.reset.remote())
        # Reset the environment
        ray.get(self.unity_env_handle.reset.remote())

        # Access the behavior spects
        #self.behavior_name = list(self.unity_env.behavior_specs.keys())[0]
        behavior_specs = ray.get(self.unity_env_handle.get_behavior_specs.remote())
        self.behavior_name = list(behavior_specs.keys())[0]
        #self.behavior_spec = self.unity_env.behavior_specs[self.behavior_name]
        self.behavior_spec = behavior_specs[self.behavior_name]

        # Initialize observation and action spaces
        self.observation_space = None
        self.action_space = None

        self.observation_spaces = {}
        self.action_spaces = {}

        # Get the actual number of agents after environment reset
        #decision_steps, _ = self.unity_env.get_steps(self.behavior_name)
        decision_steps, _ = ray.get(self.unity_env_handle.get_steps.remote(self.behavior_name))
        self.num_agents = len(decision_steps)
        print(f"Initial number of agents: {self.num_agents}")

        # Get observation size
        self.size_of_single_agent_obs = self.behavior_spec.observation_specs[0].shape[0]
>>>>>>> Stashed changes

        # You can access continuous and discrete action sizes like this:
        print(f"Continuous action size: {self.behavior_spec.action_spec.continuous_size}")
        print(f"Discrete action branches: {self.behavior_spec.action_spec.discrete_branches}")

<<<<<<< Updated upstream
=======
        #decision_steps, terminal_steps = self.unity_env.get_steps(
        #    self.behavior_name
        #)  # decision_steps.agent_id

        decision_steps, terminal_steps = ray.get(self.unity_env_handle.get_steps.remote(self.behavior_name))
        self._agent_ids = {f"agent_{i}" for i in range(self.num_agents)}
        # self._agent_ids = {f"agent_{i}" for i in decision_steps.agent_id}
>>>>>>> Stashed changes

        # Assuming you have a variable `num_agents` that specifies the number of agents
        num_agents = 2  # Replace with the actual number of agents

        # Define observation and action spaces
        #obs_shapes = [self.behavior_spec.shape for spec in self.behavior_spec.observation_specs]

        #self.observation_space = gym.spaces.Box(low=-np.inf, high=np.inf, shape=(sum(shape[0] for shape in obs_shapes),)),
        self.observation_space = gym.spaces.Box(low=-np.inf, high=np.inf, shape=(2, 19), dtype=np.float32)

        #if self.spec.action_spec.continuous_size > 0:
        #    self.action_space = gym.spaces.Box(low=-1, high=1, shape=(self.spec.action_spec.continuous_size,))
        #else:
        #    self.action_space = gym.spaces.Discrete(self.spec.action_spec.discrete_branches[0])

        #self.action_space = gym.spaces.Tuple((
        #    gym.spaces.Discrete(self.spec.action_spec.discrete_branches[0]),
        #    gym.spaces.Box(low=-1, high=1, shape=(self.spec.action_spec.continuous_size,), dtype=np.float32)
        #))

        # Generalize for multiple agents
        self.action_space = gym.spaces.Tuple(tuple(
            gym.spaces.Tuple((
                gym.spaces.Discrete(self.behavior_spec.action_spec.discrete_branches[0]),
                gym.spaces.Box(low=-1, high=1, shape=(self.behavior_spec.action_spec.continuous_size,), dtype=np.float32)
            )) for _ in range(num_agents)
        ))

        #self.observation_space = self.observation_spaces
        #self.action_space = self.action_spaces

    def __str__(self):
        return f"<{type(self).__name__} with custom behavior spec>"

    def reset(self, *, seed=None, options=None):
        # Handle seed if provided
        if seed is not None:
            np.random.seed(seed)

        self.env.reset()
        decision_steps, _ = self.env.get_steps(self.behavior_name)
        obs = np.array(decision_steps.obs[0], dtype=np.float32)
        #return np.concatenate(decision_steps.obs, axis=0), {} # Return observation and an empty info dict
        return obs, {}

<<<<<<< Updated upstream
    def step(self, action):
        #if isinstance(self.action_space, gym.spaces.Box):
        #    action_tuple = ActionTuple(continuous=np.array([action]))
        #else:
        #    action_tuple = ActionTuple(discrete=np.array([[action]]))
        print("TODO: ", action)

=======
            if new_agent_count != self.current_agent_count:
                self.current_agent_count = new_agent_count
                #self.float_props_channel.set_property(
                #    "initialAgentCount", float(new_agent_count)
                #)
                ray.get(self.unity_env_handle.set_float_property.remote("initialAgentCount", new_agent_count))
                print(f"Setting new agent count to: {new_agent_count}")

        # print("Resetting Unity environment...")
        #self.unity_env.reset()  # Reset the environment only once (ideally)
        #self.unity_env.reset()
        # Reset the environment
        ray.get(self.unity_env_handle.reset.remote())

        # Get decision steps after the reset
        #decision_steps, _ = self.unity_env.get_steps(self.behavior_name)
        decision_steps, _ = ray.get(self.unity_env_handle.get_steps.remote(self.behavior_name))
        self.num_agents = len(decision_steps)
        # print(f"Number of agents after reset: {self.num_agents}")

        # Update num_agents and observation_space
        self._update_spaces()
        # print(f"Updated action space: {self.action_space}")

        obs_dict = {}
        for i, agent_id in enumerate(decision_steps.agent_id):
            obs = decision_steps[agent_id].obs[0].astype(np.float32)
            # print(f"Agent id: {i}, Unity agent_id: {agent_id}")
            agent_key = f"agent_{i}"
            obs_dict[agent_key] = obs

            # Debug print
            # print(f"Reset - {agent_key} observation shape: {obs.shape}")
            # print(f"Reset - Agent {i} observation shape: {obs.shape}")
            # print(f"Reset - {agent_key} observation space shape: {self.observation_space[agent_key].shape}")
            # print(f"Reset - Agent {i} observation space shape: {self.observation_space[f'agent_{i}'].shape}")

        # Checking if the observations are within the bounds of the observation space
        for agent_id, obs in obs_dict.items():
            # print(f"observation space are within bounds for agent: {agent_id}")
            if not self.observation_space[agent_id].contains(obs):
                print(f"Warning: Observation for {agent_key} is out of bounds:")
                print(f"Observation: {obs}")
                print(f"Observation space: {self.observation_space[agent_id]}")

        # Returning the observations and an empty info dict
        return obs_dict, {}

    def step(
        self, action_dict: Dict[Any, Any]
    ) -> Tuple[
        Dict[Any, Any], Dict[Any, Any], Dict[Any, Any], Dict[Any, Any], Dict[Any, Any]
    ]:
>>>>>>> Stashed changes
        # Initialize lists to store discrete and continuous actions
        discrete_actions = []
        continuous_actions = []

        # Loop over the actions to separate discrete and continuous actions
        for agent_action in action:
            discrete, continuous = agent_action
            discrete_actions.append([discrete])  # Adding as a list to match the shape [[0], [4]]
            continuous_actions.append(continuous)

        # Convert lists to numpy arrays
        discrete_actions = np.array(discrete_actions)
        continuous_actions = np.array(continuous_actions)

        print(f"Discrete action: {discrete_actions}")
        print(f"Continuous action: {continuous_actions}")

<<<<<<< Updated upstream
        action_tuple = ActionTuple(discrete=discrete_actions, continuous=continuous_actions)

        self.env.set_actions(self.behavior_name, action_tuple)
        self.env.step()
=======
        #self.unity_env.set_actions(self.behavior_name, action_tuple)
        ray.get(self.unity_env_handle.set_actions.remote(self.behavior_name, action_tuple))

        #self.unity_env.step()
        ray.get(self.unity_env_handle.step.remote())

        #decision_steps, terminal_steps = self.unity_env.get_steps(self.behavior_name)
        decision_steps, terminal_steps = ray.get(self.unity_env_handle.get_steps.remote(self.behavior_name))
>>>>>>> Stashed changes

        decision_steps, terminal_steps = self.env.get_steps(self.behavior_name)
        
        done = len(terminal_steps.agent_id) > 0
        reward = terminal_steps.reward[0] if done else decision_steps.reward[0]
        #obs = np.concatenate(decision_steps.obs, axis=0)
        obs = np.array(decision_steps.obs[0], dtype=np.float32)

<<<<<<< Updated upstream
        return obs, reward, done, False, {}

    def close(self):
        self.env.close()

def env_creator(env_config):
    return UnityToGymWrapper(file_name=env_config["file_name"])
=======
        for i, agent_id in enumerate(terminal_steps.agent_id):
            agent_key = f"agent_{agent_id}"
            obs[agent_key] = terminal_steps[agent_id].obs[0].astype(np.float32)
            rewards[agent_key] = terminal_steps[agent_id].reward
            terminateds[agent_key] = True
            truncateds[agent_key] = terminal_steps[agent_id].interrupted
            infos[agent_key] = {}

        # Check if all agents are terminated
        terminateds["__all__"] = all(terminateds.values())

        # Check if all agents are truncated
        truncateds["__all__"] = any(truncateds.values())

        return obs, rewards, terminateds, truncateds, infos

    def close(self):
        ray.get(self.unity_env_handle.close.remote())


def env_creator(env_config):
    return CustomUnityMultiAgentEnv(env_config)

# Create the shared Unity environment resource
unity_env_handle = UnityEnvResource.remote("libReplicantDriveSim.app")
>>>>>>> Stashed changes

# Initialize Ray
ray.init(ignore_reinit_error=True)

# Define PPO configuration
config = {
    "env": "CustomUnityEnv",
    "env_config": {
<<<<<<< Updated upstream
        "file_name": "libReplicantDriveSim.app"  # Path to your Unity executable
=======
        #"file_name": "libReplicantDriveSim.app",  # Path to your Unity executable
        "unity_env_handle": unity_env_handle,
        "initial_agent_count": 5,  # Set your desired initial agent count here
>>>>>>> Stashed changes
    },
    "num_workers": 1,
    "framework": "torch",
}

# Register the environment with RLlib
tune.register_env("CustomUnityEnv", env_creator)

# Initialize PPO trainer
trainer = PPO(config=config)

# Training loop
<<<<<<< Updated upstream
for i in range(10):
    result = trainer.train()
    print(f"Iteration {i}: reward_mean={result['episode_reward_mean']}")

=======
for i in range(3):
    # Generate a new agent count
    new_agent_count = np.random.randint(
        1, 5
    )  # Choose a random number of agents between 1 and 10
    print(f"Episode {i} number of agents: ", new_agent_count)

    # Update the env_config with the new agent count
    trainer.config["env_config"]["initial_agent_count"] = new_agent_count

    # Update all worker environments
    def update_env(env):
        if isinstance(env, CustomUnityMultiAgentEnv):  # UnityToGymWrapper
            env.reset(options={"new_agent_count": new_agent_count})

    trainer.workers.foreach_worker(lambda worker: worker.foreach_env(update_env))

   # Train for one iteration
    result = trainer.train()
    print(f"Iteration {i}: reward_mean={result['episode_reward_mean']}")

# Make sure to close the Unity environment at the end
ray.get(unity_env_handle.close.remote())

>>>>>>> Stashed changes
# Close Ray
ray.shutdown()
