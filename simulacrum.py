# Two action option
import gymnasium as gym
import numpy as np
import pygame
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


class HighwayEnv(MultiAgentEnv):
    def __init__(self, num_agents=2, render_mode=False, configs=None):
        self.agents = ["agent_" + str(i) for i in range(num_agents)]
        self.agent_positions = {agent: np.random.uniform(0, SCREEN_WIDTH - VEHICLE_WIDTH, size=2) for agent in self.agents}
        self.agent_velocities = {agent: np.array([0.0, 0.0, 0.0]) for agent in self.agents}  # Include steering
        self.previous_positions = {agent: np.copy(self.agent_positions[agent]) for agent in self.agents}  # Track previous positions
        self.collisions = {agent: False for agent in self.agents}  # Track collisions

        self.observation_space = gym.spaces.Box(low=-np.inf, high=np.inf, shape=(8,), dtype=np.float32)

        # Create a custom action space that combines discrete and continuous actions
        self.high_level_action_space = gym.spaces.Discrete(4)  # 0: keep lane, 1: change lane, 2: accelerate, 3: decelerate
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

        # Default reward configurations
        self.configs = {
            "progress": True,
            "collision": True,
            "safety_distance": False
        }

        # Update reward configurations if provided
        if configs:
            self.configs.update(configs)

    def reset(self, seed=None, options=None):
        self.episode_step_count = 0
        self.terminated = {"__all__": False}
        self.truncated = {"__all__": False}

        self.agent_positions = {agent: np.random.uniform(0, SCREEN_WIDTH - VEHICLE_WIDTH, size=2) for agent in self.agents}
        self.agent_velocities = {agent: np.array([0.0, 0.0, 0.0]) for agent in self.agents}  # Include steering
        self.previous_positions = {agent: np.copy(self.agent_positions[agent]) for agent in self.agents}  # Reset previous positions
        self.collisions = {agent: False for agent in self.agents}  # Reset collisions

        return {agent: self._get_observation(agent) for agent in self.agents}, {}

    def step(self, action_dict: MultiAgentDict):
        self.episode_step_count += 1

        observations = {}
        rewards = {}
        reward_components = {}
        info = {}
 
        for agent, action in action_dict.items():
            high_level_action = action["discrete"] 
            low_level_action = action["continuous"]

            # Update previous positions before taking action
            self.previous_positions[agent] = np.copy(self.agent_positions[agent])

            self._take_action(agent, high_level_action, low_level_action)

            # Check for collisions
            self._check_collision(agent)

            reward_components = self._get_reward(agent)
            total_reward = sum(reward_components.values())
            rewards[agent] = total_reward

            # Include reward components in the info dictionary
            info[agent] = {
                "reward_components": reward_components,
                 "total_reward": total_reward
            }

        # Set termination and truncation statuses
        self.terminated = {agent: False for agent in self.agents} # Setting the termination status of all agents to False at the start of each step.
        self.truncated = {"__all__": False}

        # Update termination status based on collisions
        for agent in self.agents:
            if self.collisions[agent]:
                self.terminated[agent] = True

        # Check if the maximum episode steps have been reached
        if self.episode_step_count > self.max_episode_steps:
            self.truncated = {"__all__": True}

        # Check if all agents are terminated
        if all(self.terminated.values()):
            # After updating the termination statuses of individual agents (e.g., due to collisions), it is essential to check if all agents in the environment are terminated.
            # If all agents are terminated, the episode should end for all agents.
            self.terminated["__all__"] = True
        else:
            self.terminated["__all__"] = False

        observations = {agent: self._get_observation(agent) for agent in self.agents}

        return observations, rewards, self.terminated, self.truncated, info

    def _take_action(self, agent, high_level_action, low_level_action):
        if high_level_action == 0:  # Keep lane
            pass
        elif high_level_action == 1:  # Change lane
            self.agent_positions[agent][1] += LANE_WIDTH
            self.agent_positions[agent][1] = np.clip(self.agent_positions[agent][1], LANE_WIDTH, (NUM_LANES - 1) * LANE_WIDTH)
        elif high_level_action == 2:  # Accelerate
            self.agent_velocities[agent][1] += low_level_action[1]  # Acceleration
        elif high_level_action == 3:  # Decelerate
            self.agent_velocities[agent][1] -= low_level_action[2]  # Braking

        # Apply steering
        self.agent_velocities[agent][0] += low_level_action[0]  # Steering

        # Ensure velocities are within reasonable bounds
        max_velocity = 10.0  # Adjust this value as needed
        self.agent_velocities[agent] = np.clip(self.agent_velocities[agent], -max_velocity, max_velocity)

        # Update position based on velocity
        self.agent_positions[agent] += self.agent_velocities[agent][:2]

        # Wrap around screen boundaries horizontally
        self.agent_positions[agent][0] = (self.agent_positions[agent][0] + SCREEN_WIDTH) % SCREEN_WIDTH

        # Constrain vertical position to within road boundaries
        self.agent_positions[agent][1] = np.clip(self.agent_positions[agent][1], LANE_WIDTH, (NUM_LANES - 1) * LANE_WIDTH)

    def _check_collision(self, agent):
        # Check for collisions with other agents
        for other_agent in self.agents:
            if other_agent != agent:
                if np.linalg.norm(self.agent_positions[agent] - self.agent_positions[other_agent]) < VEHICLE_WIDTH:
                    self.collisions[agent] = True
                    self.collisions[other_agent] = True

    def _get_observation(self, agent):
        # Observation space is similar to KinematicObservation in highway-env
        other_agents = [a for a in self.agents if a != agent]
        other_positions = [self.agent_positions[a] for a in other_agents]
        other_velocities = [self.agent_velocities[a][:2] for a in other_agents]
        ego_position = self.agent_positions[agent]
        ego_velocity = self.agent_velocities[agent][:2]

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

        # Progress reward
        if self.configs["progress"]:
            if self.agent_positions[agent][0] > self.previous_positions[agent][0]:
                reward_components["progress"] += 1.0

        # Collision penalty
        if self.configs["collision"]:
            for other_agent in [a for a in self.agents if a != agent]:
                distance = np.linalg.norm(self.agent_positions[agent] - self.agent_positions[other_agent])
                if distance < VEHICLE_WIDTH:
                    reward_components["collision"] -= 1.0


        # Safety distance reward
        if self.configs["safety_distance"]:
            for other_agent in [a for a in self.agents if a != agent]:
                distance = np.linalg.norm(self.agent_positions[agent] - self.agent_positions[other_agent])
                if distance < 2 * VEHICLE_WIDTH:
                    reward_components["safety_distance"] -= 1.0
            if self.agent_positions[agent][1] < LANE_WIDTH or self.agent_positions[agent][1] >= SCREEN_HEIGHT - LANE_WIDTH:
                reward_components["safety_distance"] -= 1.0

        return reward_components

    def render(self, mode="human"):
        if not self.render_mode:
            return

        if not self.pygame_init:
            pygame.init()
            self.screen = pygame.display.set_mode((SCREEN_WIDTH, SCREEN_HEIGHT))
            pygame.display.set_caption("Highway Simulation")
            self.clock = pygame.time.Clock()  # Initialize clock
            self.pygame_init = True

        self.screen.fill((255, 255, 255))

        # Draw lanes
        pygame.draw.rect(self.screen, (0, 0, 0), (0, LANE_WIDTH, SCREEN_WIDTH, SCREEN_HEIGHT - 2 * LANE_WIDTH))

        # Draw dashed white line
        for i in range(15):
            pygame.draw.line(self.screen, (255, 255, 255), (i * 60, SCREEN_HEIGHT // 2), ((i * 60) + 30, SCREEN_HEIGHT // 2), 5)

        # Draw vehicles
        for agent in self.agents:
            x, y = self.agent_positions[agent]
            color = (255, 0, 0) if self.collisions[agent] else (0, 0, 255)

            pygame.draw.rect(self.screen, color, (x, y, VEHICLE_WIDTH, VEHICLE_HEIGHT))

        pygame.display.flip()
        self.clock.tick(FPS)  # Cap the frame rate

    def close(self):
        if self.pygame_init:
            pygame.quit()


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
                print(f"Reward components for {agent}: {info[agent]}")

            done = terminated.get("__all__", False) or truncated.get("__all__", False)

        print(f"Episode {episode + 1} finished")

    env.close()
