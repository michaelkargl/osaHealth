module osaHealth.Framework.Api

open System
open System.Net
open System.Net.Http
open System.Text
open System.Threading.Tasks
open FSharp.UMX
open osaHealth.Framework.Json

[<Measure>]
type HttpResponse

[<Measure>]
type HttpEndpoint

[<Measure>]
type HttpPath

[<Measure>]
type HttpBody

let buildHttpClient (endpoint: string<HttpEndpoint>) =
    new HttpClient(BaseAddress = Uri(endpoint |> UMX.untag))

let postQueryAsync
    (endpoint: string<HttpEndpoint>)
    (path: string<HttpPath>)
    (body: string<HttpBody>)
    : Task<HttpStatusCode * string<HttpResponse>> =
    task {
        let body = body |> UMX.untag
        let path = path |> UMX.untag
        let client = buildHttpClient endpoint
        let content = new StringContent(body, Encoding.UTF8, "application/json")
        let! response = client.PostAsync(path, content)
        let! responseText = response.Content.ReadAsStringAsync()
        return response.StatusCode, (responseText.Trim() |> UMX.tag)
    }

let displayResponse (label: string) (statusCode: HttpStatusCode, body: string<HttpResponse>) : unit =
    let body = body |> UMX.untag

    printfn "--- %s ---" label
    printfn "HTTP %A" statusCode

    if body = "" then
        printfn "(empty body)\n"
    else
        body |> prettyPrint |> printfn "%s\n"
