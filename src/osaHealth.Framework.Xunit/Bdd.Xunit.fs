namespace osaHealth.Framework.Testing.Bdd

open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Xunit

module Xunit =
    let private writeOutput (message: string) =
        match TestContext.Current.TestOutputHelper with
        | null -> ()
        | helper -> helper.WriteLine(message)

    type Factory =
        static member create<'TContext, 'TResult>
            (initial: 'TContext, [<CallerMemberName; Optional; DefaultParameterValue("")>] name: string)
            : Scenario<'TContext, 'TResult> =
            Scenario.Factory.create (initial, writeOutput, name)
