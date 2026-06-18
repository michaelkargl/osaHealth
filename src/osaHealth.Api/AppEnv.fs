module osaHealth.Api.AppEnv

open System
open MongoDB.Bson
open MongoDB.Bson.Serialization
open MongoDB.Bson.Serialization.Serializers
open MongoDB.Driver
open osaHealth.Repository.Entities
open osaHealth.Repositories

type AppEnv =
    { RecordingsCollection: IMongoCollection<Recording> }

let private getOptional (envVarName: string): string | null =
    Environment.GetEnvironmentVariable(envVarName)

let private getRequired (envVarName: string): string =
    match getOptional envVarName with
        | null -> failwith $"Required environment variable '{envVarName}' is not set."
        | value -> value

module AppEnv =
    let create () =
        BsonSerializer.RegisterSerializer(GuidSerializer(GuidRepresentation.Standard))

        let connectionString: string = getRequired "MongoDB__ConnectionString"
        let databaseName = getRequired "MongoDB__DatabaseName"

        let mongoClient = new MongoClient(connectionString)
        let db = mongoClient.GetDatabase(databaseName)

        { RecordingsCollection = db.GetCollection<Recording>(CollectionName) }
