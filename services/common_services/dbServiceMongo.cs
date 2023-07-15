using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

public class dbServiceMongo
{
    private readonly MongoClient _mongoClient;
    private readonly IMongoDatabase _mongoDB;


    public dbServiceMongo(string settingsKey)
    {
        IConfiguration appsettings = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build(); // this is to read the rabbit settings from settings file 
        _mongoClient = new MongoClient(appsettings[settingsKey + ":connStr"]);
        _mongoDB = _mongoClient.GetDatabase(appsettings[settingsKey + ":dbName"]);

    }


    // the below function creates indexes on collections like ascending or unique.
    public async Task CreateMultipleIndexesAsync(string indexesJson, string collectionName)
    {
        //     string indexesJson = @"[
        //     { ""keys"": { ""Name"": 1 }, ""options"": { ""unique"": true } },
        //     { ""keys"": { ""Email"": 1 }, ""options"": { ""unique"": true } },
        //     { ""keys"": { ""Age"": 1 }, ""options"": { } }
        // ]";
        try
        {
            var indexDefinitions = BsonSerializer.Deserialize<List<BsonDocument>>(indexesJson);
            var indexModels = indexDefinitions.Select(indexDef => new CreateIndexModel<BsonDocument>(
                indexDef["keys"].AsBsonDocument,
                new CreateIndexOptions { Unique = indexDef["options"]["unique"].IsBsonNull ? false : indexDef["options"]["unique"].AsBoolean }
            )).ToList();

            IMongoCollection<BsonDocument> collection = _mongoDB.GetCollection<BsonDocument>(collectionName);
            await collection.Indexes.CreateManyAsync(indexModels);
        }
        catch (Exception ex)
        {
            throw;
        }


    }


    public async Task SetCollectionValidationRuleAsync(string jsonSchemaString, string collectionName)
    {

        // string jsonSchemaString = @"{
        // 'bsonType': 'object',
        // 'required': ['name', 'age'], 
        // 'properties': {
        //     'name': {
        //         'bsonType': 'string',
        //         'description': 'The \'name\' field must be a String.'
        //     },
        //     'age': {
        //         'bsonType': 'int',
        //         'minimum': 0,
        //         'description': 'The \'age\' field must be an Integer.'
        //     },
        //     'email': {
        //         'bsonType': 'string',
        //         'pattern': '^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$',
        //         'description': 'The \'email\' field must be a valid email address.'
        //     },
        //     'int64Field': {
        //         'bsonType': 'long',
        //         'description': 'The \'int64Field\' field must be a 64-bit integer.'
        //     },
        //     'dateField': {
        //         'bsonType': 'date',
        //         'description': 'The \'dateField\' field must be a date.'
        //     },
        //     'timeField': {
        //         'bsonType': 'date',
        //         'description': 'The \'timeField\' field must be a time.'
        //     },
        //     'dateTimeField': {
        //         'bsonType': 'date',
        //         'description': 'The \'dateTimeField\' field must be a datetime.'
        //     }
        //     }
        // }";

        try
        {
            var jsonSchema = BsonDocument.Parse(jsonSchemaString);
            var validator = new BsonDocument("$jsonSchema", jsonSchema);
            var command = new BsonDocument
            {
                {
                    "collMod", collectionName
                },
                {
                    "validator", validator
                },
                {
                    "validationLevel", "strict"
                },
                {
                    "validationAction", "error"
                }
            };
            await _mongoDB.RunCommandAsync<BsonDocument>(command);

        }
        catch (Exception ex)
        {
            throw;
        }


    }



