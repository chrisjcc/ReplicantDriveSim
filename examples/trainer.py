import json
import logging
import os

import gymnasium as gym
import mlflow
import numpy as np

# Suppress DeprecationWarnings from output
import warnings
warnings.filterwarnings("ignore", category=DeprecationWarning)
warnings.filterwarnings("ignore", message="`AlgorithmConfig.num_.*` has been deprecated")

os.environ["PYTHONWARNINGS"] = "ignore::DeprecationWarning"
os.environ["RAY_DEDUP_LOGS"] = "0"
# Set it for the current notebook environment
version = "0" # "2"  # Default "1"
os.environ["RAY_AIR_NEW_OUTPUT"] = version
os.environ["RAY_AIR_RICH_LAYOUT"] = version

# Silence specific Ray loggers that emit deprecation noise
logging.getLogger("ray.rllib.algorithms.algorithm_config").setLevel(logging.ERROR)
logging.getLogger("ray.tune.tune").setLevel(logging.ERROR)


import ray
import yamale
import yaml
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.exception import UnityCommunicatorStoppedException
import hydra
from omegaconf import DictConfig, OmegaConf
from mlflow.models.signature import ModelSignature
from mlflow.types.schema import Schema, TensorSpec
from ray import train, tune
from ray.air.integrations.mlflow import MLflowLoggerCallback
from ray.air.integrations.wandb import WandbLoggerCallback
from ray.rllib.algorithms.ppo import PPO
from ray.rllib.env.env_context import EnvContext
from ray.rllib.env.multi_agent_env import MultiAgentEnv
from ray.rllib.policy.policy import Policy, PolicySpec
from ray.rllib.utils.typing import AgentID, MultiAgentDict, PolicyID
from ray.train import RunConfig
from ray.tune import Tuner
from ray.tune.registry import register_env

import replicantdrivesim

# Set all loggers to CRITICAL or disable them to effectively silence logging output
logging_level = logging.DEBUG

gym_logger = logging.getLogger("gymnasium")
gym_logger.setLevel(logging_level)

ray_logger = logging.getLogger("ray")
ray_logger.setLevel(logging_level)

ray_rllib_logger = logging.getLogger("ray.rllib")
ray_rllib_logger.setLevel(logging_level)


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


def register_model(
    policy, model_name, run_name, experiment_id, run_id, model_signature
):
    with mlflow.start_run(
        run_name=run_name, experiment_id=experiment_id, run_id=run_id
    ) as run:
        mlflow.pytorch.log_model(
            pytorch_model=policy.model,
            artifact_path="ppo_model",
            registered_model_name=model_name,
            signature=model_signature,
        )
        print(f"Model {model_name} registered with run ID: {run.info.run_id}")


