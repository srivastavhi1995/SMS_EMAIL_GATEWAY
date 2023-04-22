using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
public class mongoServices
{
    private readonly IMongoCollection<personal> _personalCollection;
    private IMongoDatabase _mongoDB;
    public mongoServices(IOptions<dbSettings> ds)
    {
        MongoClient mongoClient = new MongoClient(ds.Value.connStr);
        var mongoDb = mongoClient.GetDatabase(ds.Value.connStr);
    }

    public async Task Createsync(String tblName, List<BsonDocument> bd)
        {
            IMongoCollection<BsonDocument> collection = _mongoDB.GetCollection<BsonDocument>(tblName);
            // var jsonObject = new BsonDocument {
            //     { "title", "The Catcher in the Rye" },
            //     { "author", "J.D. Salinger" },
            //     { "year", 1951 },
            //     { "tags", new BsonArray { "novel", "coming-of-age" } }
            // };
            await collection.InsertManyAsync(bd);
        }




    public async Task<List<personal>> GetAsync() => 
        await _personalCollection.Find(_ => true).ToListAsync();
    public async Task<personal> GetAsync(string id) =>
        await _personalCollection.Find(x => x.Id == id).FirstOrDefaultAsync();
    public async Task Createsync(personal pers) => 
        await _personalCollection.InsertOneAsync(pers);
    public async Task Updatesync(personal pers) =>
        await _personalCollection.ReplaceOneAsync(x => x.Id == pers.Id, pers);

    public async Task RemoveAsync(string id) =>
        await _personalCollection.DeleteOneAsync(x=>x.Id==id);




}