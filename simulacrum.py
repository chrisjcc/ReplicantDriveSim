import os
from typing import Any, Dict, Optional, Tuple

import gymnasium as gym
import mlflow
import numpy as np
import pygame
import traffic_simulation  # Import the compiled C++ module
from ray.rllib.env.multi_agent_env import MultiAgentEnv
from ray.rllib.utils.typing import MultiAgentDict

# Constants for the Pygame simulation
SCREEN_WIDTH = 900
SCREEN_HEIGHT = 400
LANE_WIDTH = 100
VEHICLE_WIDTH = 130
VEHICLE_HEIGHT = 55
NUM_LANES = 3
FPS = 25


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


class HighwayEnv(MultiAgentEnv):
    """
    Custom multi-agent highway environment.

    This environment simulates a highway with multiple agents. Each agent is
    represented by a vehicle and can perform high-level and low-level actions.
    The environment supports rendering using Pygame.
    """

    def __init__(self, configs: Optional[Dict[str, Any]] = None) -> None:
        """
        Initialize the HighwayEnv.

        Args:
            configs (Optional[Dict[str, Any]]): Configuration dictionary containing environment settings.
        """
        super().__init__()
        self.configs = configs
        self.agents = [
            "agent_" + str(i) for i in range(self.configs.get("num_agents", 2))
        ]
        self.sim = traffic_simulation.TrafficSimulation(
            self.configs.get("num_agents", 2)
        )
        self.agent_positions = {agent: np.array([0.0, 0.0]) for agent in self.agents}
        self.previous_positions = {
            agent: np.copy(self.agent_positions[agent]) for agent in self.agents
        }  # Track previous positions
        self.collisions = {agent: False for agent in self.agents}  # Track collisions
        self.observation_space = gym.spaces.Box(
            low=-np.inf, high=np.inf, shape=(8,), dtype=np.float32
        )

        # 0: keep lane, 1: left change lane, 2: right lane change,  3: accelerate, 4: decelerate
        self.high_level_action_space = gym.spaces.Discrete(5)
        # Control action correspond to steering angle (rad), acceleration (m/s^2), and braking (m/s^2)
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

        self.pygame_init = False
        self.render_mode = self.configs.get("render_mode", "human")
        self.max_episode_steps = self.configs.get("max_episode_steps", 10000)
        self.step_count = 0
        self.episode_count = 0
        self.episode_step_count = 0
        self.terminateds = {"__all__": False}
        self.truncateds = {"__all__": False}
        self.infos = {}

        mlflow.log_params(
            {
                "num_agents": self.configs.get("num_agents", 2),
                "max_episode_steps": self.configs.get("max_episode_steps", 10000),
                "progress_reward": self.configs.get("progress", False),
                "collision_reward": self.configs.get("collision", False),
                "safety_distance_reward": self.configs.get("safety_distance", False),
            }
        )

    def reset(
        self, seed: Optional[int] = None, options: Optional[Dict[str, Any]] = None
    ) -> Tuple[Dict[str, np.ndarray], Dict[str, Any]]:
        """
        Reset the environment to the initial state.

        Args:
            seed (Optional[int]): Random seed.
            options (Optional[Dict[str, Any]]): Additional reset options.

        Returns:
            Tuple[Dict[str, np.ndarray], Dict[str, Any]]: Initial observations and info.
        """
        # Setting the termination status of all agents to False at the start of each step.
        self.episode_step_count = 0
        self.terminateds = {"__all__": False}
        self.truncateds = {"__all__": False}
        self.infos = {agent: {"cumulative_reward": 0.0} for agent in self.agents}  # Initialize cumulative rewards

        self.sim = traffic_simulation.TrafficSimulation(
            len(self.agents)
        )  # Reset the simulation

        # Get the initial agent positions and velocities from the simulation
        self.agent_positions = {
            agent: np.array(pos)
            for agent, pos in self.sim.get_agent_positions().items()
        }
        self.agent_velocities = {
            agent: np.array([pos[0], pos[1], 0.0])
            for agent, pos in self.sim.get_agent_velocities().items()
        }
        self.previous_positions = {
            agent: np.copy(self.agent_positions[agent]) for agent in self.agents
        }

        self.collisions = {agent: False for agent in self.agents}  # Reset collisions

        observations = {agent: self._get_observation(agent) for agent in self.agents}

        return observations, self.infos

    def step(self, action_dict: MultiAgentDict) -> Tuple[
        Dict[str, np.ndarray],
        Dict[str, float],
        Dict[str, bool],
        Dict[str, bool],
        Dict[str, Any],
    ]:
        """
        Execute one step in the environment.

        Args:
            action_dict (MultiAgentDict): Dictionary of actions for each agent.

        Returns:
            Tuple[Dict[str, np.ndarray], Dict[str, float], Dict[str, bool], Dict[str, bool], Dict[str, Any]]:
                Observations, rewards, terminations, truncations, and additional info.
        """
        observations = {}
        rewards = {}
        reward_components = {}

        high_level_actions = [action_dict[agent]["discrete"] for agent in self.agents]
        low_level_actions = [
            action_dict[agent]["continuous"].tolist() for agent in self.agents
        ]

        # Apply the actions directly to the simulation
        self.sim.step(high_level_actions, low_level_actions)

        self.agent_positions = {
            agent: np.array(pos)
            for agent, pos in self.sim.get_agent_positions().items()
        }
        self.previous_positions = {
            agent: np.array(pos)
            for agent, pos in self.sim.get_previous_positions().items()
        }

        for agent, action in action_dict.items():
            reward_components = self._get_reward(agent)
            rewards[agent] = sum(reward_components.values()) # Total step reward

            # Log individual rewards for each agent
            mlflow.log_metric(f"{agent}_total_reward", rewards[agent], step=self.step_count)

            # Update cumulative_reward
            self.infos[agent]["cumulative_reward"] += rewards[agent]

            # Update other info fields without overwriting existing data
            self.infos[agent].update({
                "reward_components": reward_components,
                "total_reward": rewards[agent],
            })

        # After updating the termination statuses of individual agents (e.g., due to collisions),
        # it is essential to check if all agents in the environment are terminated.
        # If all agents are terminated, the episode should end for all agents.
        # Update terminateds for each agent
        self.terminateds = {agent: False for agent in self.agents}
        self.truncateds = {"__all__": False}

        # Check if the episode step count exceeds the maximum episode steps
        if self.episode_step_count > self.max_episode_steps:
            self.truncateds = {"__all__": True}
            self.terminateds["__all__"] = True
            self.episode_count += 1

            for agent in self.agents:
                # Log mean reward metric to MLflow
                mlflow.log_metric(f"{agent}_mean_reward", self.infos[agent]["cumulative_reward"] / self.episode_step_count, step=self.episode_count)
        else:
            self.terminateds["__all__"] = all(self.terminateds.values())

        self.episode_step_count += 1
        self.step_count += 1

        observations = {agent: self._get_observation(agent) for agent in self.agents}

        return observations, rewards, self.terminateds, self.truncateds, self.infos

    def _get_observation(self, agent: str) -> np.ndarray:
        """
        Generate an observation for the given agent.

        Args:
            agent (str): The agent ID.

        Returns:
            np.ndarray: The observation array.
        """
        # Observation space is similar to KinematicObservation in highway-env
        agent_positions = self.sim.get_agent_positions()
        agent_velocities = self.sim.get_agent_velocities()

        ego_position = np.array(agent_positions[agent])
        ego_velocity = np.array(agent_velocities[agent])

        other_agents = [a for a in self.agents if a != agent]
        other_positions = [np.array(agent_positions[a]) for a in other_agents]
        other_velocities = [np.array(agent_velocities[a]) for a in other_agents]

        mean_other_positions = (
            np.mean(other_positions, axis=0) if other_positions else np.zeros(2)
        )
        mean_other_velocities = (
            np.mean(other_velocities, axis=0) if other_velocities else np.zeros(2)
        )

        observation = np.concatenate(
            [ego_position, ego_velocity, mean_other_positions, mean_other_velocities]
        )

        return observation

    def _get_reward(self, agent: str) -> Dict[str, float]:
        """
        Calculate the reward for the given agent.

        Args:
            agent (str): The agent ID.

        Returns:
            Dict[str, float]: Dictionary of reward components.
        """
        reward_components = {"progress": 0.0, "collision": 0.0, "safety_distance": 0.0}

        agent_positions = self.sim.get_agent_positions()
        agent_position = np.array(agent_positions[agent])

        if self.configs.get("progress", False):
            if agent_position[0] > self.previous_positions[agent][0]:
                reward_components["progress"] += 1.0

        if self.configs.get("collision", False):
            for other_agent in [a for a in self.agents if a != agent]:
                other_agent_position = agent_positions[other_agent]
                distance = np.linalg.norm(agent_position - other_agent_position)
                if distance < VEHICLE_WIDTH:
                    reward_components["collision"] -= 1.0

        if self.configs.get("safety_distance", False):
            for other_agent in [a for a in self.agents if a != agent]:
                other_agent_position = agent_positions[other_agent]
                distance = np.linalg.norm(agent_position - other_agent_position)
                if distance < 2 * VEHICLE_WIDTH:
                    reward_components["safety_distance"] -= 1.0
            if (
                agent_position[1] < LANE_WIDTH
                or agent_position[1] >= SCREEN_HEIGHT - LANE_WIDTH
            ):
                reward_components["safety_distance"] -= 1.0

        # Reward for lane change
        if self.configs.get("left_lane_change", False):
            if agent_position[1] != self.previous_positions[agent][1]:
                reward_components["left_lane_change"] += 1.0

        # Reward for lane change
        if self.configs.get("right_lane_change", False):
            if agent_position[1] != self.previous_positions[agent][1]:
                reward_components["right_lane_change"] += 1.0

        # Reward for speed control
        if self.configs.get("speed_control", False):
            velocity = self.sim.get_agent_velocities()[agent][0]
            if velocity > 0:
                reward_components["speed_control"] += 0.1

        return reward_components

    def render(self) -> None:
        """
        Render the environment using Pygame.
        """
        if not self.render_mode:
            return

        if not self.pygame_init:
            pygame.init()
            self.screen = pygame.display.set_mode((SCREEN_WIDTH, SCREEN_HEIGHT))
            pygame.display.set_caption("Highway Simulation")
            self.clock = pygame.time.Clock()  # Initialize clock
            self.pygame_init = True

        self.screen.fill((255, 255, 255))
        pygame.draw.rect(
            self.screen,
            (0, 0, 0),
            (0, LANE_WIDTH, SCREEN_WIDTH, SCREEN_HEIGHT - 2 * LANE_WIDTH),
        )

        for i in range(15):
            pygame.draw.line(
                self.screen,
                (255, 255, 255),
                (i * 60, SCREEN_HEIGHT // 2),
                ((i * 60) + 30, SCREEN_HEIGHT // 2),
                5,
            )

        agent_positions = self.sim.get_agent_positions()

        for agent, pos in agent_positions.items():
            x, y = int(pos[0]), int(pos[1])
            color = (255, 0, 0) if self.collisions[agent] else (0, 0, 255)
            pygame.draw.rect(
                self.screen,
                color,
                (
                    x - VEHICLE_WIDTH // 2,
                    y - VEHICLE_HEIGHT // 2,
                    VEHICLE_WIDTH,
                    VEHICLE_HEIGHT,
                ),
            )

        pygame.display.flip()
        self.clock.tick(FPS)  # Cap the frame rate

    def close(self) -> None:
        """
        Close the Pygame window if initialized.
        """
        if self.pygame_init:
            pygame.quit()
            self.pygame_init = False

        # End mlflow run
        mlflow.end_run()


# Example of running the environment with random actions
if __name__ == "__main__":

    # Configure parameters
    configs = {
        "progress": True,
        "collision": True,
        "safety_distance": True,
        "max_episode_steps": 1000,
        "num_agents": 2,
        "render_mode": "human",
    }

    # Log params for main run
    mlflow.log_params(configs)

    env = HighwayEnv(configs=configs)  # Set render_mode to True to enable rendering
    num_episodes = 8  # Define the number of episodes

    for episode in range(num_episodes):
        print(f"Starting episode {episode + 1}")

        observations, infos = env.reset()
        done = False

        while not done:
            env.render()

            actions = {
                agent: {
                    "discrete": env.high_level_action_space.sample(),
                    "continuous": env.low_level_action_space.sample(),
                }
                for agent in env.agents
            }

            observations, rewards, terminateds, truncateds, infos = env.step(actions)

            for agent in env.agents:
                total_reward = rewards[agent]
                print(f"Rewards for {agent}: {total_reward}")
                print(f"Reward components for {agent}: {infos[agent]}")

            done = terminateds.get("__all__", False) or terminateds.get(
                "__all__", False
            )

        print(f"Episode {episode + 1} finished")

    env.close()
    mlflow.end_run()