@hydra.main(version_base=None, config_path="conf", config_name="config")
def main(cfg: DictConfig):
    """
    Main function to initialize the Unity environment and start the training process.
    """
    try:
        # Convert OmegaConf to standard dict for RLlib compatibility
        config_data = OmegaConf.to_container(cfg, resolve=True)

        # Set YAML files paths
        config_schema_path = os.path.join(
            "replicantdrivesim", "configs", "config_schema.yaml"
        )

        # Validate configuration (optional, as Hydra handles most of this)
        # Note: We temporarily write to a temp file for yamale if we really want to keep it,
        # but for now let's just use the config_data directly.
        # print("Configuration loaded via Hydra.")

        # Set up MLflow logging
        mlflow.set_experiment(config_data["mlflow"]["experiment_name"])

        # Initialize Ray
        ray.init(
            ignore_reinit_error=True,
            num_cpus=config_data["ray_init"]["num_cpus"],
            num_gpus=config_data["ray_init"]["num_gpus"],
            runtime_env={
                "env_vars": {
                    "RAY_AIR_NEW_OUTPUT": version,
                    "RAY_AIR_RICH_LAYOUT": version,
                }
            },
            log_to_driver=False,
            logging_level="INFO",
            _temp_dir="/tmp/ray_tmp",  # Redirect Ray's temporary directory to use a different directory with more available space
            _system_config={
                "object_spilling_config":  None
            },  # Disable Object Spilling if disk space is a bottleneck, and you have sufficient memory, disable object spilling
        )

        # Register the environment with RLlib
        env_name = "CustomUnityMultiAgentEnv"
        register_env(env_name, env_creator)

        # Define the configuration for the PPO algorithm
        env = replicantdrivesim.make("replicantdrivesim-v0", config=config_data)

        # Get the default PPO configuration
        config = PPO.get_default_config()

        # Update the configuration using modern method-style API

        config = config.environment(
            env=env_name,
            env_config=config_data,
        )
        config = config.framework(config_data["ppo_config"]["framework"])
        config = config.resources(
            num_gpus=0
        )

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
        config = config.env_runners(
            num_env_runners=config_data["env_runners"]["num_env_runners"],
            num_envs_per_env_runner=config_data["env_runners"]["num_envs_per_env_runner"],
            num_cpus_per_env_runner=config_data["env_runners"]["num_cpus_per_env_runner"],
            num_gpus_per_env_runner=config_data["env_runners"]["num_gpus_per_env_runner"],
            rollout_fragment_length=config_data["env_runners"]["rollout_fragment_length"],
            batch_mode=config_data["env_runners"]["batch_mode"],
        )

        # Training configuration
        config = config.training(
            train_batch_size=config_data["training"]["train_batch_size"],
            num_sgd_iter=config_data["training"]["num_epochs"],
            lr=config_data["training"]["lr"],
            gamma=config_data["training"]["gamma"],
            lambda_=config_data["training"]["lambda"],
            clip_param=config_data["training"]["clip_param"],
            vf_clip_param=config_data["training"]["vf_clip_param"],
            entropy_coeff=config_data["training"]["entropy_coeff"],
            kl_coeff=config_data["training"]["kl_coeff"],
            vf_loss_coeff=config_data["training"]["vf_loss_coeff"],
        )

        # config = config.evaluation(evaluation_num_env_runners=1)
        config = config.checkpointing(export_native_model_files=True)
        config = config.debugging(log_level="ERROR")
        config = config.api_stack(
            enable_rl_module_and_learner=False,
            enable_env_runner_and_connector_v2=False
        ) # To not run PPO on the new API stack

        tags = {
            "user_name": "chrisjcc",
            "git_commit_hash": "c15d456f12bb54180b25dfa8e0d2268694dd1a9e",
        }

        # Resolve the user directory
        user_home = os.path.expanduser("~")
        storage_path = f"file://{user_home}/ray_results"

        tuner = Tuner(
            PPO,
            param_space=config,
            run_config=RunConfig(
                name="PPO_Highway_Experiment",
                storage_path=storage_path,
                checkpoint_config=train.CheckpointConfig(
                    num_to_keep=config_data["training"]["num_to_keep"], # If num_to_keep is too low, snapshots are triggered too often
                    checkpoint_frequency=config_data["training"]["checkpoint_freq"],
                    checkpoint_at_end=config_data["training"]["checkpoint_at_end"],
                ),
                callbacks=[
                    callback for callback in [
                        MLflowLoggerCallback(
                            experiment_name=config_data["mlflow"]["experiment_name"],
                            tracking_uri=mlflow.get_tracking_uri(),
                            registry_uri=None,
                            save_artifact=True,
                            tags=tags,
                        ) if config_data.get("mlflow", {}).get("enabled", True) else None,
                        WandbLoggerCallback(
                            project=config_data["wandb"]["project"],
                            entity=config_data["wandb"]["entity"],
                            log_config=True,
                        ) if config_data.get("wandb", {}).get("enabled", False) else None,
                    ] if callback is not None
                ],
                stop={
                    "training_iteration": config_data["stop"]["training_iteration"]
                },  # Stop after 100 iterations, each iteration can consist of multiple episodes, depending on how the rollout and batch sizes are configured.
                verbose=1,  # Suppresses most output
            ),
            # No need for tune.TuneConfig since we're not tuning hyperparameters
            #tune_config=tune.TuneConfig(
            #    num_samples=1,
            #    max_concurrent_trials=1,
            #    metric="episode_reward_mean",
            #    mode="max",
            #),
        )

        results = tuner.fit()

        # Print the results dictionary of the training to inspect the structure
        print(f"Training results: {results}")

        # Check if results is not empty
        if results:
            experiment = mlflow.get_experiment_by_name(
                config_data["mlflow"]["experiment_name"]
            )
            experiment_id = experiment.experiment_id
            runs = mlflow.search_runs(experiment_ids=[experiment.experiment_id])
            run_id = runs.iloc[0].run_id

            # Use env.agents which contains the list of active agent IDs
            # Add fallback to env.possible_agents if agents list is empty
            agent_ids = env.agents if env.agents else env.possible_agents
            
            if not agent_ids:
                raise ValueError("No agents found in environment - cannot create model signature")
            
            input_schema = Schema(
                [
                    TensorSpec(
                        np.dtype(np.float32),
                        (-1, env.size_of_single_agent_obs),
                        agent_id,
                    )
                    for agent_id in agent_ids
                ]
            )
            model_signature = ModelSignature(inputs=input_schema)

            try:
                # Model 1: Based on the best result (scope set to `last`)
                best_result = results.get_best_result(
                    metric="episode_reward_mean", mode="max", scope="last"
                )

                if best_result and best_result.checkpoint:
                    policy = Policy.from_checkpoint(best_result.checkpoint)[
                        "shared_policy"
                    ]
                    register_model(
                        policy,
                        "PPO_Highway_Model_BestResult_scope_last",
                        "PPO_CustomUnityMultiAgentEnv_BestResult",
                        experiment_id,
                        run_id,
                        model_signature,
                    )

                    print(f"Best config: {best_result.config}")
                    print(f"Best metrics: {best_result.metrics}")
                    print(f"Best result last checkpoint path: {best_result.checkpoint}")

                # Model 2: Based on the best result (scope set to `avg`)
                best_result = results.get_best_result(
                    metric="episode_reward_mean", mode="max", scope="avg"
                )

                if best_result and best_result.checkpoint:
                    policy = Policy.from_checkpoint(best_result.checkpoint)[
                        "shared_policy"
                    ]
                    register_model(
                        policy,
                        "PPO_Highway_Model_BestResult_scope_avg",
                        "PPO_CustomUnityMultiAgentEnv_BestResult",
                        experiment_id,
                        run_id,
                        model_signature,
                    )

                    print(f"Best config: {best_result.config}")
                    print(f"Best metrics: {best_result.metrics}")
                    print(f"Best result avg checkpoint path: {best_result.checkpoint}")

                # Model 3: Based on the best result (scope set to `all`)
                best_result = results.get_best_result(
                    metric="episode_reward_mean", mode="max", scope="last"
                )

                if best_result and best_result.checkpoint:
                    policy = Policy.from_checkpoint(best_result.checkpoint)[
                        "shared_policy"
                    ]
                    register_model(
                        policy,
                        "PPO_Highway_Model_BestResult_scope_all",
                        "PPO_CustomUnityMultiAgentEnv_BestResult",
                        experiment_id,
                        run_id,
                        model_signature,
                    )

                    print(f"Best config: {best_result.config}")
                    print(f"Best metrics: {best_result.metrics}")
                    print(f"Best result all checkpoint path: {best_result.checkpoint}")

                # Model 4: Based on the last checkpoint of all trials
                all_checkpoints = [
                    result.checkpoint for result in results if result.checkpoint
                ]

                if all_checkpoints:
                    last_checkpoint = all_checkpoints[-1]
                    policy = Policy.from_checkpoint(last_checkpoint)["shared_policy"]
                    register_model(
                        policy,
                        "PPO_Highway_Model_LastCheckpoint",
                        "PPO_CustomUnityMultiAgentEnv_LastCheckpoint",
                        experiment_id,
                        run_id,
                        model_signature,
                    )

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
        if "env" in locals():
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
