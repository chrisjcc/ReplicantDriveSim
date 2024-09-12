import os

os.environ["PYTHONWARNINGS"] = "ignore::DeprecationWarning"

import uuid

import gymnasium as gym
import numpy as np
import ray
from mlagents_envs.base_env import ActionTuple

# from ray.rllib.env.external_env import ExternalEnv
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import (
    EngineConfigurationChannel,
)
from mlagents_envs.side_channel.float_properties_channel import FloatPropertiesChannel
from ray import tune
from ray.rllib.algorithms.ppo import PPO

channel_id = uuid.UUID("621f0a70-4f87-11ea-a6bf-784f4387d1f7")


class UnityToGymWrapper(gym.Env):
    def __init__(self, file_name, initial_agent_count=2, *args, **kwargs):
        super().__init__()
        self.engine_configuration_channel = EngineConfigurationChannel()
        self.float_properties_channel = FloatPropertiesChannel(channel_id)

        self.env = UnityEnvironment(
            file_name=file_name,
            side_channels=[
                self.engine_configuration_channel,
                self.float_properties_channel,
            ],
        )

        self.initial_agent_count = initial_agent_count
        self.current_agent_count = initial_agent_count

        # Set the initialAgentCount before resetting the environment
        self.float_properties_channel.set_property(
            "initialAgentCount", float(initial_agent_count)
        )

        self.env.reset()

        # Access the BehaviorSpec for the behavior
        self.behavior_name = list(self.env.behavior_specs.keys())[0]
        self.behavior_spec = self.env.behavior_specs[self.behavior_name]

        # Now, self.behavior_spec contains the information you need
        print(f"BehaviorSpec: {self.behavior_spec}")

        # Get the actual number of agents after environment reset
        decision_steps, _ = self.env.get_steps(self.behavior_name)
        self.num_agents = len(decision_steps)

        # Get observation size
        self.size_of_single_agent_obs = self.behavior_spec.observation_specs[0].shape[0]

        # You can access continuous and discrete action sizes like this:
        print(
            f"Continuous action size: {self.behavior_spec.action_spec.continuous_size}"
        )
        print(
            f"Discrete action branches: {self.behavior_spec.action_spec.discrete_branches}"
        )

        # Define observation and action spaces
        self.observation_space = gym.spaces.Box(
            low=-np.inf,
            high=np.inf,
            shape=(self.num_agents, self.size_of_single_agent_obs),
            dtype=np.float32,
        )

        # Generalize for multiple agents
        self.action_space = gym.spaces.Tuple(
            tuple(
                gym.spaces.Tuple(
                    (
                        gym.spaces.Discrete(
                            self.behavior_spec.action_spec.discrete_branches[0]
                        ),
                        gym.spaces.Box(
                            low=np.array([-0.610865, 0.0, -8.0]),
                            high=np.array([0.610865, 4.5, 0.0]),
                            shape=(self.behavior_spec.action_spec.continuous_size,),
                            dtype=np.float32,
                        ),
                    )
                )
                for _ in range(self.num_agents)
            )
        )

    def __str__(self):
        return f"<{type(self).__name__} with custom behavior spec>"

    def reset(self, *, seed=None, options=None):
        # Handle seed if provided
        if seed is not None:
            np.random.seed(seed)

        # Check if options contain a new agent count
        if options and "new_agent_count" in options:
            new_agent_count = options["new_agent_count"]
            if new_agent_count != self.current_agent_count:
                self.current_agent_count = new_agent_count
                self.float_properties_channel.set_property(
                    "initialAgentCount", float(new_agent_count)
                )
                print(f"Setting new agent count to: {new_agent_count}")

        self.env.reset()

        decision_steps, _ = self.env.get_steps(self.behavior_name)
        obs = np.array(decision_steps.obs[0], dtype=np.float32)

        # Update num_agents and observation_space
        self.num_agents = len(decision_steps)
        self.observation_space = gym.spaces.Box(
            low=-np.inf,
            high=np.inf,
            shape=(self.num_agents, self.size_of_single_agent_obs),
            dtype=np.float32,
        )
        self.action_space = gym.spaces.Tuple(
            tuple(
                gym.spaces.Tuple(
                    (
                        gym.spaces.Discrete(
                            self.behavior_spec.action_spec.discrete_branches[0]
                        ),
                        gym.spaces.Box(
                            low=np.array([-0.610865, 0.0, -8.0]),
                            high=np.array([0.610865, 4.5, 0.0]),
                            shape=(self.behavior_spec.action_spec.continuous_size,),
                            dtype=np.float32,
                        ),
                    )
                )
                for _ in range(self.num_agents)
            )
        )

        return obs, {}  # Return observation and an empty info dict

    def step(self, action):
        # Initialize lists to store discrete and continuous actions
        discrete_actions = []
        continuous_actions = []

        # Loop over the actions to separate discrete and continuous actions
        for agent_action in action:
            discrete, continuous = agent_action
            discrete_actions.append(
                [discrete]
            )  # Adding as a list to match the shape [[0], [4]]
            continuous_actions.append(continuous)

        # Convert lists to numpy arrays
        discrete_actions = np.array(discrete_actions)
        continuous_actions = np.array(continuous_actions)

        print(f"Discrete action: {discrete_actions}")
        print(f"Continuous action: {continuous_actions}")

        action_tuple = ActionTuple(
            discrete=discrete_actions, continuous=continuous_actions
        )

        self.env.set_actions(self.behavior_name, action_tuple)
        self.env.step()

        decision_steps, terminal_steps = self.env.get_steps(self.behavior_name)

        done = len(terminal_steps.agent_id) > 0
        reward = terminal_steps.reward[0] if done else decision_steps.reward[0]
        obs = np.array(decision_steps.obs[0], dtype=np.float32)

        return obs, reward, done, False, {}

    def close(self):
        self.env.close()


