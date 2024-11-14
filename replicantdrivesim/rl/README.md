# Multi-Agent Reinforcement Learning (MARL) with Ray RLlib

This repository demonstrates how to train multi-agent reinforcement learning (MARL) models using [Ray RLlib](https://docs.ray.io/en/latest/rllib.html). The `trainer.py` script allows you to configure, train, and evaluate MARL agents within a customizable environment.

<img src="https://raw.githubusercontent.com/chrisjcc/ReplicantDriveSim/main/External/images/marl-logo.png" alt="MARL LOGO" width="800" height="400">

## Getting Started

### Prerequisites

Before running the training script, ensure you have the following installed:

- Python 3.7+
- Ray 2.0.0+ with RLlib
- NumPy
- Gym (or custom environment)
- Any other dependencies listed in `requirements.txt` (if provided)

Install the required dependencies using pip:

```shell
pip install -r requirements.txt
```

Alternatively, can create a conda environment:
```shell
conda env create -f environment.yml
conda activate drive
```

### Usage
The main training script is `trainer.py`, which leverages Ray's RLlib to set up and train multiple agents within the specified environment. To start the training process, run:

```shell
python examples/trainer.py
```

### Configuration
The `trainer.py` script includes several configuration options to customize the training process:

- **Environment**: Define the environment for MARL training, whe are using our `CustomUnityMultiAgentEnv` a custom Gym-based multi-agent environment.
- **Algorithm**: Choose from various RL algorithms supported by RLlib such as PPO, DQN, A3C, etc.
- **Hyperparameters**: Adjust each agent's learning rates, batch sizes, and other hyperparameters.
- **Multi-Agent Setup**: Define the policies and mapping from agents to policies in the multi-agent environment.

### Parameterized Action Space
The ReplicantDriveSim environment implements a parameterized action space, aligning with the concept described in the paper ["Continuous Deep Q-Learning with Model-based Acceleration"](https://arxiv.org/abs/1603.00748). This approach combines discrete and continuous actions, allowing for both high-level decision-making and fine-tuned control.

#### Action Space Structure
The action space is defined as a tuple containing two elements:

- **Discrete Action**: A single value representing high-level decisions or action types.
- **Continuous Action Vector**: A 3-dimensional vector for fine-grained control.

```python
self._single_agent_action_space = gym.spaces.Tuple(
    (
        gym.spaces.Discrete(self.behavior_spec.action_spec.discrete_branches),
        gym.spaces.Box(
            low=np.array([-0.610865, 0.0, -8.0]),
            high=np.array([0.610865, 4.5, 0.0]),
            shape=(self.behavior_spec.action_spec.continuous_size,),
            dtype=np.float32,
        ),
    )
)
```

#### Key Features
- **Combined high-level and precise control**: Agents can make both high-level choices and precise adjustments.
- **Unity-compatible action formatting**: Actions are converted to a Unity-compatible format using the `_convert_to_action_tuple` method.
- **Complex agent behaviors**: The parameterized action space enables sophisticated agent behaviors, ideal for scenarios requiring nuanced control.

This implementation allows for a wide range of agent actions, from broad strategic decisions to fine-tuned movements, making it well-suited for complex reinforcement learning tasks in the ReplicantDriveSim environment.

### Running the Traffic Simulation
The traffic simulation supports both discrete high-level decision-making and continuous low-level control. Below is an example Python script demonstrating how to run the simulation and interact with the environment with agents performing both random actions.
The simulation supports two types of control:
- **Discrete high-level decision-making**: Actions such as lane changes (left or right), maintaining the current lane, speeding up, or slowing down.
- **Continuous low-level control**: Actions such as steering, throttle control, and braking.

```python
import os
import yaml
import ray
import argparse

import replicantdrivesim


def run_episodes(env, num_episodes):
    """Run a defined number of episodes with the environment."""
    for episode in range(num_episodes):
        print(f"Starting episode {episode + 1}")

        observations, _ = env.reset()
        done = False

        while not done:
            actions = env.action_space_sample()

            # Modify the actions for all agents
            for agent in actions:
                discrete_action, continuous_actions = actions[agent]
 
                # Breakdown of continuous actions
                # continuous_actions = 0.0  # Set steering to zero
                # continuous_actions = 0.1  # Some other modification
                # continuous_actions = 0.0  # Another modification

                actions[agent] = (discrete_action, continuous_actions)

            # Print the modified actions
            print(f"actions: {actions}")

            # Step the environment
            observations, rewards, terminateds, truncateds, infos = env.step(actions)
            print("rewards: ", rewards)

            # Check if the episode is done
            done = terminateds.get("__all__", False) or truncateds.get("__all__", False)

        print(f"Episode {episode + 1} finished")

def main():

    # Set up argument parser
    parser = argparse.ArgumentParser(description="Run a Unity environment simulation.")
    parser.add_argument(
        "--num-episodes",
        type=int,
        default=10,
        help="Number of episodes to run (default: 10)"
    )
    parser.add_argument(
       "--config-path",
       type=str,
       default=os.path.join("replicantdrivesim", "configs", "config.yaml"),
       help="Environment configuration."
    )
    # Parse command-line arguments
    args = parser.parse_args()

    num_episodes = int(args.num_episodes)
    config_path = str(args.config_path)

    # Load configuration from YAML file
    with open(config_path, "r") as config_file:
        config_data = yaml.safe_load(config_file)

    # Initialize Ray
    ray.init()

    # Create the Unity environment
    env = replicantdrivesim.make(env_name="replicantdrivesim-v0", config=config_data)

    # Run the episodes
    run_episodes(env, num_episodes)

    # Close the environment
    env.close()

if __name__ == "__main__":
    main()
```

This script sets up the environment, runs a series of episodes where agents perform actions, and allows you to modify actions programmatically before sending them to the environment. You can adjust the number of episodes and provide a custom configuration file path using command-line arguments.

### Training Script Overview
- `trainer.py`: The main script for configuring and running the multi-agent training using Ray RLlib. It includes setting up the environment, configuring the RLlib trainer, and executing the training loop.

### Results and Evaluation
After training, the results and checkpoints will be saved to the directory specified in the script. 

`~/ray_results/PPO_Highway_Experiment/PPO_CustomUnityMultiAgentEnv_2e073_00000_0_2024-10-22_16-59-04/`

You can use these checkpoints to evaluate the trained agents' performance or resume training.

#### View Previous Runs via the MLflow UI
If your `mlruns` folder contains your experiment runs, you can start an MLflow tracking server to view them.

### Start the MLflow UI:

- Navigate to the directory where the mlruns folder is located.
- Start the MLflow UI by running:

```bash
mlflow ui --backend-store-uri file:mlruns
```

This will start the MLflow UI on `localhost:5000`. Open a browser and go to:

```
http://127.0.0.1:5000
```

You can view all your experiments, metrics, parameters, and artifacts through the web interface.

### Additional Resources

[Ray RLlib Documentation](https://docs.ray.io/en/latest/rllib/index.html)

[Multi-Agent Training](https://marllib.readthedocs.io/en/latest/index.html)

### Acknowledgments
- The Ray Team for creating RLlib
- OpenAI Gym for providing standard RL environments
This `README.md` provides an overview of the MARL training setup with Ray RLlib, instructions for getting started, and a brief example. You can adapt this template as needed to fit the specifics of your project.
