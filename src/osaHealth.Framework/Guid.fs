module osaHealth.Framework.Guid

open System

let tryParse (bytes: byte[]) (offset: int) : Guid option =
    if bytes.Length < (sizeof<Guid> + offset) then
        None
    else
        try
            bytes[offset..] |> Guid |> Some
        with _ -> None