def env_creator(env_config):
    return UnityToGymWrapper(
        file_name=env_config["file_name"],
        initial_agent_count=env_config.get("initial_agent_count", 2),
    )


# Initialize Ray
ray.init(ignore_reinit_error=True)

# Define PPO configuration
config = {
    "env": "CustomUnityEnv",
    "env_config": {
        "file_name": "libReplicantDriveSim.app",  # Path to your Unity executable
        "initial_agent_count": 5,  # Set your desired initial agent count here
    },
    "num_workers": 1,
    "framework": "torch",
    "train_batch_size": 4000,
    "sgd_minibatch_size": 128,
    "num_sgd_iter": 30,
    "lr": 3e-4,
    "gamma": 0.99,
    "lambda": 0.95,
    "clip_param": 0.2,
    "num_envs_per_worker": 1,
    "rollout_fragment_length": 200,
}

# Register the environment with RLlib
tune.register_env("CustomUnityEnv", env_creator)

# Initialize PPO trainer
trainer = PPO(config=config)

# Training loop
for i in range(10):
    # Generate a new agent count
    new_agent_count = np.random.randint(
        1, 10
    )  # Choose a random number of agents between 1 and 10
    print(f"Episode {i} number of agents: ", new_agent_count)

    # Update the env_config with the new agent count
    trainer.config["env_config"]["initial_agent_count"] = new_agent_count

    # Update all worker environments
    def update_env(env):
        if isinstance(env, UnityToGymWrapper):
            env.reset(options={"new_agent_count": new_agent_count})

    trainer.workers.foreach_worker(lambda worker: worker.foreach_env(update_env))

    # Train for one iteration
    result = trainer.train()
    print(f"Iteration {i}: reward_mean={result['episode_reward_mean']}")


# Train the model using PPO
# tune.run(
#    ppo.PPOTrainer,
#    config=config,
#    stop={"training_iteration": 10}  # Number of training iterations
# )

# Close Ray
ray.shutdown()
