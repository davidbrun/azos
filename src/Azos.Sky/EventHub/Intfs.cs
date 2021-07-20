﻿/*<FILE_LICENSE>
 * Azos (A to Z Application Operating System) Framework
 * The A to Z Foundation (a.k.a. Azist) licenses this file to you under the MIT license.
 * See the LICENSE file in the project root for more information.
</FILE_LICENSE>*/

using System.Collections.Generic;
using System.Threading.Tasks;

using Azos.Apps;

namespace Azos.Sky.EventHub
{
  /// <summary>
  /// Produces events into named queues
  /// </summary>
  public interface IEventProducer
  {
    /// <summary>
    /// This cluster region/zone id tag
    /// </summary>
    Atom Origin { get; }

    /// <summary>
    /// Creates an instance of Event initialized with payload and optional headers
    /// </summary>
    /// <param name="contentType">Event content type</param>
    /// <param name="content">Event content. You can specify up to <see cref="Event.MAX_CONTENT_LENGTH"/></param>
    /// <param name="headers">Optional event headers. You can specify up to <see cref="Event.MAX_HEADERS_LENGTH"/></param>
    /// <returns>New instance of Event initialized with cluster-unique ID/precision time stamp</returns>
    Event MakeNew(Atom contentType, byte[] content, string headers = null);

    /// <summary>
    /// Posts event into the queue
    /// </summary>
    Task<WriteResult> PostAsync(Route route, Event evt, DataLossMode lossMode = DataLossMode.Default);
  }

  /// <summary>
  /// Implementation of IEventProducer
  /// </summary>
  public interface IEventProducerLogic : IEventProducer, IModuleImplementation
  {
  }

  /// <summary>
  /// Consumes events from queue
  /// </summary>
  public interface IEventConsumer
  {
    /// <summary>
    /// This cluster region/zone id tag
    /// </summary>
    Atom Origin { get; }

    /// <summary>
    /// Fetches the count of messages starting at the specified checkpoint
    /// </summary>
    Task<IEnumerable<Event>> FetchAsync(Route route, ulong checkpoint, int count, DataLossMode lossMode = DataLossMode.Default);

    /// <summary>
    /// Gets the checkpoint for the consumer
    /// </summary>
    Task<ulong> GetCheckpoint(Route route, string idConsumer, DataLossMode lossMode = DataLossMode.Default);

    /// <summary>
    /// Sets the checkpoint for the specified consumer
    /// </summary>
    Task<WriteResult> SetCheckpoint(Route route, string idConsumer, ulong checkpoint, DataLossMode lossMode = DataLossMode.Default);
  }

  /// <summary>
  /// Implementation of IEventConsumer
  /// </summary>
  public interface IEventConsumerLogic : IEventConsumer, IModuleImplementation
  {
  }
}
