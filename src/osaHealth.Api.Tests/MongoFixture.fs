module osaHealth.Api.Tests.MongoFixture

open System.Threading.Tasks
open MongoDB.Driver
open Testcontainers.MongoDb

let private container = lazy MongoDbBuilder().Build()
    
let private getDbClientAsync =
    task {
        do! container.Value.StartAsync()
        osaHealth.Api.Host.registerBsonSerializers ()
        return new MongoClient(container.Value.GetConnectionString())
    }

let getDbCollection<'TDocument> (databaseName: string) (collectionName: string) : Task<IMongoCollection<'TDocument>> =
    task {
        let! client = getDbClientAsync 
        return client.GetDatabase(databaseName).GetCollection<'TDocument>(collectionName)
    }
