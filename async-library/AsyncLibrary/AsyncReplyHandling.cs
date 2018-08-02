using Aptiv.Messages;
using Global.SearchTrie.Patterns;
using System;
using System.Collections.Generic;
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
    public class AsyncReplyHandling<TContent> : IDisposable where TContent : IComparable
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
        private ReaderWriterLockSlim pattern_lock = new ReaderWriterLockSlim();

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
        public AsyncReplyHandling(SendMessageDelegate<IEnumerable<TContent>> sender,
            ref EventHandler<MessageReceivedArgs<IEnumerable<TContent>>> handle,
            TContent genericInstance, TContent genericSeries)
        {
            Patterns = new PatternDictionary<TContent, TaskCompletionSource<IEnumerable<TContent>>>(genericInstance, genericSeries);

            this.sender = sender;
            handle += OnReceived;
        }

        #endregion

        #region --- Public Async Methods ---

        /// <summary>
        /// Returns when a message is received that matches the provided
        /// patterns.
        /// </summary>
        /// <param name="patterns">The patterns to match.</param>
        /// <param name="timeout">The amount of time in milliseconds to wait
        /// before cancelling the operation.</param>
        /// <returns></returns>
        public async Task<IEnumerable<TContent>> MatchAny(int timeout = Timeout.Infinite, params IEnumerable<TContent>[] patterns)
        {
            var tcs = new TaskCompletionSource<IEnumerable<TContent>>();

            SetTimeout(tcs, timeout);

            using (new WhileMatching<TContent>(Patterns, tcs, patterns))
            {
                try
                {
                    return await tcs.Task;
                }
                catch (AggregateException e)
                {
                    throw new TimeoutException("The wait action has timed out for the pattern(s): <" + ArrayToString(patterns) + ">", e);
                }
                catch (TaskCanceledException e)
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
        public async Task<IList<IEnumerable<TContent>>> MatchAll(int timeout = Timeout.Infinite, params IEnumerable<TContent>[] patterns)
        {
            var results = new List<IEnumerable<TContent>>();
            var tcsl = new List<TaskCompletionSource<IEnumerable<TContent>>>();

            foreach (var p in patterns)
            {
                var tcs = new TaskCompletionSource<IEnumerable<TContent>>();
                SetTimeout(tcs, timeout);
                AddPattern(p, tcs);
            }
            try
            {
                foreach (var tcs in tcsl)
                    results.Add(await tcs.Task);

                return results;
            }
            catch (AggregateException e)
            {
                throw new TimeoutException("The wait action has timed out for the pattern(s): <" + ArrayToString(patterns) + ">", e);
            }
            catch (TaskCanceledException e)
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
        public async Task<IEnumerable<TContent>> SendAndMatchAny(IEnumerable<TContent> m, int timeout = Timeout.Infinite, params IEnumerable<TContent>[] patterns)
        {
            var tcs = new TaskCompletionSource<IEnumerable<TContent>>();

            SetTimeout(tcs, timeout);

            using (new WhileMatching<TContent>(Patterns, tcs, patterns))
            {
                sender(m);
                try
                {
                    return await tcs.Task;
                }
                catch (AggregateException e)
                {
                    throw new TimeoutException("The wait action has timed out for the pattern(s): <" + ArrayToString(patterns) + ">", e);
                }
                catch (TaskCanceledException e)
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
        public async Task<IList<IEnumerable<TContent>>> SendAndMatchAll(IEnumerable<TContent> m, int timeout = Timeout.Infinite, params IEnumerable<TContent>[] patterns)
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
                    results.Add(await tcs.Task);

                // Return the results.
                return results;
            }
            catch (AggregateException e)
            {
                throw new TimeoutException("The wait action has timed out for the pattern(s): <" + ArrayToString(patterns) + ">", e);
            }
            catch (TaskCanceledException e)
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

        #endregion

        #region --- Private Supporting Methods ---

        /// <summary>
        /// Handle to provide to the Message Received Event handle.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnReceived(object sender, MessageReceivedArgs<IEnumerable<TContent>> args)
        {
            IList<TaskCompletionSource<IEnumerable<TContent>>> toCall;

            // Collect all sources for the received message.
            pattern_lock.EnterReadLock();
            try
            {
                toCall = Patterns.Collect(args.Received);
            }
            finally { pattern_lock.ExitReadLock(); }

            foreach (TaskCompletionSource<IEnumerable<TContent>> t in toCall)
                t.TrySetResult(args.Received);
        }

        /// <summary>
        /// Set the timeout for a TaskCompletionSource.
        /// </summary>
        /// <param name="tcs"></param>
        /// <param name="timeout"></param>
        private void SetTimeout(TaskCompletionSource<IEnumerable<TContent>> tcs, int timeout)
        {
            if (timeout != Timeout.Infinite)
            {
                var cts = new CancellationTokenSource(timeout);
                cts.Token.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false);
            }
        }

        /// <summary>
        /// Converts an IEnumerable to string.
        /// </summary>
        /// <param name="contents"></param>
        /// <returns></returns>
        private string ArrayToString(Array contents)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (var content in contents)
            {
                sb.Append(content);
                sb.Append(", ");
            }
            return sb.ToString().TrimEnd(' ', ',');
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
        // ~AsyncReplyHandling() {
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
