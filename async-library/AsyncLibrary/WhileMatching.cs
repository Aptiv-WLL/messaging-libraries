using Global.SearchTrie.Patterns;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Aptiv.Messaging.Async
{
    /// <summary>
    /// A class to provide opportunity for calls to be made while a pattern
    /// is inside of a pattern dictionary and to ensure proper disposal of the
    /// insertion through an implementation of IDisposable.
    /// </summary>
    /// <typeparam name="TContent">The type of a pattern's content pieces.</typeparam>
    public class WhileMatching<TContent> : IDisposable
        where TContent : IComparable
    {
        PatternDictionary<TContent, TaskCompletionSource<IEnumerable<TContent>>> dict;
        IEnumerable<TContent>[] patterns;
        TaskCompletionSource<IEnumerable<TContent>> toCall;

        /// <summary>
        /// Construct a new WhileMatching construct utilizing an existing PatternDictionary.
        /// </summary>
        /// <param name="dict">The pattern dictionary to match from.</param>
        /// <param name="toCall">The TaskCompletionSource to trigger on match.</param>
        /// <param name="patterns">The set of patterns to match.</param>
        public WhileMatching(PatternDictionary<TContent, TaskCompletionSource<IEnumerable<TContent>>> dict,
            TaskCompletionSource<IEnumerable<TContent>> toCall,
            params IEnumerable<TContent>[] patterns)
        {
            this.dict = dict;
            this.patterns = patterns;
            this.toCall = toCall;

            foreach (var p in patterns) dict.Add(p, toCall);
        }

        /// <summary>
        /// Construct a new WhileMatching construct with pattern recognition particular
        /// to the defined <paramref name="genericPiece"/> and
        /// <paramref name="genericSeries"/>.
        /// </summary>
        /// <param name="genericPiece">Defines a generic piece in the patterns.</param>
        /// <param name="genericSeries">Defines a generic series in the patterns.</param>
        /// <param name="toCall">The TaskCompletionSource to trigger on call.</param>
        /// <param name="patterns">The set of patterns to match.</param>
        public WhileMatching(TContent genericPiece,
            TContent genericSeries,
            TaskCompletionSource<IEnumerable<TContent>> toCall,
            params IEnumerable<TContent>[] patterns)
        {
            dict = new PatternDictionary<TContent, TaskCompletionSource<IEnumerable<TContent>>>(genericPiece, genericSeries);
            this.patterns = patterns;
            this.toCall = toCall;

            foreach (var p in patterns) dict.Add(p, toCall);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        /// <summary>
        /// Dispose this object.
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    foreach (var p in patterns)
                        dict.Remove(p, toCall);
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~WhileMatching() {
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
}
