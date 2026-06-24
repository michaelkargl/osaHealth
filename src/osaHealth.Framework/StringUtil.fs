module osaHealth.Framework.StringUtil

open System

/// Returns defaultStr when str is null, empty, or whitespace.
let defaultIfNullOrWhiteSpace (defaultStr: string) (str: string) : string =
    if String.IsNullOrWhiteSpace str then defaultStr else str


let tryFromBase64 (raw: string) : byte[] option =
    // 1 base64 char  = 6 bits
    // Buffer size: N characters × 6 bits/char ÷ 8 bits/byte, rounded up.
    let bufferSize = Math.Ceiling(float raw.Length * 6.0 / 8.0) |> int
    let buffer = Array.zeroCreate<byte> bufferSize
    
    let mutable written = 0
    if Convert.TryFromBase64String(raw, Span(buffer), & written) then
        // -1 converts the byte count to the last valid index
        // Base64 "AA==" (4 chars with padding) => Actually 1 byte
        // Base64 "AAAA" (4 chars no padding) => Actually 3 bytes
        Some buffer[.. written - 1]
    else
        None
