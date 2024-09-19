import os
import yaml
import yamale

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
from ray import train
from ray import tune
from ray.air.integrations.mlflow import MLflowLoggerCallback
from ray.rllib.algorithms.ppo import PPO
from ray.rllib.env.env_context import EnvContext
from ray.rllib.env.multi_agent_env import MultiAgentEnv
from ray.rllib.policy.policy import PolicySpec
from ray.rllib.policy.policy import Policy
from ray.rllib.utils.typing import AgentID, MultiAgentDict, PolicyID
from ray.train import RunConfig
from ray.tune import Tuner
from ray.tune.registry import register_env

from environment import CustomUnityMultiAgentEnv
from utils import create_unity_env

# Suppress DeprecationWarnings from output
os.environ["PYTHONWARNINGS"] = "ignore::DeprecationWarning"

gym.logger.set_level(gym.logger.DISABLED)

def validate_yaml_schema(data_path, schema_path):
    try:
        schema = yamale.make_schema(schema_path)
        data = yamale.make_data(data_path)
        yamale.validate(schema, data)
        print("YAML is valid according to the schema!")
        return True
    except yamale.YamaleError as e:
        print(f"Validation failed: {e}")
        return False

def main():
    """
    Main function to initialize the Unity environment and start the training process.

    Returns:
        None
    """

    # Determine the current directory where the script is running
    current_dir = os.path.dirname(os.path.abspath(__file__))

    # Set YAML files paths
    config_path = os.path.join(current_dir, "config.yaml")
    config_schema_path = os.path.join(current_dir, 'config_schema.yaml')

    # Validate YAML file
    validate_yaml_schema(config_path, config_schema_path)

    # Load configuration from YAML file
    with open(config_path, "r") as config_file:
        config_data = yaml.safe_load(config_file)

    # Set up MLflow logging
    mlflow.set_experiment(config_data["mlflow"]["experiment_name"])

    # Initialize Ray
    ray.init(ignore_reinit_error=True, num_cpus=config_data["ray"]["num_cpus"])

    # Get the base directory by moving up one level (assuming the script is in 'rl' folder)
    base_dir = os.path.dirname(current_dir)

    # Construct the full path to the Unity executable
    unity_executable_path = os.path.join(base_dir, "libReplicantDriveSim.app")

    # Create Unity environment
    unity_env_handle = create_unity_env(
        file_name=unity_executable_path,
        worker_id=0,
        base_port=config_data["unity_env"]["base_port"],
        no_graphics=config_data["unity_env"]["no_graphics"],
    )

    # Define environment creator function
    def env_creator(env_config):
        return CustomUnityMultiAgentEnv(env_config)

    # Register the environment with RLlib
    env_name = "CustomUnityMultiAgentEnv"
    register_env(env_name, env_creator)

    # Create an instance of the environment for configuration
    env_config = {
        "initial_agent_count": config_data["env_config"]["initial_agent_count"],
        "unity_env_handle": unity_env_handle,
        "episode_horizon": config_data["env_config"]["episode_horizon"],
    }
    env = CustomUnityMultiAgentEnv(config=env_config, unity_env_handle=unity_env_handle)

    # Define the configuration for the PPO algorithm
    config = PPO.get_default_config()
    config = config.environment(env=env_name, env_config=env_config)
    config = config.framework(config_data["ppo_config"]["framework"])
    config = config.resources(num_gpus=config_data["ppo_config"]["num_gpus"])

    def policy_mapping_fn(agent_id, episode, worker, **kwargs):
        return "shared_policy"

    # Multi-agent configuration
    config = config.multi_agent(
        policies={
            # Define our policies here (e.g., "default_policy")
            "shared_policy": PolicySpec(
                policy_class=None,  # Use default PPOTorchPolicy policy
                observation_space=env.single_agent_obs_space,
                action_space=env.single_agent_action_space,
                config={}
            )
        },
        policy_mapping_fn=policy_mapping_fn,
    )

    # Rollout configuration
    # Setting num_env_runners=0 will only create the local worker, in which case both sample collection
    # and training will be done by the local worker. On the other hand, setting num_env_runners=5
    # will create the local worker (responsible for training updates)
    # and 5 remote workers (responsible for sample collection).
    config = config.rollouts(
        num_rollout_workers=config_data["rollouts"]["num_rollout_workers"],
        num_envs_per_worker=config_data["rollouts"]["num_envs_per_worker"],
        rollout_fragment_length=config_data["rollouts"]["rollout_fragment_length"],
        batch_mode=config_data["rollouts"]["batch_mode"],
    )

    # Training configuration
    config = config.training(
        train_batch_size=config_data["training"]["train_batch_size"],
        sgd_minibatch_size=config_data["training"]["sgd_minibatch_size"],
        num_sgd_iter=config_data["training"]["num_sgd_iter"],
        lr=config_data["training"]["lr"],
        gamma=config_data["training"]["gamma"],
        lambda_=config_data["training"]["lambda"],
        clip_param=config_data["training"]["clip_param"],
        vf_clip_param=config_data["training"]["vf_clip_param"],
        entropy_coeff=config_data["training"]["entropy_coeff"],
        kl_coeff=config_data["training"]["kl_coeff"],
        vf_loss_coeff=config_data["training"]["vf_loss_coeff"],
    )

    # Environment configuration
    # Source: https://discuss.ray.io/t/agent-ids-that-are-not-the-names-of-the-agents-in-the-env/6964/3
    config = config.environment(disable_env_checking=config_data["environment"]["disable_env_checking"])

    config = config.checkpointing(
        export_native_model_files=True # This option will save the model in its native format .pb for PyTorch alongside the checkpoints.
    )
    config = config.debugging(log_level='ERROR') # INFO, DEBUG, ERROR, WARN

    tags = { "user_name" : "chrisjcc",
             "git_commit_hash" : "c15d456f12bb54180b25dfa8e0d2268694dd1a9e"  # git rev-parse HEAD
    }

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
        checkpoint_config=train.CheckpointConfig(
            num_to_keep=1,           # Number of checkpoints to keep
            checkpoint_frequency=1,  # Frequency of checkpointing
            checkpoint_at_end=True,  # Save a checkpoint at the end of training
        ),
        num_samples=1,   # Number of times to repeat the experiment
        max_failures=1,  # Maximum number of failures before stopping the experiment
        verbose=1,       # Verbosity level for logging
        callbacks=[
            MLflowLoggerCallback(
                experiment_name=config_data["mlflow"]["experiment_name"],
                tracking_uri=mlflow.get_tracking_uri(), # file://./mlruns
                registry_uri=None,
                save_artifact=True,
                tags=tags,
            )
        ],
        #resume="AUTO",
        stop={"training_iteration": 1},
        #storage_path=os.path.abspath("./mlruns"), # default ~/ray_results
        local_dir="./ray_results",
        name="PPO_Highway_Experiment",
    )

    # Print the results dictionary of the training to inspect the structure
    print("Training results: ", results)

    # Get the best trial based on a metric (e.g., 'episode_reward_mean')
    best_trial = results.get_best_trial("episode_reward_mean", mode="max")

    if results.trials:
        checkpoint = results.trials[0].checkpoint # best_trial.checkpoint

        # Load the model from the best checkpoint
        if best_trial and best_trial.checkpoint or last_checkpoint:
            # Load the model from the last checkpoint
            policy = Policy.from_checkpoint(checkpoint)["shared_policy"]

            # The most recent experiment and run will be the first one
            experiment = mlflow.get_experiment_by_name(config_data["mlflow"]["experiment_name"])
            experiment_id = experiment.experiment_id

            runs = mlflow.search_runs(experiment_ids=[experiment.experiment_id])
            run_id = runs.iloc[0].run_id
            run_name = f"PPO_CustomUnityMultiAgentEnv_{results.trials[0].trial_id}"

            # Register the model, resume a run with the specified run ID
            with mlflow.start_run(run_name=run_name, experiment_id=experiment_id, run_id=run_id, tags=tags) as run:
                # Log model
                mlflow.pytorch.log_model(
                    policy.model,
                    "ppo_model",
                    registered_model_name="PPO_Highway_Model",
                )
                print(f"Model registered with run ID: {run.info.run_id}")
        else:
            print("No checkpoint found. Model registration skipped.")
    else:
        print("No trials completed. Model registration skipped.")

    # Make sure to close the Unity environment at the end
    ray.get(unity_env_handle.close.remote())

    ray.shutdown()


if __name__ == "__main__":
    main()
