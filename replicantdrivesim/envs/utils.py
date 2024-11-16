import uuid
from typing import Optional

from mlagents_envs.side_channel.incoming_message import IncomingMessage
from mlagents_envs.side_channel.outgoing_message import OutgoingMessage
from mlagents_envs.side_channel.side_channel import SideChannel


class CustomSideChannel(SideChannel):
    """
    A custom side channel for sending and receiving named field values in Unity ML-Agents.
    """

    def __init__(self) -> None:
        """
        Initializes the CustomSideChannel with a predefined UUID.
        Also initializes a dictionary to store received field values.
        """
        # Initialize the side channel with a unique identifier (UUID).
        super().__init__(uuid.UUID("621f0a70-4f87-11ea-a6bf-784f4387d1f8"))

        # A dictionary to store field values received from Unity.
        self.received_values: dict[str, float] = {}

    def send_field_value(self, field_name: str, value: float) -> None:
        """
        Sends a field name and its corresponding value to Unity.

        Args:
            field_name (str): The name of the field.
            value (float): The value to be sent.
        """
        # Create an outgoing message and write the field name and value to it.
        msg = OutgoingMessage()
        msg.write_string(field_name)
        msg.write_float32(value)

        # Queue the message to be sent.
        super().queue_message_to_send(msg)

    def on_message_received(self, msg: IncomingMessage) -> None:
        """
        Handles the reception of messages from Unity by extracting the field name and value.

        Args:
            msg (IncomingMessage): The message received from Unity.
        """
        # Read the field name and value from the incoming message.
        field_name = msg.read_string()
        value = msg.read_float32()

        # Store the received field name and value in the dictionary.
        self.received_values[field_name] = value

    def get_field_value(self, field_name: str) -> Optional[float]:
        """
        Retrieves the value for a given field name.

        Args:
            field_name (str): The name of the field.

        Returns:
            Optional[float]: The value associated with the field name, or None if the field doesn't exist.
        """
        return self.received_values.get(field_name)
