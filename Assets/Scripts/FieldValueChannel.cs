using System;
using Unity.MLAgents.SideChannels;
using UnityEngine;

namespace CustomSideChannel
{
    /// <summary>
    /// Custom side channel to send and receive field values between Unity and external environments.
    /// </summary>
    public class FieldValueChannel : SideChannel
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FieldValueChannel"/> class.
        /// Allows for injecting a custom Guid. If no Guid is provided, it defaults to a predefined value.
        /// </summary>
        /// <param name="guid">Optional custom Guid for the side channel.</param>
        public FieldValueChannel(Guid? guid = null) : base()
        {
            // Use the provided Guid if available, otherwise use the default
            ChannelId = guid ?? new Guid("621f0a70-4f87-11ea-a6bf-784f4387d1f8");
        }

        /// <summary>
        /// Sends a field value through the side channel.
        /// </summary>
        /// <param name="fieldName">The name of the field.</param>
        /// <param name="value">The float value to be sent.</param>
        public void SendFieldValue(string fieldName, float value)
        {
            if (string.IsNullOrEmpty(fieldName))
            {
                Debug.LogWarning("Field name cannot be null or empty.");
                return;
            }

            using (var msgOut = new OutgoingMessage())
            {
                try
                {
                    msgOut.WriteString(fieldName);
                    msgOut.WriteFloat32(value);
                    QueueMessageToSend(msgOut);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error sending field value: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Handles the message received from the side channel.
        /// Note: This method is currently a placeholder and does not handle incoming messages.
        /// </summary>
        /// <param name="msg">The incoming message.</param>
        protected override void OnMessageReceived(IncomingMessage msg)
        {
            // Currently, there is no logic for handling incoming messages.
            // This can be extended as needed.
        }
    }
}