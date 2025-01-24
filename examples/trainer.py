import os

import logging
import gymnasium as gym
import mlflow
import numpy as np
import ray
import yamale
import yaml

import replicantdrivesim

from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.exception import UnityCommunicatorStoppedException
from mlflow.models.signature import ModelSignature
from mlflow.types.schema import Schema, TensorSpec

from ray import train, tune
from ray.air.integrations.mlflow import MLflowLoggerCallback
from ray.rllib.algorithms.ppo import PPO
from ray.rllib.env.env_context import EnvContext
from ray.rllib.env.multi_agent_env import MultiAgentEnv
from ray.rllib.policy.policy import Policy, PolicySpec
from ray.rllib.utils.typing import AgentID, MultiAgentDict, PolicyID
from ray.train import RunConfig
from ray.tune import Tuner
from ray.tune.registry import register_env


# Suppress DeprecationWarnings from output
os.environ["PYTHONWARNINGS"] = "ignore::DeprecationWarning"

# Set it for the current notebook environment
version = "0"  # Default "1"
os.environ["RAY_AIR_NEW_OUTPUT"] = version
os.environ["RAY_AIR_RICH_LAYOUT"] = version

# Set the logging level to DISABLED (which is equivalent to CRITICAL in the logging module)
logging.getLogger('gymnasium').setLevel(logging.CRITICAL)


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


# Define environment creator function
def env_creator(env_config):
    return replicantdrivesim.CustomUnityMultiAgentEnv(env_config)

def policy_mapping_fn(agent_id, episode, worker, **kwargs):
    return "shared_policy"


def register_model(policy, model_name, run_name, experiment_id, run_id, model_signature):
    with mlflow.start_run(run_name=run_name, experiment_id=experiment_id, run_id=run_id) as run:
        mlflow.pytorch.log_model(
            pytorch_model=policy.model,
            artifact_path="ppo_model",
            registered_model_name=model_name,
            signature=model_signature,
        )
        print(f"Model {model_name} registered with run ID: {run.info.run_id}")


