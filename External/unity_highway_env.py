import math
import os
from typing import Any, Dict, Optional, Tuple

import gymnasium as gym
import mlflow
import numpy as np
import pygame
import simulation  # Import the compiled C++ module
from ray.rllib.env.multi_agent_env import MultiAgentEnv
from ray.rllib.utils.typing import MultiAgentDict

# Set MLflow tracking URI
mlflow_tracking_uri = os.path.abspath(os.path.join(os.getcwd(), "mlruns"))
os.environ["MLFLOW_TRACKING_URI"] = f"file://{mlflow_tracking_uri}"

# Define your experiment name
experiment_name = "MyExperiment"

# Create the experiment (if it doesn't exist, it will be created; otherwise, it will retrieve existing)
# Attempt to create the experiment if it doesn't exist (this check avoids errors if it already exists)
try:
    mlflow.create_experiment(experiment_name)
except mlflow.exceptions.MlflowException as e:
    print(f"Experiment '{experiment_name}' already exists.")

# Set the default experiment (replace "MyExperiment" with your desired experiment name)
mlflow.set_experiment(experiment_name)

# Get the experiment ID by name
experiment = mlflow.get_experiment_by_name(experiment_name)

if experiment:
    experiment_id = experiment.experiment_id
else:
    raise ValueError(f"Experiment '{experiment_name}' does not exist.")

# Start MLflow tracking with explicit experiment ID
mlflow.start_run(run_name="HighwayEnv", experiment_id=experiment_id, nested=False)


class HybridHighwayEnv(MultiAgentEnv):
    """
    Custom multi-agent highway environment.

    This environment simulates a highway with multiple agents. Each agent is
    represented by a vehicle and can perform high-level and low-level actions.
    The environment supports rendering using Pygame.
    """
    def __init__(self, configs: Optional[Dict[str, Any]] = None):
        """
        Initialize the HighwayEnv.

        Args:
            configs (Optional[Dict[str, Any]]): Configuration dictionary containing environment settings.
        """
        super().__init__()
        self.configs = configs or {}
        self.num_agents = self.configs.get("num_agents", 2)
        self.agents = [f"agent_{i}" for i in range(self.num_agents)]
        
        # Initialize Unity environment
        self.engine_configuration_channel = EngineConfigurationChannel()
        self.environment_parameters_channel = EnvironmentParametersChannel()
        self.unity_env = UnityEnvironment(
            file_name="path/to/your/HighwayEnv.exe",  # Update this path
            side_channels=[self.engine_configuration_channel, self.environment_parameters_channel],
            no_graphics=self.configs.get("render_mode") is None,
            worker_id=0
        )
        self.unity_env.reset()
        self.behavior_name = list(self.unity_env.behavior_specs)[0]
        self.spec = self.unity_env.behavior_specs[self.behavior_name]
        
        # Set up action and observation spaces
        self.high_level_action_space = gym.spaces.Discrete(5)
        self.low_level_action_space = gym.spaces.Box(
            low=np.array([-0.610865, 0.0, -8.0], dtype=np.float32),
            high=np.array([0.610865, 4.5, 0.0], dtype=np.float32),
            shape=(3,),
            dtype=np.float32
        )
        self.action_space = gym.spaces.Dict({
            "discrete": self.high_level_action_space,
            "continuous": self.low_level_action_space
        })
        self.observation_space = gym.spaces.Box(
            low=-np.inf, high=np.inf, shape=(8,), dtype=np.float32
        )

    def reset(self, seed: Optional[int] = None, options: Optional[Dict[str, Any]] = None) -> Tuple[Dict[str, np.ndarray], Dict[str, Any]]:
        """
        Reset the environment to the initial state.

        Args:
            seed (Optional[int]): Random seed.
            options (Optional[Dict[str, Any]]): Additional reset options.

        Returns:
            Tuple[Dict[str, np.ndarray], Dict[str, Any]]: Initial observations and info.
        """
        self.unity_env.reset()
        decision_steps, _ = self.unity_env.get_steps(self.behavior_name)
        observations = {f"agent_{i}": decision_steps.obs[0][i] for i in range(self.num_agents)}
        infos = {f"agent_{i}": {} for i in range(self.num_agents)}
        return observations, infos

    def step(self, action_dict: Dict[str, Dict[str, np.ndarray]]) -> Tuple[Dict[str, np.ndarray], Dict[str, float], Dict[str, bool], Dict[str, bool], Dict[str, Any]]:
        """
        Execute one step in the environment.

        Args:
            action_dict (MultiAgentDict): Dictionary of actions for each agent.

        Returns:
            Tuple[Dict[str, np.ndarray], Dict[str, float], Dict[str, bool], Dict[str, bool], Dict[str, Any]]:
                Observations, rewards, terminations, truncations, and additional info.
        """
        unity_actions = ActionTuple()
        unity_actions.add_discrete(np.array([action_dict[agent]["discrete"] for agent in self.agents]))
        unity_actions.add_continuous(np.array([action_dict[agent]["continuous"] for agent in self.agents]))
        
        self.unity_env.set_actions(self.behavior_name, unity_actions)
        self.unity_env.step()
        
        decision_steps, terminal_steps = self.unity_env.get_steps(self.behavior_name)
        
        observations = {f"agent_{i}": decision_steps.obs[0][i] for i in range(self.num_agents)}
        rewards = {f"agent_{i}": decision_steps.reward[i] for i in range(self.num_agents)}
        terminateds = {f"agent_{i}": i in terminal_steps for i in range(self.num_agents)}
        truncateds = {f"agent_{i}": False for i in range(self.num_agents)}
        infos = {f"agent_{i}": {} for i in range(self.num_agents)}
        
        terminateds["__all__"] = all(terminateds.values())
        truncateds["__all__"] = False
        
        return observations, rewards, terminateds, truncateds, infos


    def render(self):
        # Unity environments are always rendered, so this method is a no-op
        pass

    def close(self):
        self.unity_env.close()

# Usage example
if __name__ == "__main__":
    configs = {
        "num_agents": 2,
        "render_mode": None
    }
    env = HybridHighwayEnv(configs)
    
    num_episodes = 5
    for episode in range(num_episodes):
        observations, _ = env.reset()
        done = False
        while not done:
            actions = {
                agent: {
                    "discrete": env.high_level_action_space.sample(),
                    "continuous": env.low_level_action_space.sample()
                }
                for agent in env.agents
            }
            observations, rewards, terminateds, truncateds, infos = env.step(actions)
            done = terminateds["__all__"] or truncateds["__all__"]
    
    env.close()
