import os
from typing import Any, Dict, Tuple

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
from ray.rllib.env.multi_agent_env import MultiAgentEnv

channel_id = uuid.UUID("621f0a70-4f87-11ea-a6bf-784f4387d1f7")


class CustomUnityMultiAgentEnv(MultiAgentEnv):
    def __init__(self, config, *args, **kwargs):
        super().__init__()
        self.file_name = config["file_name"]
        self.initial_agent_count = config.get("initial_agent_count", 2)
        self.current_agent_count = self.initial_agent_count

        # Initialize Unity environment here
        self.engine_configuration_channel = EngineConfigurationChannel()
        self.float_props_channel = FloatPropertiesChannel(channel_id)

        self.float_props_channel.set_property(
            "initialAgentCount", float(self.initial_agent_count)
        )

        self.env = UnityEnvironment(
            file_name=self.file_name,
            side_channels=[
                self.engine_configuration_channel,
                self.float_props_channel,
            ],
        )

        # Start the environment
        print(f"Initializing with {self.initial_agent_count} agents")
        self.env.reset()

        # Access the BehaviorSpec for the behavior
        self.behavior_name = list(self.env.behavior_specs.keys())[0]
        self.behavior_spec = self.env.behavior_specs[self.behavior_name]

        # Initialize observation and action spaces
        self.observation_spaces = {}
        self.action_spaces = {}

        # Get the actual number of agents after environment reset
        decision_steps, _ = self.env.get_steps(self.behavior_name)
        self.num_agents = len(decision_steps)
        print(f"Initial number of agents: {self.num_agents}")

        # Get observation size
        self.size_of_single_agent_obs = self.behavior_spec.observation_specs[0].shape[0]

        # You can access continuous and discrete action sizes like this:
        print(
            f"Continuous action size: {self.behavior_spec.action_spec.continuous_size}"
        )
        print(
            f"Discrete action branches: {self.behavior_spec.action_spec.discrete_branches}"
        )
        self._agent_ids = {f"agent_{i}" for i in range(self.num_agents)}
        self._update_spaces()

    def _update_spaces(self):
        single_agent_obs_space = gym.spaces.Box(
            low=-np.inf,
            high=np.inf,
            shape=(self.size_of_single_agent_obs,),
            dtype=np.float32,
        )
        print(f"Observation space for each agent: {single_agent_obs_space}")

        single_agent_action_space = gym.spaces.Tuple(
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

        self.observation_space = {
            agent: single_agent_obs_space for agent in self._agent_ids
        }
        self.action_space = {
            agent: single_agent_action_space for agent in self._agent_ids
        }

    def __str__(self):
        return f"<{type(self).__name__} with custom behavior spec>"

    def set_observation_space(self, agent_id, obs_space):
        self.observation_spaces[agent_id] = obs_space

    def set_action_space(self, agent_id, act_space):
        self.action_spaces[agent_id] = act_space

    def observation_space_contains(self, observation):
        # Check if the given observation is valid for any agent
        for agent_id, obs_space in self.observation_spaces.items():
            if obs_space.contains(observation.get(agent_id, None)):
                return True
        return False

    def action_space_sample(self):
        # Sample actions for each agent and return a dictionary
        actions = {}
        for agent_id, act_space in self.action_spaces.items():
            actions[agent_id] = act_space.sample()
        return actions

    def action_space_contains(self, action):
        # Check if the given action is valid for any agent's action space
        for agent_id, act_space in self.action_spaces.items():
            if act_space.contains(action.get(agent_id, None)):
                return True
        return False

    def reset(self, *, seed=None, options=None):
        # Handle seed if provided
        if seed is not None:
            np.random.seed(seed)

        # Check if options contain a new agent count
        if options and "new_agent_count" in options:
            new_agent_count = options["new_agent_count"]

            if new_agent_count != self.current_agent_count:
                self.current_agent_count = new_agent_count
                self.float_props_channel.set_property(
                    "initialAgentCount", float(new_agent_count)
                )
                print(f"Setting new agent count to: {new_agent_count}")

        print("Resetting Unity environment...")
        self.env.reset()
        self.env.reset()

        decision_steps, _ = self.env.get_steps(self.behavior_name)
        self.num_agents = len(decision_steps)
        print(f"Number of agents after reset: {self.num_agents}")

        #obs = np.array(decision_steps.obs[0], dtype=np.float32)
        #print(f"Observation shape: {obs.shape}")

        # Update num_agents and observation_space
        self._update_spaces()
        print(f"Updated action space: {self.action_space}")

        print(f"Number of agents after reset: {self.num_agents}")
        #print(f"Observation shape: {obs.shape}")
        print(f"Action space: {self.action_space}")

        #obs_dict = {f"agent_{i}": obs[i] for i in range(self.num_agents)}

        obs_dict = {}
        for agent_id, idx in enumerate(decision_steps.agent_id):
            print(f"TODO: agent_id: {agent_id}: idx: {idx}")
            obs_dict[f"agent_{agent_id}"] = decision_steps.obs[0][idx]

        obs_dict = {agent: obs.astype(np.float32) for agent, obs in obs_dict.items()}

        for agent_id, obs in obs_dict.items():
            if not self.observation_space[agent_id].contains(obs):
                print(f"Observation for {agent_id} is out of bounds:")
                print(f"Observation: {obs}")
                print(f"Observation space: {self.observation_space[agent_id]}")

        #print(f"Observation shape: {obs.shape}")

        return obs_dict, {}

    def step(
        self, action_dict: Dict[Any, Any]
    ) -> Tuple[
        Dict[Any, Any], Dict[Any, Any], Dict[Any, Any], Dict[Any, Any], Dict[Any, Any]
    ]:
        action_dict = {
            'agent_1': (5, np.array([[-0.610865, 0., -8.], [0.610865, 4.5, 0.]])),
            'agent_0': (5, np.array([[-0.610865, 0., -8.], [0.610865, 4.5, 0.]])),
            'agent_4': (5, np.array([[-0.610865, 0., -8.], [0.610865, 4.5, 0.]])),
            'agent_3': (5, np.array([[-0.610865, 0., -8.], [0.610865, 4.5, 0.]])),
            'agent_2': (5, np.array([[-0.610865, 0., -8.], [0.610865, 4.5, 0.]]))
        }

        print("TODO: ACTION: ", action_dict)

        # Initialize lists to store discrete and continuous actions
        discrete_actions = []
        continuous_actions = []

        # Loop over the actions to separate discrete and continuous actions
        for agent_id, action in action_dict.items():
            # Assuming action is of type gym.spaces.Tuple(gym.spaces.Discrete, gym.spaces.Box)
            discrete, continuous = action

            # Append the actions to the respective lists
            discrete_actions.append([discrete])
            continuous_actions.append(continuous)

        # Convert lists to numpy arrays
        discrete_actions = np.array(discrete_actions)
        continuous_actions = np.array(continuous_actions)

        print(f"Step - Number of agents: {self.num_agents}")
        print(f"Discrete action: {discrete_actions}")
        print(f"Continuous action: {continuous_actions}")

        action_tuple = ActionTuple(
            discrete=discrete_actions, continuous=continuous_actions
        )

        self.env.set_actions(self.behavior_name, action_tuple)
        self.env.step()

        decision_steps, terminal_steps = self.env.get_steps(self.behavior_name)

        obs_dict = {f"agent_{i}": obs[i] for i in range(self.num_agents)}
        rewards_dict = {f"agent_{i}": reward[i] for i in range(self.num_agents)}
        terminateds_dict = {f"agent_{i}": False for i in range(self.num_agents)}
        terminateds_dict["__all__"] = done
        truncateds_dict = {f"agent_{i}": truncated[i] for i in range(self.num_agents)}
        truncateds_dict["__all__"] = (
            False  # or `done`, depending on how you detect truncations
        )

        infos_dict = {}
        return obs_dict, rewards_dict, terminateds_dict, truncateds_dict, infos_dict

    def close(self):
        self.env.close()


def env_creator(env_config):
    return CustomUnityMultiAgentEnv(env_config)


# Initialize Ray
ray.init(ignore_reinit_error=True)

# Define PPO configuration
config = {
    "env": "CustomUnityMultiAgentEnv",
    "env_config": {
        "file_name": "libReplicantDriveSim.app",  # Path to your Unity executable
        "initial_agent_count": 5,  # Set your desired initial agent count here
    },
    "multiagent": {
        "policies": {
            "default_policy": (
                None,
                gym.spaces.Box(-np.inf, np.inf, (1,)),
                gym.spaces.Tuple(
                    (
                        gym.spaces.Discrete(2),
                        gym.spaces.Box(
                            low=np.array([-0.610865, 0.0, -8.0]),
                            high=np.array([0.610865, 4.5, 0.0]),
                            dtype=np.float32,
                        ),
                    )
                ),
                {},
            ),
        },
        "policy_mapping_fn": lambda agent_id, episode, worker, **kwargs: "default_policy",
    },
    # ... other config options ...
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
tune.register_env("CustomUnityMultiAgentEnv", env_creator)

# Initialize PPO trainer
trainer = PPO(config=config)

# Training loop
for i in range(10):
    # Generate a new agent count
    #new_agent_count = np.random.randint(
    #    1, 10
    #)  # Choose a random number of agents between 1 and 10
    new_agent_count = 5
    print(f"Episode {i} number of agents: ", new_agent_count)

    # Update the env_config with the new agent count
    trainer.config["env_config"]["initial_agent_count"] = new_agent_count

    # Update all worker environments
    def update_env(env):
        if isinstance(env, CustomUnityMultiAgentEnv): # UnityToGymWrapper
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
