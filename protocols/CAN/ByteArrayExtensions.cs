using System;

namespace Aptiv.Messages.CAN
{
    /// <summary>
    /// Describes some extension methods for byte[]'s to be used with CAN
    /// Messages.
    /// </summary>
    public static class ByteArrayExtensions
    {
        /// <summary>
        /// Construct a new CAN Message from this byte array.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static CANMessage ToCANMessage(this byte[] data)
        {
            return new CANMessage(data);
        }

        /// <summary>
        /// Construct a new CAN Message from this byte array starting at the
        /// given <paramref name="startIndex"/> and only using the specified
        /// <paramref name="length"/> of bytes.
        /// </summary>
        /// <param name="data">The source.</param>
        /// <param name="startIndex">Where to start in the source.</param>
        /// <param name="length">The number of bytes to use.</param>
        /// <returns></returns>
        public static CANMessage ToCANMessage(this byte[] data, int startIndex, int length)
        {
            var c_ = new CANMessage(length);
            data.CopyTo(c_, startIndex, 0, length);
            return c_;
        }

        /// <summary>
        /// Copies this byte array to the provided CANMessage <paramref name="c"/>.
        /// </summary>
        /// <param name="e">The source byte array.</param>
        /// <param name="c">The CAN Message to copy to.</param>
        /// <param name="sourceIndex">The location to start in the source array.</param>
        /// <param name="destIndex">The location to start in the CAN Message.</param>
        public static void CopyTo(this byte[] e, CANMessage c, int sourceIndex = 0, int destIndex = 0)
        {
            if (sourceIndex >= e.Length || sourceIndex < 0)
                throw new ArgumentOutOfRangeException("sourceIndex");
            if (destIndex < 0 || destIndex > c.Count)
                throw new ArgumentOutOfRangeException("destIndex");

            e.CopyTo(c, sourceIndex, destIndex, Math.Min(e.Length - sourceIndex, c.Count - destIndex));
        }

        /// <summary>
        /// Copies this byte array to the provided CANMessage <paramref name="c"/>.
        /// </summary>
        /// <param name="e">The source byte array.</param>
        /// <param name="c">The CAN Message to copy to.</param>
        /// <param name="sourceIndex">The location to start in the source array.</param>
        /// <param name="destIndex">The location to start in the CAN Message.</param>
        /// <param name="length">The number of bytes to copy over.</param>
        public static void CopyTo(this byte[] e, CANMessage c, int sourceIndex, int destIndex, int length)
        {
            if (sourceIndex >= e.Length || sourceIndex < 0)
                throw new ArgumentOutOfRangeException("sourceIndex");
            if (destIndex < 0 || destIndex > c.Count)
                throw new ArgumentOutOfRangeException("destIndex");

            for (
                int i = sourceIndex, j = destIndex;
                i < length && i < e.Length && j < c.Count;
                i++, j++)

                c[j] = e[i];
        }
    }
}
