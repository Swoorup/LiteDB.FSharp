namespace LiteDB.FSharp

open System
open FSharp.Reflection
open Newtonsoft.Json
open LiteDB

/// Utilities to convert between BSON document and F# types
module Bson = 
    /// Returns the value of entry in the BsonDocument by it's key
    let read key (doc: BsonDocument) =
        doc.[key]

    /// Reads a property from a BsonDocument by it's key as a string
    let readStr key (doc: BsonDocument) = 
        doc.[key].AsString

    /// Reads a property from a BsonDocument by it's key and converts it to an integer
    let readInt key (doc: BsonDocument) = 
        doc.[key].AsString |> int

    /// Adds an entry to a `BsonDocument` given a key and a BsonValue
    let withKeyValue key value (doc: BsonDocument) = 
        doc.Add(key, value)
        doc

    /// Removes an entry (property) from a `BsonDocument` by the key of that property
    let removeEntryByKey (key:string) (doc: BsonDocument) = 
        doc.Remove(key) |> ignore
        doc

    let private fableConverter = Fable.JsonConverter()
    let private converters : JsonConverter[] = [| fableConverter |]

    /// Converts a typed entity (normally an F# record) to a BsonDocument. 
    /// Assuming there exists a field called `Id` or `id` of the record that will be mapped to `_id` in the BsonDocument, otherwise an exception is thrown.
    let serialize<'t> (entity: 't) = 
        let typeName = typeof<'t>.Name
        let json = JsonConvert.SerializeObject(entity, converters)
        let doc = LiteDB.JsonSerializer.Deserialize(json) |> unbox<LiteDB.BsonDocument>
        doc.Keys
        |> Seq.tryFind (fun key -> key = "Id" || key = "id")
        |> function
          | Some key -> 
             doc
             |> withKeyValue "_id" (read key doc) 
             |> removeEntryByKey key
          | None -> 
              let error = sprintf "Exected type %s to have a unique identifier property of 'Id' (exact name)" typeName
              failwith error

    /// Converts a BsonDocument to a typed entity given the document the type of the CLR entity.
    let deserializeByType (entity: BsonDocument) (entityType: Type) = 
        let key = 
          if FSharpType.IsRecord entityType 
          then FSharpType.GetRecordFields entityType 
               |> Seq.tryFind (fun field -> field.Name = "Id" || field.Name = "id")
               |> function | Some field -> field.Name
                           | None -> "Id"
          else "Id"
        entity
        |> withKeyValue key (read "_id" entity) 
        |> removeEntryByKey "_id"
        |> LiteDB.JsonSerializer.Serialize // Bson to Json
        |> fun json -> JsonConvert.DeserializeObject(json, entityType, converters) // Json to obj
    
    /// Converts a BsonDocument to a typed entity given the document the type of the CLR entity.
    let deserialize<'t>(entity: BsonDocument) = 
        let typeInfo = typeof<'t>
        deserializeByType entity typeInfo
        |> unbox<'t>
