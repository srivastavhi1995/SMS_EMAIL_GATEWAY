using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System;

public static class appCommonFunctions
{
    public static async Task createDB(dbServiceMongo db)
    {
        try
        {
            // // create indexes for serials collection 
            // string indexesJson = @"[
            // { ""keys"": { ""serial_name"": 1 }, ""options"": { ""unique"": true } },
            // ]";
            // await db.CreateMultipleIndexesAsync(indexesJson,"serials");



            // // create indexes for email table where email_id is indexed with unique criteria
            // indexesJson = @"[
            // { ""keys"": { ""email_id"": 1 }, ""options"": { ""unique"": true } }
            // ]";
            // await db.CreateMultipleIndexesAsync(indexesJson, "email_otp");

            // // create for email_messages
            // // otp type should have values 1=registration_otp, 2=authentication_otp, 
            // string jsonSchemaString = @"{
            // 'bsonType': 'object',
            // 'required': ['full_name', 'email_id','otp','valid_till','otp_type'], 
            // 'properties': {
            //     'otp': {
            //         'bsonType': 'int',
            //         'minimum': 0,
            //         'description': 'The \'otp\' field must be an Integer.'
            //     },
                // 'otp_type': {
                //     'bsonType': 'int',
                //     'minimum': 0,
                //     'description': 'The \'otp_type\' field must be an Integer.'
                // },                
                // 'email_id': {
                //     'bsonType': 'string',
                //     'pattern': '^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-zA-Z]{2,}$',
                //     'description': 'The \'email\' field must be a valid email address.'
                // },
                // 'valid_till': {
                //     'bsonType': 'date',
                //     'description': 'The \'dateTimeField\' field must be a datetime.'
                // }
            // }
            // }";

            // await db.SetCollectionValidationRuleAsync(jsonSchemaString, "email_otp");

        }
        catch(Exception ex)
        {
            throw;
        }
        
    }

    public static async Task log(Int32 errorID,String errorMessage)
    {
        
    }
}