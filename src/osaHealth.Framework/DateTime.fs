module osaHealth.Framework.DateTime

open System

let tryParseUtc (bytes: byte[]) (offset: int) : DateTime option =
    if bytes.Length < sizeof<int64> + offset then
        None
    else
        let ticks = BitConverter.ToInt64(bytes, offset)

        if (ticks < DateTime.MinValue.Ticks) || (ticks > DateTime.MaxValue.Ticks) then
            None
        else
            DateTime(ticks, DateTimeKind.Utc) |> Some
