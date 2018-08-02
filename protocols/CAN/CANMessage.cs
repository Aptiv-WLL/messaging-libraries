using Aptiv.Messaging;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Aptiv.Messages.CAN
{
    /// <summary>
    /// Represents the CAN Message protocol as a data-structure wrapped around
    /// a byte array providing accessors to access defined attributes of a 
    /// CAN Message.
    /// </summary>
    public class CANMessage : IList<byte>, IMessage<byte>
    {
        private IList<byte> bytes;

        /// <summary>
        /// Construct a new CAN Message from an IEnumerable of bytes.
        /// </summary>
        /// <param name="bytes"></param>
        public CANMessage(IEnumerable<byte> bytes)
        {
            this.bytes = (IList<byte>)bytes;
            Message_ID = new MessageID(this);
        }

        /// <summary>
        /// Construct a new CANMessage from a list of bytes.
        /// </summary>
        /// <param name="bytes"></param>
        public CANMessage(IList<byte> bytes)
        {
            this.bytes = bytes;
            Message_ID = new MessageID(this);
        }

        /// <summary>
        /// Construct a CANMessage from an array of bytes to avoid copying data
        /// if you are ok with updates to this CANMessage affecting the source
        /// of the passed data.
        /// </summary>
        /// <param name="bytes"></param>
        public CANMessage(ArraySegment<byte> bytes)
        {
            this.bytes = bytes;
            Message_ID = new MessageID(this);
        }

        /// <summary>
        /// Construct a new CAN Message with the given <paramref name="size"/>
        /// of bytes, all set to <c>0x00</c>.
        /// </summary>
        /// <param name="size">The number of bytes for this message to have.</param>
        public CANMessage(int size)
        {
            bytes = new byte[size];
            Message_ID = new MessageID(this);
        }

        /// <summary>
        /// Obtains a new instance of a struct which wraps the identifiable
        /// information of this CAN Message and makes it accessable through
        /// attributes.
        /// </summary>
        public MessageID Message_ID;

        /// <summary>
        /// Obtains an iterator for the Data field of this CAN Message. You
        /// can make the decision to cast to a byte array if necessary.
        /// </summary>
        public ArraySegment<byte> Data
        {
            get
            {
                int offset = Message_ID.Length;
                int count = bytes.Count - Message_ID.Length;

                // Check if this CAN message was constructed from
                // an ArraySegment and if so get the underlying array
                // before constructing a new ArraySegment.
                if (bytes.GetType() == typeof(ArraySegment<byte>))
                {
                    var a = (ArraySegment<byte>)bytes;
                    return new ArraySegment<byte>(a.Array, a.Offset + offset, count);
                }
                // Otherwise do a simple cast to an array.
                else
                {
                    // Store the casted bytes for the case
                    // when the bytes were not stored as an
                    // array to avoid repeated array construction
                    // on multiple accesses to the Data.
                    bytes = (byte[])bytes;
                    return new ArraySegment<byte>((byte[])bytes, offset, count);
                }
            }
        }

        #region --- IList Interface ---

        public int Count => bytes.Count;

        public bool IsReadOnly => bytes.IsReadOnly;

        public byte this[int index] { get => bytes[index]; set => bytes[index] = value; }

        public int IndexOf(byte item)
        {
            return bytes.IndexOf(item);
        }

        public void Insert(int index, byte item)
        {
            bytes.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            bytes.RemoveAt(index);
        }

        public void Add(byte item)
        {
            bytes.Add(item);
        }

        public void Clear()
        {
            bytes.Clear();
        }

        public bool Contains(byte item)
        {
            return bytes.Contains(item);
        }

        public void CopyTo(byte[] array, int arrayIndex)
        {
            bytes.CopyTo(array, arrayIndex);
        }

        public bool Remove(byte item)
        {
            return bytes.Remove(item);
        }

        #endregion

        #region --- IComparable Interface ---

        /// <summary>
        /// Compare two instances of an CAN Message.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public int CompareTo(object obj)
        {
            if (obj.GetType() == GetType())
            {
                var other = (CANMessage)obj;
                var o_e = other.GetEnumerator();
                foreach (byte b in this)
                {
                    if (o_e.MoveNext())
                    {
                        int x = b.CompareTo(o_e.Current);
                        if (x == 0) continue;
                        else return x;
                    }
                    else return 1; // This instance follows obj in the sort order.
                }
                return -1; // This instance preceeds obj in the sort order.
            }
            else throw new ArgumentException("The provided object is not the same as this instance", "obj");
        }

        #endregion

        #region --- IEnumerable Interface ---

        /// <summary>
        /// Obtains the enumerator for the bytes of this CAN Message.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<byte> GetEnumerator()
        {
            return (IEnumerator<byte>)bytes.GetEnumerator();
        }

        /// <summary>
        /// Obtains the enumerator for the bytes of this CAN Message.
        /// </summary>
        /// <returns></returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return bytes.GetEnumerator();
        }

        #endregion
    }
}
