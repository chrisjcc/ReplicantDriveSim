import sys
import time  # Import time module for throttling
import socket
import json
import signal
import threading
import logging

from simulacrum import HighwayEnv


HOST = '127.0.0.1' #'192.168.178.88' #'127.0.0.1'
PORT = 12350

# Setup logging
logging.basicConfig(
    level=logging.DEBUG, # level=logging.INFO,
    format='%(asctime)s - %(levelname)s - %(message)s'
)

def main():
    configs = {
        "progress": True,
        "collision": True,
        "safety_distance": True,
        "max_episode_steps": 100,
        "num_agents": 2,
        "render_mode": None #"human"  # Set to False to disable rendering for now
    }

    def signal_handler(sig, frame):
        logging.info('Shutting down server...')
        try:
            sock.close()
        except:
            pass
        sys.exit(0)

    env = HighwayEnv(configs=configs)  # Initialize the environment

    sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)

    try:
        sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        sock.bind((HOST, PORT))
        sock.listen(1)

        logging.info(f"Server listening on {HOST}:{PORT}")

        signal.signal(signal.SIGINT, signal_handler)
        signal.signal(signal.SIGTERM, signal_handler)

        while True:
            conn, addr = sock.accept()

            logging.info(f"Accepted connection from {addr}")

            threading.Thread(target=handle_client, args=(conn, env)).start()

    except Exception as e:
        logging.error(f"Failed to bind to port {PORT}: {e}", exc_info=True)
    finally:
        env.close()
        sock.close()

def handle_client(conn, env):
    try:
        conn.settimeout(5)  # Set a timeout of 5 seconds
        command = conn.recv(1024).decode() # Increase buffer size from 1024

        logging.info(f"Received command: {command}")

        command = command.strip()  # Strip any extraneous whitespace/newline characters

        if command == "step":
            observations, infos = env.reset()
            render_data = prepare_render_data(infos)

            # Measure memory size of render_data
            size_bytes = sys.getsizeof(render_data)
            print(f"Memory size of render_data: {size_bytes} bytes")

            try:
                message = json.dumps(render_data)
                conn.sendall(message.encode('utf-8'))
            except BrokenPipeError:
                logging.info("Client disconnected")
                return

            logging.info("Sent render data to Unity")

            done = False
            step_count = 1

            while not done:
                print(f"Step count: {step_count}")
                actions = {agent: {"discrete": env.high_level_action_space.sample(), "continuous": env.low_level_action_space.sample()} for agent in env.agents}

                observations, rewards, terminateds, truncateds, infos = env.step(actions)
                render_data = prepare_render_data(infos)

                try:
                    message = json.dumps(render_data)
                    conn.sendall(message.encode('utf-8'))
                except BrokenPipeError:
                    logging.info("Client disconnected")
                    break

                logging.info("Sent render data to Unity")

                for agent in env.agents:
                    total_reward = rewards[agent]
                    print(f"Rewards for {agent}: {total_reward}")
                    print(f"Reward components for {agent}: {infos[agent]}")

                done = terminateds.get("__all__", False) or truncateds.get("__all__", False)

                step_count += 1

                # Introduce a small delay to throttle data flow
                time.sleep(0.1)  # Adjust as needed (default: 0.1)

        env.close()

    except Exception as e:
        logging.error(f"Error handling client: {e}", exc_info=True)
    finally:
        conn.close()

def prepare_render_data(observations):
    render_data = {"agents": []}
    for agent, attributes in observations.items():
        render_data["agents"].append({
            "agent_id": agent,
            "position": attributes["position"].tolist(),  # Convert ndarray to list
            "orientation": attributes["orientation"].tolist()  # Convert ndarray to list
        })
    return render_data

if __name__ == "__main__":
    main()
