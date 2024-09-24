import os
import yaml
import yamale

import gymnasium as gym
import mlflow
import numpy as np
from mlagents_envs.base_env import ActionTuple
from mlagents_envs.environment import UnityEnvironment
from mlagents_envs.side_channel.engine_configuration_channel import EngineConfigurationChannel
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
version = "0"
os.environ["RAY_AIR_NEW_OUTPUT"] = version
os.environ['RAY_AIR_RICH_LAYOUT'] = version
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

def env_creator(env_config):
    return CustomUnityMultiAgentEnv(env_config)

def policy_mapping_fn(agent_id, episode, worker, **kwargs):
    return "shared_policy"

def main():
    current_dir = os.path.dirname(os.path.abspath(__file__))
    config_path = os.path.join(current_dir, "config.yaml")
    config_schema_path = os.path.join(current_dir, 'config_schema.yaml')

    validate_yaml_schema(config_path, config_schema_path)

    with open(config_path, "r") as config_file:
        config_data = yaml.safe_load(config_file)

    mlflow.set_experiment(config_data["mlflow"]["experiment_name"])

    ray.init(
        ignore_reinit_error=True,
        num_cpus=config_data["ray"]["num_cpus"],
        runtime_env={
            "env_vars": {
                "RAY_AIR_NEW_OUTPUT": version,
                "RAY_AIR_RICH_LAYOUT": version,
            }
        }
    )

    base_dir = os.path.dirname(current_dir)
    unity_executable_path = os.path.join(base_dir, "libReplicantDriveSim.app")

    unity_env_handle = create_unity_env(
        file_name=unity_executable_path,
        worker_id=0,
        base_port=config_data["unity_env"]["base_port"],
        no_graphics=config_data["unity_env"]["no_graphics"],
    )

    env_name = "CustomUnityMultiAgentEnv"
    register_env(env_name, env_creator)

    env_config = {
        "initial_agent_count": config_data["env_config"]["initial_agent_count"],
        "unity_env_handle": unity_env_handle,
        "episode_horizon": config_data["env_config"]["episode_horizon"],
    }

    env = CustomUnityMultiAgentEnv(config=env_config, unity_env_handle=unity_env_handle)

    config = PPO.get_default_config()
    config = config.environment(env=env_name, env_config=env_config)
    config = config.framework(config_data["ppo_config"]["framework"])
    config = config.resources(num_gpus=config_data["ppo_config"]["num_gpus"])
    config = config.multi_agent(
        policies={
            "shared_policy": PolicySpec(
                policy_class=None,
                observation_space=env.single_agent_obs_space,
                action_space=env.single_agent_action_space,
                config={}
            )
        },
        policy_mapping_fn=policy_mapping_fn,
    )
    config = config.rollouts(
        num_rollout_workers=config_data["rollouts"]["num_rollout_workers"],
        num_envs_per_worker=config_data["rollouts"]["num_envs_per_worker"],
        rollout_fragment_length=config_data["rollouts"]["rollout_fragment_length"],
        batch_mode=config_data["rollouts"]["batch_mode"],
    )
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
    config = config.environment(disable_env_checking=config_data["environment"]["disable_env_checking"])
    config = config.checkpointing(export_native_model_files=True)
    config = config.debugging(log_level='ERROR')

    tags = {
        "user_name": "chrisjcc",
        "git_commit_hash": "c15d456f12bb54180b25dfa8e0d2268694dd1a9e"
    }

    tuner = Tuner(
        PPO,
        param_space=config,
        run_config=RunConfig(
            name="PPO_Highway_Experiment",
            local_dir="./ray_results",
            checkpoint_config=train.CheckpointConfig(
                num_to_keep=1,
                checkpoint_frequency=1,
                #checkpoint_at_end=True,
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
            stop={"training_iteration": config_data["stop"]["training_iteration"]},  # Stop after 100 iterations, each iteration can consist of multiple episodes, depending on how the rollout and batch sizes are configured.
        ),
        tune_config=tune.TuneConfig(
            num_samples=1,
            max_concurrent_trials=1,
            metric="episode_reward_mean",
            mode="max",
        ),
    )

    results = tuner.fit()

    print("Training results: ", results)

    best_result = results.get_best_result(metric="episode_reward_mean", mode="max")
    
    if best_result:
        checkpoint = best_result.checkpoint

        if checkpoint:
            policy = Policy.from_checkpoint(checkpoint)["shared_policy"]
            print(f"TODO: Loaded policy: {policy}")

            policy.export_model(
                #f"{checkpoint}_saved_model",
                "saved_model",
                onnx=None, # OpSet 14-15: These are more recent versions that may include newer features.
            )

            experiment = mlflow.get_experiment_by_name(config_data["mlflow"]["experiment_name"])
            experiment_id = experiment.experiment_id
            runs = mlflow.search_runs(experiment_ids=[experiment.experiment_id])
            run_id = runs.iloc[0].run_id
            run_name = f"PPO_CustomUnityMultiAgentEnv_{best_result.metrics['trial_id']}"

            with mlflow.start_run(run_name=run_name, experiment_id=experiment_id, run_id=run_id, tags=tags) as run:
                mlflow.pytorch.log_model(
                    policy.model,
                    "ppo_model",
                    registered_model_name="PPO_Highway_Model",
                )
                print(f"Model registered with run ID: {run.info.run_id}")
        else:
            print("No checkpoint found. Model registration skipped.")
    else:
        print("No successful trials completed. Model registration skipped.")

    ray.get(unity_env_handle.close.remote())
    ray.shutdown()

if __name__ == "__main__":
    main()
