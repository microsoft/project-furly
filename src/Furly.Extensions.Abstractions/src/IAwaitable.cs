// ------------------------------------------------------------
//  Copyright (c) Microsoft.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Furly
{
    using System.Threading.Tasks;

    /// <summary>
    /// <para>
    /// Sometimes it is convinient to fire and forget asynchronous
    /// tasks, e.g., the creation of long running operations or
    /// the initialization of external resources in the constructor.
    /// </para>
    /// <para>
    /// This interface can be implemented to allow a user of the
    /// class to ensure the initialization has been completed
    /// before using it the first time, e.g., in test scenarios.
    /// </para>
    /// <para>
    /// Implementing the interface with a TResult type of the class
    /// that is implementing, then starting the task in the constructor
    /// and finally converting the task into an awaiter using the
    /// <see cref="TaskExtensions.AsAwaiter{T}(Task{T})"/> extension
    /// allows you to await the constructions like this:
    /// <code>
    /// var result = await new AwaitableObject();
    /// </code>
    /// </para>
    /// <para>
    /// Using the class name of the constructed class also enables
    /// easy dependency resolution of singletons and awaiting their
    /// completion using <see cref="TaskExtensions.WhenAll"/>.
    /// </para>
    /// </summary>
    /// <typeparam name="TResult">The awaited result.</typeparam>
    public interface IAwaitable<out TResult> : IAwaitable
    {
        /// <summary>
        /// Get the awaiter
        /// </summary>
        IAwaiter<TResult> GetAwaiter();
    }

    /// <summary>
    /// Awaitable without result
    /// </summary>
#pragma warning disable CA1040 // Avoid empty interfaces
    public interface IAwaitable;
#pragma warning restore CA1040 // Avoid empty interfaces

}