def main():
    """
    Main function to initialize the Unity environment and start the training process.

    Returns:
        None
    """
    try:
        # Set YAML files paths
        config_path = os.path.join("examples", "configs", "config.yaml")
        config_schema_path = os.path.join("replicantdrivesim", "configs", "config_schema.yaml")

        # Validate YAML file
        validate_yaml_schema(config_path, config_schema_path)

        # Load configuration from YAML file
        with open(config_path, "r") as config_file:
            config_data = yaml.safe_load(config_file)

        # Set up MLflow logging
        mlflow.set_experiment(config_data["mlflow"]["experiment_name"])

        # Initialize Ray
        ray.init(
            ignore_reinit_error=True,
            num_cpus=config_data["ray"]["num_cpus"],
            runtime_env={
                "env_vars": {
                    "RAY_AIR_NEW_OUTPUT": version,
                    "RAY_AIR_RICH_LAYOUT": version,
                }
            },
        )

        # Register the environment with RLlib
        env_name = "CustomUnityMultiAgentEnv"
        register_env(env_name, env_creator)

        # Define the configuration for the PPO algorithm
        env = replicantdrivesim.make("replicantdrivesim-v0", config=config_data)

        config = PPO.get_default_config()
        config = config.environment(
            env=env_name,
            env_config=config_data,
            disable_env_checking=config_data["environment"][
                "disable_env_checking"
            ],  # Source: https://discuss.ray.io/t/agent-ids-that-are-not-the-names-of-the-agents-in-the-env/6964/3
        )
        config = config.framework(config_data["ppo_config"]["framework"])
        config = config.resources(num_gpus=config_data["ppo_config"]["num_gpus"])

        # Multi-agent configuration
        config = config.multi_agent(
            policies={
                "shared_policy": PolicySpec(
                    policy_class=None,
                    observation_space=env.single_agent_obs_space,
                    action_space=env.single_agent_action_space,
                    config={},
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
        config = config.checkpointing(export_native_model_files=True)
        config = config.debugging(log_level="ERROR")

        tags = {
            "user_name": "chrisjcc",
            "git_commit_hash": "c15d456f12bb54180b25dfa8e0d2268694dd1a9e",
        }

        tuner = Tuner(
            PPO,
            param_space=config,
            run_config=RunConfig(
                name="PPO_Highway_Experiment",
                local_dir="./ray_results",
                checkpoint_config=train.CheckpointConfig(
                    num_to_keep=1,
                    checkpoint_frequency=100,
                    checkpoint_at_end=True,
                ),
                callbacks=[
                    MLflowLoggerCallback(
                        experiment_name=config_data["mlflow"]["experiment_name"],
                        tracking_uri=mlflow.get_tracking_uri(),
                        registry_uri=None,
                        save_artifact=True,
                        tags=tags,
                    )
                ],
                stop={
                    "training_iteration": config_data["stop"]["training_iteration"]
                },  # Stop after 100 iterations, each iteration can consist of multiple episodes, depending on how the rollout and batch sizes are configured.
            ),
            tune_config=tune.TuneConfig(
                num_samples=1,
                max_concurrent_trials=1,
                metric="episode_reward_mean",
                mode="max",
            ),
        )

        results = tuner.fit()


        # Print the results dictionary of the training to inspect the structure
        print(f"Training results: {results}")

        # Check if results is not empty
        if results:
            experiment = mlflow.get_experiment_by_name(config_data["mlflow"]["experiment_name"])
            experiment_id = experiment.experiment_id
            runs = mlflow.search_runs(experiment_ids=[experiment.experiment_id])
            run_id = runs.iloc[0].run_id

            input_schema = Schema([
                TensorSpec(np.dtype(np.float32), (-1, env.size_of_single_agent_obs), agent_id)
                for agent_id in env._agent_ids
            ])
            model_signature = ModelSignature(inputs=input_schema)

            try:
                # Model 1: Based on the best result (scope set to `last`)
                best_result = results.get_best_result(metric="episode_reward_mean", mode="max", scope='last')

                if best_result and best_result.checkpoint:
                    policy = Policy.from_checkpoint(best_result.checkpoint)["shared_policy"]
                    register_model(policy, "PPO_Highway_Model_BestResult_scope_last", "PPO_CustomUnityMultiAgentEnv_BestResult", experiment_id, run_id, model_signature)

                    print(f"Best config: {best_result.config}")
                    print(f"Best metrics: {best_result.metrics}")
                    print(f"Best result last checkpoint path: {best_result.checkpoint}")

                # Model 2: Based on the best result (scope set to `avg`)
                best_result = results.get_best_result(metric="episode_reward_mean", mode="max", scope='avg')

                if best_result and best_result.checkpoint:
                    policy = Policy.from_checkpoint(best_result.checkpoint)["shared_policy"]
                    register_model(policy, "PPO_Highway_Model_BestResult_scope_avg", "PPO_CustomUnityMultiAgentEnv_BestResult", experiment_id, run_id, model_signature)

                    print(f"Best config: {best_result.config}")
                    print(f"Best metrics: {best_result.metrics}")
                    print(f"Best result avg checkpoint path: {best_result.checkpoint}")

                # Model 3: Based on the best result (scope set to `all`)
                best_result = results.get_best_result(metric="episode_reward_mean", mode="max", scope='last')

                if best_result and best_result.checkpoint:
                    policy = Policy.from_checkpoint(best_result.checkpoint)["shared_policy"]
                    register_model(policy, "PPO_Highway_Model_BestResult_scope_all", "PPO_CustomUnityMultiAgentEnv_BestResult", experiment_id, run_id, model_signature)

                    print(f"Best config: {best_result.config}")
                    print(f"Best metrics: {best_result.metrics}")
                    print(f"Best result all checkpoint path: {best_result.checkpoint}")

                # Model 4: Based on the last checkpoint of all trials
                all_checkpoints = [result.checkpoint for result in results if result.checkpoint]

                if all_checkpoints:
                    last_checkpoint = all_checkpoints[-1]
                    policy = Policy.from_checkpoint(last_checkpoint)["shared_policy"]
                    register_model(policy, "PPO_Highway_Model_LastCheckpoint", "PPO_CustomUnityMultiAgentEnv_LastCheckpoint", experiment_id, run_id, model_signature)

                    print(f"Last checkpoint path: {last_checkpoint}")

            except Exception as e:
                print(f"Error loading policy or registering models: {e}")
        else:
            print("No results returned from tuner.fit(). Model registration skipped.")

    except UnityCommunicatorStoppedException:
        print("Unity environment was closed. Terminating training gracefully.")
    except Exception as e:
        print(f"An unexpected error occurred during training: {e}")
    finally:
        # Cleanup
        if 'env' in locals():
            try:
                env.close()
            except Exception as e:
                print(f"Error closing environment: {e}")

        try:
            # Make sure to close the Unity environment at the end
            ray.shutdown()
        except Exception as e:
            print(f"Error shutting down Ray: {e}")

        print("Training process completed.")


if __name__ == "__main__":
    main()
