using Global.SearchTrie.Patterns;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Aptiv.Messaging.Async
{
    /// <summary>
    /// A class which provides functions for synchronously awaiting replies.
    /// <para>
    /// ReplyHandling uses a pattern matching dictionary which requires that
    /// the 'Messages' being sent and received be able to be broken down into
    /// <typeparamref name="TContent"/>'s and that the 'Messages' are defined
    /// as IEnumerable of <typeparamref name="TContent"/>.
    /// </para>
    /// </summary>
    /// <typeparam name="TContent"></typeparam>
    [Serializable]
    public class BlockingReplyHandling<TContent> : IDisposable where TContent : IComparable
    {
        #region --- Initialization ---

        /// <summary>
        /// The function to call to send messages which is provided by the
        /// construction call.
        /// </summary>
        private SendMessageDelegate<IEnumerable<TContent>> sender;
        /// <summary>
        /// The set of patterns to match against incoming messages.
        /// </summary>
        private PatternDictionary<TContent, TaskCompletionSource<IEnumerable<TContent>>> Patterns;
        /// <summary>
        /// A lock to protect the <code>Patterns</code> from concurrent access.
        /// </summary>
        [NonSerialized]
        private ReaderWriterLockSlim pattern_lock = new ReaderWriterLockSlim();

        private Action<IEnumerable<TContent>> handle;

        /// <summary>
        /// Construct a new instance for reply handling.
        /// </summary>
        /// <param name="sender">The function to send messages with.</param>
        /// <param name="handle">The handle to receive messages with.</param>
        /// <param name="genericInstance">An instance of the type 
        /// <typeparamref name="TContent"/> that will pattern match a single
        /// item of the same type.</param>
        /// <param name="genericSeries">An instance of the type
        /// <typeparamref name="TContent"/> that will pattern match a series of
        /// items of the same type.</param>
        public BlockingReplyHandling(SendMessageDelegate<IEnumerable<TContent>> sender,
            ref Action<IEnumerable<TContent>> handle,
            TContent genericInstance, TContent genericSeries)
        {
            Patterns = new PatternDictionary<TContent, TaskCompletionSource<IEnumerable<TContent>>>(genericInstance, genericSeries);
            this.handle = handle;
            this.sender = sender;
            handle += OnReceived;
        }

        #endregion

        #region --- Public Methods ---

        /// <summary>
        /// Returns when a message is received that matches the provided
        /// patterns.
        /// </summary>
        /// <param name="patterns">The patterns to match.</param>
        /// <param name="timeout">The amount of time in milliseconds to wait
        /// before cancelling the operation.</param>
        /// <returns></returns>
        public IEnumerable<TContent> MatchAny(int timeout = Timeout.Infinite, params IEnumerable<TContent>[] patterns)
        {
            var tcs = new TaskCompletionSource<IEnumerable<TContent>>();

            SetTimeout(tcs, timeout);

            using (new WhileMatching<TContent>(Patterns, tcs, patterns))
            {
                try
                {
                    return tcs.Task.Result;
                }
                catch (AggregateException e)
                {
                    throw new TimeoutException("The wait action has timed out for the pattern(s): <" + ArrayToString(patterns) + ">", e);
                }
            }
        }

        /// <summary>
        /// Returns when messages are received that match all the provided
        /// patterns.
        /// </summary>
        /// <param name="patterns">The patterns to match.</param>
        /// <param name="timeout">The amount of time in milliseconds to wait
        /// before cancelling the operation.</param>
        /// <returns></returns>
        public IList<IEnumerable<TContent>> MatchAll(int timeout = Timeout.Infinite, params IEnumerable<TContent>[] patterns)
        {
            var results = new List<IEnumerable<TContent>>();
            var tcsl = new List<TaskCompletionSource<IEnumerable<TContent>>>();

            foreach (var p in patterns)
            {
                var tcs = new TaskCompletionSource<IEnumerable<TContent>>();
                SetTimeout(tcs, timeout);
                AddPattern(p, tcs);
                tcsl.Add(tcs);
            }

            try
            {
                foreach (var tcs in tcsl)
                    results.Add(tcs.Task.Result);

                return results;
            }
            catch (AggregateException e)
            {
                throw new TimeoutException("The wait action has timed out for the pattern(s): <" + ArrayToString(patterns) + ">", e);
            }
            finally
            {
                for (int i = 0; i < patterns.Length && i < tcsl.Count; i++)
                    RemovePattern(patterns[i], tcsl[i]);
            }
        }

        /// <summary>
        /// Sends the provided message and waits for a reply to be received which
        /// matches the provided patterns.
        /// </summary>
        /// <param name="m">The message to send.</param>
        /// <param name="patterns">The patterns to match.</param>
        /// <param name="timeout">The amount of time in milliseconds to wait
        /// before cancelling the operation.</param>
        /// <returns></returns>
        public IEnumerable<TContent> SendAndMatchAny(IEnumerable<TContent> m, int timeout = Timeout.Infinite, params IEnumerable<TContent>[] patterns)
        {
            var tcs = new TaskCompletionSource<IEnumerable<TContent>>();

            SetTimeout(tcs, timeout);

            using (new WhileMatching<TContent>(Patterns, tcs, patterns))
            {
                sender(m);
                try
                {
                    return tcs.Task.Result;
                }
                catch (AggregateException e)
                {
                    throw new TimeoutException("The wait action has timed out for the pattern(s): <" + ArrayToString(patterns) + ">", e);
                }
            }
        }

        /// <summary>
        /// Sends the provided message and waits for a replies to be received which
        /// match all of the provided patterns.
        /// </summary>
        /// <param name="m">The message to send.</param>
        /// <param name="patterns">The patterns to match.</param>
        /// <param name="timeout">The amount of time in milliseconds to wait
        /// before cancelling the operation.</param>
        /// <returns></returns>
        public IList<IEnumerable<TContent>> SendAndMatchAll(IEnumerable<TContent> m, int timeout = Timeout.Infinite, params IEnumerable<TContent>[] patterns)
        {
            var results = new List<IEnumerable<TContent>>();
            var tcsl = new List<TaskCompletionSource<IEnumerable<TContent>>>();

            // Create completion sources for each pattern to run in parrellel.
            foreach (var p in patterns)
            {
                var tcs = new TaskCompletionSource<IEnumerable<TContent>>();
                SetTimeout(tcs, timeout);
                AddPattern(p, tcs);
                tcsl.Add(tcs);
            }
            try
            {
                // Send the message.
                sender(m);

                // Await the results.

                foreach (var tcs in tcsl)
                    results.Add(tcs.Task.Result);

                // Return the results.
                return results;
            }
            catch (AggregateException e)
            {
                throw new TimeoutException("The wait action has timed out for the pattern(s): <" + ArrayToString(patterns) + ">", e);
            }
            finally
            {
                // Make sure to remove all patterns.
                for (int i = 0; i < patterns.Length && i < tcsl.Count; i++)
                    RemovePattern(patterns[i], tcsl[i]);
            }
        }

        /// <summary>
        /// Obtains a disposable object that allows the user to provide a function
        /// that is called whenever the provided pattern is matched. The function
        /// is only intended to be used within a <c>using</c> statement, so
        /// holding an instance of the returning type is marked as <c>Obsolete</c>.
        /// </summary>
        /// <param name="pattern">The pattern to match that will call the action.</param>
        /// <param name="action">The action to be called, the passed parameter is the message received.</param>
        /// <param name="handle">The receiving handle to initiate calls to the provided action.</param>
        /// <returns>An instance of an obsolete class which is disposable.</returns>
#pragma warning disable CS0618
        public CallOnMatch Call_On_Match(
            IEnumerable<TContent> pattern,
            Action<IEnumerable<TContent>> action,
            ref Action<IEnumerable<TContent>> handle)
        {
            var com = new CallOnMatch
#pragma warning restore CS0618
            {
                action = action,
                dict = new PatternDictionary<TContent, bool>(Patterns.genericPiece, Patterns.genericSeriesPiece)
                {
                    { pattern, true }
                }
            };
            com.SetHandle(ref handle);

            return com;
        }

        #region --- Call On Match class ---
        /// <summary>
        /// A disposable class that assists creating functionality to call a
        /// provided delegate on matching a particular pattern.
        /// </summary>
        [Serializable]
        [Obsolete("Use the Call_On_Match(pattern, action) function in your using statements" +
            " instead of creating and instance of this class.", false)]
        public class CallOnMatch : IDisposable
        {
            /// <summary>
            /// The dictionary to store the patterns for pattern comparison.
            /// </summary>
            public PatternDictionary<TContent, bool> dict;
            /// <summary>
            /// The action to call on match.
            /// </summary>
            public Action<IEnumerable<TContent>> action;
            /// <summary>
            /// The handle that signals receive events.
            /// </summary>
            public Action<IEnumerable<TContent>> handle;

            /// <summary>
            /// Set this C.O.M. handle.
            /// </summary>
            /// <param name="handle"></param>
            public void SetHandle(ref Action<IEnumerable<TContent>> handle)
            {
                this.handle = handle;
                handle += Call;
            }

            private void Call(IEnumerable<TContent> args)
            {
                if (dict.Collect(args).Count > 0)
                    action?.Invoke(args);
            }

            #region IDisposable Support
            private bool disposedValue = false; // To detect redundant calls

            /// <summary>
            /// Dispose of this objects resources.
            /// </summary>
            /// <param name="disposing"></param>
            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        handle -= Call;
                    }

                    // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                    // TODO: set large fields to null.

                    disposedValue = true;
                }
            }

            // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
            // ~CallOnMatch() {
            //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            //   Dispose(false);
            // }

            // This code added to correctly implement the disposable pattern.
            /// <summary>
            /// Dispose of this object's resources.
            /// </summary>
            public void Dispose()
            {
                // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
                Dispose(true);
                // TODO: uncomment the following line if the finalizer is overridden above.
                // GC.SuppressFinalize(this);
            }
            #endregion
        }
        #endregion

        #endregion

        #region --- Private Support Methods ---

        /// <summary>
        /// Handle to provide to the Message Received Event handle.
        /// </summary>
        /// <param name="args"></param>
        private void OnReceived(IEnumerable<TContent> args)
        {
            IList<TaskCompletionSource<IEnumerable<TContent>>> toCall;

            // Collect all sources for the received message.
            pattern_lock.EnterReadLock();
            try
            {
                toCall = Patterns.Collect(args);
            }
            finally { pattern_lock.ExitReadLock(); }

            foreach (TaskCompletionSource<IEnumerable<TContent>> t in toCall)
                t.TrySetResult(args);
        }

        /// <summary>
        /// Set the timeout for a TaskCompletionSource.
        /// </summary>
        /// <param name="tcs"></param>
        /// <param name="timeout"></param>
        private void SetTimeout(TaskCompletionSource<IEnumerable<TContent>> tcs, int timeout)
        {
            if (timeout != Timeout.Infinite)
                Task.Run(async () =>
                {
                    await Task.Delay(timeout);
                    tcs.TrySetCanceled();
                });
        }

        /// <summary>
        /// Converts an IEnumerable to string.
        /// </summary>
        /// <param name="contents"></param>
        /// <returns></returns>
        private string ArrayToString(Array contents)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var content in contents)
            {
                sb.Append(content);
                sb.Append(", ");
            }
            return sb.ToString().TrimEnd(' ',',');
        }

        /// <summary>
        /// Add the pair with locking.
        /// </summary>
        /// <param name="pattern"></param>
        /// <param name="source"></param>
        private void AddPattern(IEnumerable<TContent> pattern, TaskCompletionSource<IEnumerable<TContent>> source)
        {
            pattern_lock.EnterWriteLock();
            try
            {
                Patterns.Add(pattern, source);
            }
            finally
            {
                pattern_lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Remove the pair with locking.
        /// </summary>
        /// <param name="pattern"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        private bool RemovePattern(IEnumerable<TContent> pattern, TaskCompletionSource<IEnumerable<TContent>> source)
        {
            pattern_lock.EnterWriteLock();
            try
            {
                return Patterns.Remove(pattern, source);
            }
            finally
            {
                pattern_lock.ExitWriteLock();
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        /// <summary>
        /// Dispose of this object.
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    pattern_lock.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~BlockingReplyHandling() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        /// <summary>
        /// Dispose of this object.
        /// </summary>
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

        #endregion
    }
}
