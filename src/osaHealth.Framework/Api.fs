module osaHealth.Framework.Api

open System
open System.Net
open System.Net.Http
open System.Text
open System.Threading.Tasks
open osaHealth.Framework.Json

let buildHttpClient (endpoint: string) =
    new HttpClient(BaseAddress = Uri(endpoint))

let postQueryAsync
    (endpoint: string)
    (path: string)
    (body: string)
    : Task<HttpStatusCode * string> =
    task {
        let client = buildHttpClient endpoint
        let content = new StringContent(body, Encoding.UTF8, "application/json")
        let! response = client.PostAsync(path, content)
        let! responseText = response.Content.ReadAsStringAsync()
        return response.StatusCode, responseText.Trim()
    }

let displayResponse (label: string) (statusCode: HttpStatusCode, body: string) : unit =
    printfn $"--- %s{label} ---"
    printfn $"HTTP %A{statusCode}"

    if body = "" then
        printfn "(empty body)\n"
    else
        body |> prettyPrint |> printfn "%s\n"
