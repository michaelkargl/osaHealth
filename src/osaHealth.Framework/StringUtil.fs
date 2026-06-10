module osaHealth.Framework.StringUtil

open System

/// Returns defaultStr when str is null, empty, or whitespace.
let defaultIfNullOrWhiteSpace (defaultStr: string) (str: string) : string =
    if String.IsNullOrWhiteSpace str then defaultStr else str
