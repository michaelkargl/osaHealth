module osaHealth.Framework.DateTime

open System
open System.Buffers.Binary

let tryParseUtc (bytes: byte[]) (offset: int) : DateTime option =
    if bytes.Length < sizeof<int64> + offset then
        None
    else
        let ticks = BinaryPrimitives.ReadInt64LittleEndian(ReadOnlySpan<byte>(bytes, offset, sizeof<int64>))

        if (ticks < DateTime.MinValue.Ticks) || (ticks > DateTime.MaxValue.Ticks) then
            None
        else
            DateTime(ticks, DateTimeKind.Utc) |> Some
