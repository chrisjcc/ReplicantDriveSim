import os

import gymnasium as gym
import mlflow
import numpy as np

from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import (
    EngineConfigurationChannel,
)
from mlagents_envs.side_channel.float_properties_channel import FloatPropertiesChannel

import ray
from ray import tune
from ray.air.integrations.mlflow import MLflowLoggerCallback
from ray.rllib.algorithms.ppo import PPO
from ray.rllib.env.env_context import EnvContext
from ray.rllib.env.multi_agent_env import MultiAgentEnv
from ray.rllib.utils.typing import AgentID, MultiAgentDict, PolicyID
from ray.train import RunConfig
from ray.tune import Tuner
from ray.tune.registry import register_env

from environment import CustomUnityMultiAgentEnv
from utils import create_unity_env

# Suppress DeprecationWarnings from output
os.environ["PYTHONWARNINGS"] = "ignore::DeprecationWarning"


def main():
    """
    Main function to initialize the Unity environment and start the training process.

    Returns:
        None
    """
    # Initialize Ray
    ray.init(ignore_reinit_error=True, num_cpus=4)

    unity_env_handle = create_unity_env(
        file_name="../libReplicantDriveSim.app",  # Path to your Unity executable
        worker_id=0,
        base_port=5004,
        no_graphics=False,
    )

    # Register the environment with RLlib
    env_name = "CustomUnityMultiAgentEnv"
    register_env(
        env_name,
        lambda config: CustomUnityMultiAgentEnv(
            config, unity_env_handle=unity_env_handle
        ),
    )

    # Define the configuration for the PPO algorithm
    config = PPO.get_default_config()
    config = config.environment(
        env=env_name,
        env_config={"initial_agent_count": 2, "unity_env_handle": unity_env_handle},
    )
    config = config.framework("torch")
    config = config.resources(num_gpus=0)

    # Multi-agent configuration
    config = config.multi_agent(
        policies={
            # Define our policies here (e.g., "default_policy")
            "shared_policy": (
                None,
                gym.spaces.Box(-np.inf, np.inf, (19,)),
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
        policy_mapping_fn=lambda agent_id, episode, worker, **kwargs: "shared_policy",
    )

    # Rollout configuration
    # Setting num_env_runners=0 will only create the local worker, in which case both sample collection
    # and training will be done by the local worker. On the other hand, setting num_env_runners=5
    # will create the local worker (responsible for training updates)
    # and 5 remote workers (responsible for sample collection).
    config = config.rollouts(
        num_rollout_workers=2,
        num_envs_per_worker=1,
        rollout_fragment_length=200,
        batch_mode="truncate_episodes",
    )

    # Training configuration
    config = config.training(
        train_batch_size=4000,
        sgd_minibatch_size=128,
        num_sgd_iter=30,
        lr=3e-4,
        gamma=0.99,
        lambda_=0.95,
        clip_param=0.2,
        vf_clip_param=10.0,
        entropy_coeff=0.01,
        kl_coeff=0.5,
        vf_loss_coeff=1.0,
    )

    # Source: https://discuss.ray.io/t/agent-ids-that-are-not-the-names-of-the-agents-in-the-env/6964/3
    config = config.environment(disable_env_checking=True)

    # Set up MLflow logging
    mlflow.set_experiment("MARLExperiment")

    # Initialize PPO trainer
    #    trainer = PPO(config=config)

    # Training loop
    #    for i in range(1):
    # Generate a new agent count
    #        new_agent_count = np.random.randint(
    #            1, 5
    #        )  # Choose a random number of agents between 1 and 10
    #        print(f"Episode {i} number of agents: ", new_agent_count)

    # Update the env_config with the new agent count
    #        trainer.config["env_config"]["initial_agent_count"] = new_agent_count

    # Update all worker environments
    #        def update_env(env):
    #            if hasattr(env, 'reset') and callable(env.reset):
    #                env.reset(options={"new_agent_count": new_agent_count})

    #        trainer.workers.foreach_worker(lambda worker: worker.foreach_env(update_env))

    # Train for one iteration
    #        result = trainer.train()
    #        print(f"Iteration {i}: reward_mean={result['episode_reward_mean']}")

    #        if i % 10 == 0:  # Save a checkpoint every 10 iterations
    #            checkpoint = trainer.save()
    #            print(f"Checkpoint saved at {checkpoint}")

    results = tune.run(
        PPO,
        config=config,
        checkpoint_freq=5,  # Set checkpoint frequency here
        num_samples=1,  # Number of times to repeat the experiment
        max_failures=1,  # Maximum number of failures before stopping the experiment
        verbose=1,  # Verbosity level for logging
        callbacks=[
            MLflowLoggerCallback(
                experiment_name="MARLExperiment",
                tracking_uri=mlflow.get_tracking_uri(),
                # tracking_uri="file://" + os.path.abspath("./mlruns"),
                save_artifact=True,
            )
        ],
        stop={"training_iteration": 1},  # storage_path=
        local_dir="./ray_results",  # default ~/ray_results
        name="PPO_Highway_Experiment",
    )

    # Print the results dictionary of the training to inspect the structure
    print("Training results: ", results)

    # Get the best trial based on a metric (e.g., 'episode_reward_mean')
    best_trial = results.get_best_trial("episode_reward_mean", mode="max")

    if best_trial:
        best_checkpoint = best_trial.checkpoint

        if best_checkpoint:
            # Load the model from the best checkpoint
            best_model = PPO.from_checkpoint(best_checkpoint)

            # Register the model
            with mlflow.start_run(run_name="model_registration") as run:
                # Log model
                mlflow.pytorch.log_model(
                    best_model.get_policy().model,
                    "ppo_model",
                    registered_model_name="PPO_Highway_Model",
                )
                print(f"Model registered with run ID: {run.info.run_id}")
        else:
            print("No best trial found. Model registration skipped.")

    # Make sure to close the Unity environment at the end
    ray.get(unity_env_handle.close.remote())

    ray.shutdown()


if __name__ == "__main__":
    main()
