import gymnasium as gym
import numpy as np
import pygame
from ray.rllib.env.multi_agent_env import MultiAgentEnv
from ray.rllib.utils.typing import MultiAgentDict
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
    def __init__(self, num_agents=2, render_mode=False, configs=None):
        self.agents = ["agent_" + str(i) for i in range(num_agents)]
        self.sim = traffic_simulation.TrafficSimulation(num_agents)
        self.agent_positions = {agent: np.array([0.0, 0.0]) for agent in self.agents}
        # Initialize previous positions dictionary to track previous positions of agents
        self.previous_positions = {agent: np.copy(self.agent_positions[agent]) for agent in self.agents}
        self.collisions = {agent: False for agent in self.agents}
        self.observation_space = gym.spaces.Box(low=-np.inf, high=np.inf, shape=(8,), dtype=np.float32)

        self.high_level_action_space = gym.spaces.Discrete(4)
        self.low_level_action_space = gym.spaces.Box(low=-1.0, high=1.0, shape=(3,), dtype=np.float32)
        self.action_space = gym.spaces.Dict({
            "discrete": self.high_level_action_space,
            "continuous": self.low_level_action_space
        })

        self.pygame_init = False
        self.render_mode = render_mode
        self.max_episode_steps = configs.get("max_episode_steps", 10000)
        self.episode_step_count = 0
        self.terminated = {"__all__": False}
        self.truncated = {"__all__": False}

        self.configs = {
            "progress": True,
            "collision": True,
            "safety_distance": False
        }

        if configs:
            self.configs.update(configs)

    def reset(self, seed=None, options=None):
        self.episode_step_count = 0
        self.terminated = {"__all__": False}
        self.truncated = {"__all__": False}
        self.sim = traffic_simulation.TrafficSimulation(len(self.agents))  # Reset the simulation
        observations = {agent: self._get_observation(agent) for agent in self.agents}

        return observations, {}

    def step(self, action_dict: MultiAgentDict):
        self.episode_step_count += 1

        observations = {}
        rewards = {}
        reward_components = {}
        info = {}

        high_level_actions = [action_dict[agent]["discrete"] for agent in self.agents]
        low_level_actions = [action_dict[agent]["continuous"].tolist() for agent in self.agents]
        self.sim.step(high_level_actions, low_level_actions)

        self.agent_positions = {agent: np.array(pos) for agent, pos in self.sim.get_agent_positions().items()}
        self.previous_positions = {agent: np.array(pos) for agent, pos in self.sim.get_previous_positions().items()}

        for agent, action in action_dict.items():
            reward_components = self._get_reward(agent)
            total_reward = sum(reward_components.values())
            rewards[agent] = total_reward

            info[agent] = {
                "reward_components": reward_components,
                "total_reward": total_reward
            }

        self.terminated = {agent: False for agent in self.agents}
        self.truncated = {"__all__": False}

        if self.episode_step_count > self.max_episode_steps:
            self.truncated = {"__all__": True}

        if all(self.terminated.values()):
            self.terminated["__all__"] = True

        observations = {agent: self._get_observation(agent) for agent in self.agents}

        return observations, rewards, self.terminated, self.truncated, info

    def _get_observation(self, agent):
        agent_positions = self.sim.get_agent_positions()
        agent_velocities = self.sim.get_agent_velocities()

        ego_position = np.array(agent_positions[agent])
        ego_velocity = np.array(agent_velocities[agent])

        other_agents = [a for a in self.agents if a != agent]
        other_positions = [np.array(agent_positions[a]) for a in other_agents]
        other_velocities = [np.array(agent_velocities[a]) for a in other_agents]

        if other_positions:
            mean_other_positions = np.mean(other_positions, axis=0)
        else:
            mean_other_positions = np.zeros(2)

        if other_velocities:
            mean_other_velocities = np.mean(other_velocities, axis=0)
        else:
            mean_other_velocities = np.zeros(2)

        observation = np.concatenate([ego_position, ego_velocity, mean_other_positions, mean_other_velocities])

        return observation

    def _get_reward(self, agent):
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

    def render(self, mode="human"):
        if not self.render_mode:
            return

        if not self.pygame_init:
            pygame.init()
            self.screen = pygame.display.set_mode((SCREEN_WIDTH, SCREEN_HEIGHT))
            pygame.display.set_caption("Highway Simulation")
            self.clock = pygame.time.Clock()
            self.pygame_init = True

        self.screen.fill((255, 255, 255))
        pygame.draw.rect(self.screen, (0, 0, 0), (0, LANE_WIDTH, SCREEN_WIDTH, SCREEN_HEIGHT - 2 * LANE_WIDTH))

        for i in range(15):
            pygame.draw.line(self.screen, (255, 255, 255), (i * 60, SCREEN_HEIGHT // 2), ((i * 60) + 30, SCREEN_HEIGHT // 2), 5)

        agent_positions = self.sim.get_agent_positions()
        for agent, pos in agent_positions.items():
            pygame.draw.circle(self.screen, (255, 0, 0), (int(pos[0]), int(pos[1])), VEHICLE_WIDTH // 2)

        pygame.display.flip()
        self.clock.tick(30)

    def close(self):
        if self.pygame_init:
            pygame.quit()
            self.pygame_init = False


# Example of running the environment with random actions
if __name__ == "__main__":

    # Configure training parameters
    configs = {
        "progress": True,
        "collision": True,
        "safety_distance": True,
        "max_episode_steps": 1000,
    }

    env = HighwayEnv(num_agents=2, render_mode=True, configs=configs)  # Set render_mode to True to enable rendering
    num_episodes = 8  # Define the number of episodes

    for episode in range(num_episodes):
        print(f"Starting episode {episode + 1}")

        observations = env.reset()
        done = False

        while not done:
            env.render()

            actions = {agent: {"discrete": env.high_level_action_space.sample(), "continuous": env.low_level_action_space.sample()} for agent in env.agents}

            observations, rewards, terminated, truncated, info = env.step(actions)

            for agent in env.agents:
                total_reward = rewards[agent]
                print(f"Rewards for {agent}: {total_reward}")
                print(info)
                print(f"Reward components for {agent}: {info[agent]}")

            done = terminated.get("__all__", False) or truncated.get("__all__", False)

        print(f"Episode {episode + 1} finished")

    env.close()