    public async Task<mongoResponse> executeStatements(mongoRequest mongoReq, Boolean returnSession)
    {
        IClientSessionHandle session = null;
        //mongoResponse mRet = new mongoResponse();
        mongoResponse mongoResp = new mongoResponse(); // initialize 

        try
        {
            //mReturns = new List<mongoReturn>();

            var sessionOptions = new ClientSessionOptions
            {
                DefaultTransactionOptions = new TransactionOptions(
                readConcern: ReadConcern.Snapshot,
                writeConcern: WriteConcern.WMajority,
                maxCommitTime: TimeSpan.FromSeconds(5))
            };
            session = _mongoClient.StartSession(sessionOptions);

            session.StartTransaction();

            for (int i = 0; i < mongoReq._reqStatements.Count; i++)
            {

                var ms = mongoReq._reqStatements[i];
                var mr = mongoResp.newResponseStatement();
                mr._statementType = ms._statementType;

                IMongoCollection<BsonDocument> collection = _mongoDB.GetCollection<BsonDocument>(ms._collectionName);
                if (ms._statementType == 0) // select 3
                {
                    mr._selectedResults = collection.Find(ms._filters).ToList();
                }
                else if (ms._statementType == 1) // insert 0
                {
                    await collection.InsertManyAsync(session, ms._sub_statements);
                }
                else if (ms._statementType == 2) // delete 1
                {
                    mr._deleteResult = await collection.DeleteManyAsync(session, ms._filters);
                }
                else if (ms._statementType == 3) // update 2
                {
                    mr._updateResult = await collection.UpdateManyAsync(session, ms._filters, ms._updates);
                }
                else // if not insert/update or delete then roll back transaction 
                {
                    throw new Exception("Error in passed document");
                }
                //mRet.mRetStatements.Add(mr);
            }
            if (returnSession) // if the session is to be returned do not commit transaction return the session
            {
                mongoResp.session = session;
            }
            else // if session is not to be returned commit transaction
            {
                await session.CommitTransactionAsync(); // if all the above statements executes correctly its a go go else 
            }
            return mongoResp; // return the values
        }
        catch (Exception ex)
        {
            await session.AbortTransactionAsync(); // else roll back if any error occurs
            throw;
        }

    }


}


public class mongoRequest
{
    public IClientSessionHandle _session = null;
    public List<mongoRequestStatement> _reqStatements = new List<mongoRequestStatement>();

    public mongoRequest() // this constructor can be used if session is passes from calling method.
    {
        _session = null;
    }
    public mongoRequest(IClientSessionHandle session) // this constructor can be used if session is passes from calling method.
    {
        _session = session;
    }


    public void newRequestStatement(int statementType, string collectionName, BsonDocument filters, BsonDocument requiredFields, BsonDocument updates, BsonDocument[] sub_statements)
    {
        mongoRequestStatement ms = new mongoRequestStatement();
        ms._statementType = statementType;
        ms._collectionName = collectionName;
        ms._filters = filters;
        ms._requiredFields = requiredFields;
        ms._updates = updates;
        ms._sub_statements = sub_statements;
        _reqStatements.Add(ms);
    }
    public class mongoRequestStatement
    {
        public int _statementType = 0;
        public string _collectionName;
        public BsonDocument _filters;
        public BsonDocument _requiredFields;
        public BsonDocument _updates;
        public BsonDocument[] _sub_statements;
    }

}


public class mongoResponse
{
    public IClientSessionHandle session = null;
    public List<mongoResponseStatement> _resStatements = new List<mongoResponseStatement>();

    public mongoResponseStatement newResponseStatement(int statementType, List<BsonDocument> selectedResults, DeleteResult deleteResult, UpdateResult updateResult)
    {
        mongoResponseStatement mr = new mongoResponseStatement();
        mr._statementType = statementType;
        mr._selectedResults = selectedResults;
        mr._deleteResult = deleteResult;
        mr._updateResult = updateResult;
        _resStatements.Add(mr);
        return mr;
    }
    public mongoResponseStatement newResponseStatement()
    {
        mongoResponseStatement mr = new mongoResponseStatement();
        mr._statementType = 0;
        mr._selectedResults = null;
        mr._deleteResult = null;
        mr._updateResult = null;
        _resStatements.Add(mr);
        return mr;
    }

    public class mongoResponseStatement
    {
        public int _statementType = 0;
        public List<BsonDocument> _selectedResults;
        public DeleteResult _deleteResult;
        public UpdateResult _updateResult;
    }


}



