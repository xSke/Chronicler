﻿using System;
using MessagePack;

namespace SIBR.Storage.CLI.Models
{
    [MessagePackObject]
    public struct RawUpdate
    {
        [Key(0)] public short Type { get; set; }
        [Key(1)] public DateTime Timestamp { get; set; }
        [Key(2)] public Guid Hash { get; set; }
        [Key(3)] public Guid EntityId { get; set; }
        [Key(4)] public Guid SourceId { get; set; }
        [Key(5)] public Guid UpdateId { get; set; }
    }

    [MessagePackObject]
    public struct RawObject
    {
        [Key(0)] public Guid Hash { get; set; }

        // [MessagePackFormatter(typeof(MsgpackJsonFormatter))]
        [Key(1)] public byte[] Data { get; set; }
    }

    [MessagePackObject]
    public struct RawBinaryObject
    {
        [Key(0)] public Guid Hash { get; set; }
        [Key(1)] public byte[] Data { get; set; }
    }

    [MessagePackObject]
    public struct RawGameUpdate
    {
        [Key(0)] public DateTime Timestamp { get; set; }
        [Key(1)] public Guid Hash { get; set; }
        [Key(2)] public Guid GameId { get; set; }
        [Key(3)] public Guid SourceId { get; set; }
        [Key(4)] public short Season { get; set; }
        [Key(5)] public short Day { get; set; }
        [Key(6)] public short Tournament { get; set; }
    }

    [MessagePackObject]
    public struct RawSiteUpdate
    {
        [Key(0)] public DateTime Timestamp { get; set; }
        [Key(1)] public string Path { get; set; }
        [Key(2)] public Guid Hash { get; set; }
        [Key(3)] public Guid SourceId { get; set; }
        [Key(4)] public DateTime? LastModified { get; set; }
    }
}