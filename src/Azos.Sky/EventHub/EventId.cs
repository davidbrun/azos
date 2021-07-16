﻿/*<FILE_LICENSE>
 * Azos (A to Z Application Operating System) Framework
 * The A to Z Foundation (a.k.a. Azist) licenses this file to you under the MIT license.
 * See the LICENSE file in the project root for more information.
</FILE_LICENSE>*/

using Azos.Data.Idgen;
using System;
using System.Collections.Generic;
using System.Threading;


namespace Azos.Sky.EventHub
{
  /// <summary>
  /// Uniquely identifies event in a node set (cluster)
  /// </summary>
  public struct EventId : IEquatable<EventId>, IDistributedStableHashProvider//, IJsonReadable, IJsonWritable
  {
    /// <summary>
    /// Generates new ID in the scope of this app cluster
    /// </summary>
    public static EventId GenerateNew(IApplication app)
    {
      //WARNING: must use cluster-synchronized app clock
      var utc = app.NonNull(nameof(app)).TimeSource.UTCNow.ToUnsignedMillisecondsSinceUnixEpochStart();
      var ctr = (uint)Interlocked.Increment(ref s_Counter);
      var node = app.NodeDiscriminator;//#511
      return new EventId(utc, ctr, node);
    }

    /// <summary>
    /// Initializes EventId; use EventId.GenerateNew(app) .ctor to generate new id
    /// </summary>
    private EventId(ulong utc, uint counter, ushort node)
    {
      CreateUtc = utc;
      Counter = counter;
      NodeDiscriminator = node;
    }

    /// <summary>
    /// Creates instance from byte[] obtained from call to .Bytes
    /// </summary>
    public EventId(byte[] bytes, int startIdx = 0)
    {
      if (bytes == null || startIdx < 0 || (bytes.Length - startIdx) < sizeof(ulong) + sizeof(uint) + sizeof(ushort))
        throw new AzosException(StringConsts.ARGUMENT_ERROR + "EventId.ctor(bytes==null<minsz)");

      CreateUtc = bytes.ReadBEUInt64(ref startIdx);
      Counter = bytes.ReadBEUInt32(ref startIdx);
      NodeDiscriminator = bytes.ReadBEUShort(ref startIdx);
    }

    private static int s_Counter;

    public readonly ulong   CreateUtc;
    public readonly uint    Counter;
    public readonly ushort  NodeDiscriminator;

    public DateTime CreateDateTimeUtc => CreateUtc.FromMillisecondsSinceUnixEpochStart();

    /// <summary>
    /// True if the structure represents an assigned value
    /// </summary>
    public bool Assigned => CreateUtc > 0;

    /// <summary>
    /// Returns EventId as byte[], e.g. to store in file or database field
    /// </summary>
    public byte[] Bytes
    {
      get
      {
        //WARNING: BE encoding and field order must not change
        var result = new byte[sizeof(ulong) + sizeof(uint) + sizeof(ushort)];
        result.WriteBEUInt64(0, CreateUtc);
        result.WriteBEUInt32(sizeof(ulong), Counter);
        result.WriteBEUShort(sizeof(ulong) + sizeof(uint), NodeDiscriminator);
        return result;
      }
    }

    public bool Equals(EventId other)
      => this.CreateUtc == other.CreateUtc &&
         this.Counter   == other.Counter &&
         this.NodeDiscriminator == other.NodeDiscriminator;

    public override int GetHashCode()
      => CreateUtc.GetHashCode() ^
         Counter.GetHashCode() ^
         NodeDiscriminator.GetHashCode();

    public override bool Equals(object obj)
      => obj is EventId other ? this.Equals(other) : false;

    public ulong GetDistributedStableHash() => CreateUtc ^ Counter;
    public override string ToString() => $"{CreateUtc:x2}-{Counter:x2}-{NodeDiscriminator:x4}]";


    public static bool operator ==(EventId a, EventId b) => a.Equals(b);
    public static bool operator !=(EventId a, EventId b) => !a.Equals(b);
  }
}
