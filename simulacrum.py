import gymnasium as gym
import numpy as np
import pygame
from ray.rllib.env.multi_agent_env import MultiAgentEnv
from ray.rllib.utils.typing import MultiAgentDict
from typing import Optional, Dict, Any, Tuple, List
import traffic_simulation  # Import the compiled C++ module

# Constants for the Pygame simulation
SCREEN_WIDTH = 900
SCREEN_HEIGHT = 400
LANE_WIDTH = 100
VEHICLE_WIDTH = 130
VEHICLE_HEIGHT = 55
NUM_LANES = 3
FPS = 25

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
        self.agents = ["agent_" + str(i) for i in range(configs.get("num_agents", 2))]
        self.sim = traffic_simulation.TrafficSimulation(configs.get("num_agents", 2))
        self.agent_positions = {agent: np.array([0.0, 0.0]) for agent in self.agents}
        self.previous_positions = {agent: np.copy(self.agent_positions[agent]) for agent in self.agents}
        self.collisions = {agent: False for agent in self.agents}
        self.observation_space = gym.spaces.Box(low=-np.inf, high=np.inf, shape=(8,), dtype=np.float32)

        # 0: keep lane, 1: change lane, 2: accelerate, 3: decelerate
        self.high_level_action_space = gym.spaces.Discrete(4)
        self.low_level_action_space = gym.spaces.Box(low=-1.0, high=1.0, shape=(3,), dtype=np.float32)
        self.action_space = gym.spaces.Dict({
            "discrete": self.high_level_action_space,
            "continuous": self.low_level_action_space
        })

        self.pygame_init = False
        self.render_mode = configs.get("render_mode", None)
        self.max_episode_steps = configs.get("max_episode_steps", 10000)
        self.episode_step_count = 0
        self.terminateds = {"__all__": False}
        self.truncateds = {"__all__": False}
        self.infos = {}

        self.configs = {
            "progress": True,
            "collision": True,
            "safety_distance": False
        }

        if configs:
            self.configs.update(configs)

    def reset(self, seed: Optional[int] = None, options: Optional[Dict[str, Any]] = None) -> Tuple[Dict[str, np.ndarray], Dict[str, Any]]:
        """
        Reset the environment to the initial state.

        Args:
            seed (Optional[int]): Random seed.
            options (Optional[Dict[str, Any]]): Additional reset options.

        Returns:
            Tuple[Dict[str, np.ndarray], Dict[str, Any]]: Initial observations and info.
        """
        self.episode_step_count = 0
        # Setting the termination status of all agents to False at the start of each step.
        self.terminateds = {"__all__": False}
        self.truncateds = {"__all__": False}
        self.infos = {}

        self.sim = traffic_simulation.TrafficSimulation(len(self.agents))  # Reset the simulation

        # Get the initial agent positions and velocities from the simulation
        self.agent_positions = {agent: np.array(pos) for agent, pos in self.sim.get_agent_positions().items()}
        self.agent_velocities = {agent: np.array([pos[0], pos[1], 0.0]) for agent, pos in self.sim.get_agent_velocities().items()}
        self.previous_positions = {agent: np.copy(self.agent_positions[agent]) for agent in self.agents}

        self.collisions = {agent: False for agent in self.agents}  # Reset collisions

        observations = {agent: self._get_observation(agent) for agent in self.agents}

        return observations, self.infos

    def step(self, action_dict: MultiAgentDict) -> Tuple[Dict[str, np.ndarray], Dict[str, float], Dict[str, bool], Dict[str, bool], Dict[str, Any]]:
        """
        Execute one step in the environment.

        Args:
            action_dict (MultiAgentDict): Dictionary of actions for each agent.

        Returns:
            Tuple[Dict[str, np.ndarray], Dict[str, float], Dict[str, bool], Dict[str, bool], Dict[str, Any]]:
                Observations, rewards, terminations, truncations, and additional info.
        """
        self.episode_step_count += 1

        observations = {}
        rewards = {}
        reward_components = {}

        high_level_actions = [action_dict[agent]["discrete"] for agent in self.agents]
        low_level_actions = [action_dict[agent]["continuous"].tolist() for agent in self.agents]
        self.sim.step(high_level_actions, low_level_actions)

        self.agent_positions = {agent: np.array(pos) for agent, pos in self.sim.get_agent_positions().items()}
        self.previous_positions = {agent: np.array(pos) for agent, pos in self.sim.get_previous_positions().items()}

        for agent, action in action_dict.items():
            reward_components = self._get_reward(agent)
            total_reward = sum(reward_components.values())
            rewards[agent] = total_reward

            self.infos[agent] = {
                "reward_components": reward_components,
                "total_reward": total_reward
            }

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
        else:
            self.terminateds["__all__"] = all(self.terminateds.values())

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

        mean_other_positions = np.mean(other_positions, axis=0) if other_positions else np.zeros(2)
        mean_other_velocities = np.mean(other_velocities, axis=0) if other_velocities else np.zeros(2)

        observation = np.concatenate([ego_position, ego_velocity, mean_other_positions, mean_other_velocities])

        return observation

    def _get_reward(self, agent: str) -> Dict[str, float]:
        """
        Calculate the reward for the given agent.

        Args:
            agent (str): The agent ID.

        Returns:
            Dict[str, float]: Dictionary of reward components.
        """
        reward_components = {
            "progress": 0.0,
            "collision": 0.0,
            "safety_distance": 0.0
        }

        agent_positions = self.sim.get_agent_positions()
        agent_position = np.array(agent_positions[agent])

        if self.configs["progress"]:
            if agent_position[0] > self.previous_positions[agent][0]:
                reward_components["progress"] += 1.0

        if self.configs["collision"]:
            for other_agent in [a for a in self.agents if a != agent]:
                other_agent_position = agent_positions[other_agent]
                distance = np.linalg.norm(agent_position - other_agent_position)
                if distance < VEHICLE_WIDTH:
                    reward_components["collision"] -= 1.0

        if self.configs["safety_distance"]:
            for other_agent in [a for a in self.agents if a != agent]:
                other_agent_position = agent_positions[other_agent]
                distance = np.linalg.norm(agent_position - other_agent_position)
                if distance < 2 * VEHICLE_WIDTH:
                    reward_components["safety_distance"] -= 1.0
            if agent_position[1] < LANE_WIDTH or agent_position[1] >= SCREEN_HEIGHT - LANE_WIDTH:
                reward_components["safety_distance"] -= 1.0

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
            self.clock = pygame.time.Clock() # Initialize clock
            self.pygame_init = True

        self.screen.fill((255, 255, 255))
        pygame.draw.rect(self.screen, (0, 0, 0), (0, LANE_WIDTH, SCREEN_WIDTH, SCREEN_HEIGHT - 2 * LANE_WIDTH))

        for i in range(15):
            pygame.draw.line(self.screen, (255, 255, 255), (i * 60, SCREEN_HEIGHT // 2), ((i * 60) + 30, SCREEN_HEIGHT // 2), 5)

        agent_positions = self.sim.get_agent_positions()

        for agent, pos in agent_positions.items():
            x, y = int(pos[0]), int(pos[1])
            color = (255, 0, 0) if self.collisions[agent] else (0, 0, 255)
            pygame.draw.rect(self.screen, color, (x - VEHICLE_WIDTH // 2, y - VEHICLE_HEIGHT // 2, VEHICLE_WIDTH, VEHICLE_HEIGHT))

        pygame.display.flip()
        self.clock.tick(FPS) # Cap the frame rate

    def close(self) -> None:
        """
        Close the Pygame window if initialized.
        """
        if self.pygame_init:
            pygame.quit()
            self.pygame_init = False

# Example of running the environment with random actions
if __name__ == "__main__":
    # Configure parameters
    configs = {
        "progress": True,
        "collision": True,
        "safety_distance": True,
        "max_episode_steps": 1000,
        "num_agents": 2,
        "render_mode": "human"
    }

    env = HighwayEnv(configs=configs)  # Set render_mode to True to enable rendering
    num_episodes = 8  # Define the number of episodes

    for episode in range(num_episodes):
        print(f"Starting episode {episode + 1}")

        observations, infos = env.reset()
        done = False

        while not done:
            env.render()

            actions = {agent: {"discrete": env.high_level_action_space.sample(), "continuous": env.low_level_action_space.sample()} for agent in env.agents}

            observations, rewards, terminateds, truncateds, infos = env.step(actions)

            for agent in env.agents:
                total_reward = rewards[agent]
                print(f"Rewards for {agent}: {total_reward}")
                print(f"Reward components for {agent}: {infos[agent]}")

            done = terminateds.get("__all__", False) or terminateds.get("__all__", False)

        print(f"Episode {episode + 1} finished")

    env.close()
