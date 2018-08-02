using System;
using System.Text;

namespace Aptiv.Messages.CAN
{
    /// <summary>
    /// Isolates attributes necessary for unique identification of a CAN Message.
    /// </summary>
    [Serializable]
    public struct MessageID : IComparable<MessageID>, IEquatable<MessageID>
    {
        private CANMessage cm;

        /// <summary>
        /// Generate a Message ID for the provided CANMessage.
        /// </summary>
        /// <param name="cm">The CAN Message to identify.</param>
        public MessageID(CANMessage cm)
        {
            this.cm = cm;
        }

        /// <summary>
        /// Obtain or set the byte at a specific index of this message id.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public byte this[int index]
        {
            get
            {
                if (index >= Length)
                    throw new IndexOutOfRangeException();
                return cm[index];
            }
            set
            {
                if (index >= Length)
                    throw new IndexOutOfRangeException();
                cm[index] = value;
            }
        }

        /// <summary>
        /// The number of bytes in this CAN Message ID.
        /// </summary>
        public int Length => ((cm[0] >> 7) == 0x01) ? 4 : 2;

        /// <summary>
        /// Obtains the instance of the MessageIDType enum which represents
        /// a more specific type than just the Length.
        /// </summary>
        public MessageIDType IDType => ((this[0] >> 7) == 0x01) ? MessageIDType._29Bit : MessageIDType._11Bit;

        /// <summary>
        /// The part of the Message ID that is limited to the specific
        /// information for identifying this message.
        /// </summary>
        public byte[] Identifier
        {
            get
            {
                if (Length == 2)
                {
                    byte[] id = new byte[2];
                    id[0] = (byte)(this[0] & 0x07);
                    id[1] = this[1];
                    return id;
                }
                else if (Length == 4)
                {
                    byte[] id = new byte[4];
                    id[0] = (byte)(this[0] & 0x1F);
                    id[1] = this[1];
                    id[2] = this[2];
                    id[3] = this[3];
                    return id;
                }
                else return new byte[0];
            }
        }

        /// <summary>
        /// The section of the Message ID identified as the Definition Bits.
        /// </summary>
        public byte CANFrameDefinitionBits
        {
            get
            {
                if (IDType == MessageIDType._11Bit)
                    return (byte)(this[0] & 0xF8);
                else // (IDType == MessageIDType._29Bit)
                    return (byte)(this[0] & 0xE0);
            }
        }

        /// <summary>
        /// True if the message is a "Remote Frame".
        /// </summary>
        public bool IsRemoteFrame => ((CANFrameDefinitionBits >> 5) & 0x1) == 1;
        
        /// <summary>
        /// Set to true if the CAN Frame Definition Bits set to error value.
        /// (optional)
        /// </summary>
        public bool ErrorInFrame => ((CANFrameDefinitionBits >> 6) & 0x1) == 1;

        /// <summary>
        /// Obtains the string representation of this MessageID.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < Length; i++)
                sb.Append(this[i].ToString("X2"));
            return sb.ToString();
        }

        #region --- IComparable Interface ---

        /// <summary>
        /// Make a comparison between this instance and another based on
        /// Identifiers.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(MessageID other)
        {
            int i = 0;
            for (; i < Length && i < other.Length; i++)
            {
                var x = Identifier[i].CompareTo(other.Identifier[i]);
                if (x != 0) return x;
            }
            if (i < Length) return 1;
            else if (i < other.Length) return -1;
            else return 0;
        }

        #endregion

        #region --- IEquatable Interface ---

        /// <summary>
        /// Compares two MessageIDs by their Identifiers.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(MessageID other)
        {
            return CompareTo(other) == 0;
        }

        /// <summary>
        /// Compares this mid to another.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            return Equals((MessageID)obj);
        }

        /// <summary>
        /// Compares two Message IDs by CAN Identifier bits.
        /// </summary>
        /// <param name="one"></param>
        /// <param name="other"></param>
        /// <returns></returns>
        public static bool operator ==(MessageID one, MessageID other)
        {
            if (null == (object)one) return (object)other == null;
            return one.Equals(other);
        }

        /// <summary>
        /// Compares two Message IDs by CAN Identifier bits.
        /// </summary>
        /// <param name="one"></param>
        /// <param name="other"></param>
        /// <returns></returns>
        public static bool operator !=(MessageID one, MessageID other)
        {
            if (null == (object)one) return (object)other != null;
            return !one.Equals(other);
        }

        /// <summary>
        /// Returns a unique hash of this Message ID based on its Identifier.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            int hash = 0;
            int c = 3;
            foreach (int b in Identifier)
                hash += b << (c-- * 8);
            return hash;
        }

        #endregion
    }

    /// <summary>
    /// The type of Message ID.
    /// </summary>
    [Serializable]
    public enum MessageIDType
    {
        /// <summary>
        /// A messge ID that is 11 bits long.
        /// </summary>
        _11Bit = 0x02,
        /// <summary>
        /// A message ID that is 29 bits long.
        /// </summary>
        _29Bit = 0x04,
    };
}
