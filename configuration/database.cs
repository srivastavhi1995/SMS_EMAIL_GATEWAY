using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;

public class dbSettings
{
    public string connStr {get;set;}=null!;
    public string dbName { get; set; }= null!;
    //private IMongoDatabase _mongoDB;

    public async Task insertData(String tblName, List<BsonDocument> bd)
    {
        MongoClient mongoClient = new MongoClient(connStr);
        IMongoDatabase _mongoDB = mongoClient.GetDatabase(dbName);
        IMongoCollection<BsonDocument> collection = _mongoDB.GetCollection<BsonDocument>(tblName);
        await collection.InsertManyAsync(bd);
    }

    public async Task executeStatements(List<mongoStatements> allMongoStatements)
    {
        MongoClient mongoClient = new MongoClient(connStr);
        IMongoDatabase _mongoDB = mongoClient.GetDatabase(dbName);
        var sessionOptions = new ClientSessionOptions
        {
            DefaultTransactionOptions = new TransactionOptions(
            readConcern: ReadConcern.Snapshot,
            writeConcern: WriteConcern.WMajority,
            maxCommitTime: TimeSpan.FromSeconds(5))
        };


        using (var session = await mongoClient.StartSessionAsync(sessionOptions))
        {
            try
            {
                session.StartTransaction();

                foreach (mongoStatements ms in allMongoStatements)
                {

                    IMongoCollection<BsonDocument> collection = _mongoDB.GetCollection<BsonDocument>(ms.collectionName);
                    if (ms.statementType == 0) // insert
                    {
                        await collection.InsertManyAsync(session, ms.statements);
                    }
                    else if (ms.statementType == 1) // delete
                    {
                        var filterDefinitions = new List<FilterDefinition<BsonDocument>>();
                        foreach (var filter in ms.filters)
                        {
                            var filterDefinition = new BsonDocumentFilterDefinition<BsonDocument>(filter);
                            filterDefinitions.Add(filterDefinition);
                        }
                        var allFilters = Builders<BsonDocument>.Filter.Or(filterDefinitions);
                        await collection.DeleteManyAsync(session,allFilters);
                    }
                    else if (ms.statementType == 2) // update
                    {
                        var filterDefinitions = new List<FilterDefinition<BsonDocument>>();
                        foreach (var filter in ms.filters)
                        {
                            var filterDefinition = new BsonDocumentFilterDefinition<BsonDocument>(filter);
                            filterDefinitions.Add(filterDefinition);
                        }
                        var allFilters = Builders<BsonDocument>.Filter.Or(filterDefinitions);

                        var updateDefinitions = new List<UpdateDefinition<BsonDocument>>();
                        foreach (var update in ms.statements)
                        {
                            var updateDefinition = new BsonDocumentUpdateDefinition<BsonDocument>(update);
                            updateDefinitions.Add(updateDefinition);
                        }
                        var allUpdates = Builders<BsonDocument>.Update.Combine(updateDefinitions);

                        await collection.UpdateManyAsync(session,allFilters, allUpdates);
                    }
                    else // if not insert/update or delete then roll back transacrion 
                    {
                        throw new Exception("Error in passed document");
                    }
                }

                await session.CommitTransactionAsync(); // if all the above statements executes correctly its a go go else 
            }
            catch (Exception ex)
            {
                await session.AbortTransactionAsync(); // else roll back if any error occurs
                throw;
            }
        }



        //IMongoCollection<BsonDocument> collection = _mongoDB.GetCollection<BsonDocument>(tblName);
        //await collection.InsertManyAsync(bd);
    }

    // public async Task updateData(String tblName )
    // {
    //     MongoClient mongoClient = new MongoClient(connStr);
    //     IMongoDatabase _mongoDB = mongoClient.GetDatabase(dbName);
    //     IMongoCollection<BsonDocument> collection = _mongoDB.GetCollection<BsonDocument>(tblName);
    //     await collection.InsertManyAsync(bd);
    // }

}

public class mongoStatements
{
    public int statementType = 0;
    public string collectionName;
    public BsonDocument filters;
    public BsonDocument updates; 
    public BsonDocument[] statements;
}

