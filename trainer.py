import os
import gymnasium as gym
import numpy as np
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

        # You can access continuous and discrete action sizes like this:
        print(f"Continuous action size: {self.behavior_spec.action_spec.continuous_size}")
        print(f"Discrete action branches: {self.behavior_spec.action_spec.discrete_branches}")


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

    def step(self, action):
        #if isinstance(self.action_space, gym.spaces.Box):
        #    action_tuple = ActionTuple(continuous=np.array([action]))
        #else:
        #    action_tuple = ActionTuple(discrete=np.array([[action]]))
        print("TODO: ", action)

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

        action_tuple = ActionTuple(discrete=discrete_actions, continuous=continuous_actions)

        self.env.set_actions(self.behavior_name, action_tuple)
        self.env.step()

        decision_steps, terminal_steps = self.env.get_steps(self.behavior_name)
        
        done = len(terminal_steps.agent_id) > 0
        reward = terminal_steps.reward[0] if done else decision_steps.reward[0]
        #obs = np.concatenate(decision_steps.obs, axis=0)
        obs = np.array(decision_steps.obs[0], dtype=np.float32)

        return obs, reward, done, False, {}

    def close(self):
        self.env.close()

def env_creator(env_config):
    return UnityToGymWrapper(file_name=env_config["file_name"])

# Initialize Ray
ray.init(ignore_reinit_error=True)

# Define PPO configuration
config = {
    "env": "CustomUnityEnv",
    "env_config": {
        "file_name": "libReplicantDriveSim.app"  # Path to your Unity executable
    },
    "num_workers": 1,
    "framework": "torch",
}

# Register the environment with RLlib
tune.register_env("CustomUnityEnv", env_creator)

# Initialize PPO trainer
trainer = PPO(config=config)

# Training loop
for i in range(10):
    result = trainer.train()
    print(f"Iteration {i}: reward_mean={result['episode_reward_mean']}")

# Close Ray
ray.shutdown()
