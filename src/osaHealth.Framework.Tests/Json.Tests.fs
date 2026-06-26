module osaHealth.Framework.Tests.JsonTests

open System.Text.Json
open Xunit
open osaHealth.Framework
open osaHealth.Framework.Testing.Bdd.Scenario
open osaHealth.Framework.Testing.Bdd.Xunit

[<Fact>]
let ``serialize produces the expected JSON string`` () =
    let ctx = {| Input = {| Name = "test"; Value = 42 |}; Output = "" |}

    Factory.create ctx
    |> GIVEN "a record with string and integer fields" (fun ctx ->
        {| ctx with Input = {| Name = "test"; Value = 42 |} |})
    |> WHEN "the record is serialized" (fun ctx ->
        {| ctx with Output = Json.serialize ctx.Input |})
    |> THEN "the output matches the expected compact JSON string" (fun ctx ->
        Assert.Equal("""{"Name":"test","Value":42}""", ctx.Output)
        ctx)
    |> run

[<Fact>]
let ``prettyPrint formats compact JSON with indentation`` () =
    let ctx = {| Input = ""; Output = "" |}

    Factory.create ctx
    |> GIVEN "a compact single-line JSON string" (fun ctx ->
        {| ctx with Input = """{"name":"test","value":42}""" |})
    |> WHEN "the string is pretty printed" (fun ctx ->
        {| ctx with Output = Json.prettyPrint ctx.Input |})
    |> THEN "the output matches the expected indented JSON string" (fun ctx ->
        let expected = """{
  "name": "test",
  "value": 42
}"""
        Assert.Equal(expected.ReplaceLineEndings(), ctx.Output.ReplaceLineEndings())
        ctx)
    |> run

[<Fact>]
let ``prettyPrint returns the input unchanged when given invalid JSON`` () =
    let ctx = {| Input = "not-valid-json"; Output = "" |}

    Factory.create ctx
    |> GIVEN "an invalid JSON string" (fun ctx ->
        {| ctx with Input = "not-valid-json" |})
    |> WHEN "the string is pretty printed" (fun ctx ->
        {| ctx with Output = Json.prettyPrint ctx.Input |})
    |> THEN "the original string is returned unchanged" (fun ctx ->
        Assert.Equal("not-valid-json", ctx.Output)
        ctx)
    |> run

[<Fact>]
let ``tryGetJsonElement returns Some with the element when the property exists`` () =
    let ctx = {| Document = (null: JsonDocument); Result = (None: JsonElement option) |}

    Factory.create ctx
    |> GIVEN "a JSON document with a known property" (fun ctx ->
        {| ctx with Document = JsonDocument.Parse("""{"token":"abc123"}""") |})
    |> WHEN "the property is looked up" (fun ctx ->
        {| ctx with Result = Json.tryGetJsonElement "token" ctx.Document |})
    |> THEN "the result is Some with the expected string value" (fun ctx ->
        Assert.True(ctx.Result.IsSome)
        Assert.Equal("abc123", ctx.Result.Value.GetString())
        ctx)
    |> run

[<Fact>]
let ``tryGetJsonElement returns None when the property is absent`` () =
    let ctx = {| Document = (null: JsonDocument); Result = (None: JsonElement option) |}

    Factory.create ctx
    |> GIVEN "a JSON document without the requested property" (fun ctx ->
        {| ctx with Document = JsonDocument.Parse("""{"other":"value"}""") |})
    |> WHEN "a missing property is looked up" (fun ctx ->
        {| ctx with Result = Json.tryGetJsonElement "token" ctx.Document |})
    |> THEN "the result is None" (fun ctx ->
        Assert.True(ctx.Result.IsNone)
        ctx)
    |> run

[<Fact>]
let ``tryGetStringValue returns Some with the string when the property exists`` () =
    let ctx = {| Document = (null: JsonDocument); Result = (None: string option) |}

    Factory.create ctx
    |> GIVEN "a JSON document with a string property" (fun ctx ->
        {| ctx with Document = JsonDocument.Parse("""{"name":"osaHealth"}""") |})
    |> WHEN "the string value is retrieved" (fun ctx ->
        {| ctx with Result = Json.tryGetStringValue "name" ctx.Document |})
    |> THEN "the result is Some with the correct string" (fun ctx ->
        Assert.Equal(Some "osaHealth", ctx.Result)
        ctx)
    |> run

[<Fact>]
let ``tryGetStringValue returns None when the property is absent`` () =
    let ctx = {| Document = (null: JsonDocument); Result = (None: string option) |}

    Factory.create ctx
    |> GIVEN "a JSON document without the requested property" (fun ctx ->
        {| ctx with Document = JsonDocument.Parse("""{"other":"value"}""") |})
    |> WHEN "a missing string property is retrieved" (fun ctx ->
        {| ctx with Result = Json.tryGetStringValue "name" ctx.Document |})
    |> THEN "the result is None" (fun ctx ->
        Assert.True(ctx.Result.IsNone)
        ctx)
    |> run

[<Fact>]
let ``getStringValue returns the string when the property exists`` () =
    let ctx = {| Document = (null: JsonDocument); Result = "" |}

    Factory.create ctx
    |> GIVEN "a JSON document with a string property" (fun ctx ->
        {| ctx with Document = JsonDocument.Parse("""{"status":"active"}""") |})
    |> WHEN "the string value is retrieved directly" (fun ctx ->
        {| ctx with Result = Json.getStringValue "status" ctx.Document |})
    |> THEN "the result is the expected string" (fun ctx ->
        Assert.Equal("active", ctx.Result)
        ctx)
    |> run