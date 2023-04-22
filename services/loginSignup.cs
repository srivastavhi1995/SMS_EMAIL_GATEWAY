
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using MySql.Data.MySqlClient;


public class loginSignup
{
    smsService sms =new smsService();
    dbServices ds=new dbServices();
    IConfiguration appsettings = new ConfigurationBuilder().AddJsonFile("Properties/appsettings.json").Build();

    private readonly dbSettings _ds;
    private readonly IMongoCollection<personal> _personalCollection;
    private  IMongoDatabase _mongoDB;
    public loginSignup(IOptions<dbSettings> ds)
    {

try{
            _ds = ds.Value; // this passes the initialised class for database in Configure before AddSingleton
        }
catch(Exception ex)
{
    Console.Write(ex);
}

        // MongoClient mongoClient = new MongoClient(ds.Value.connStr);
        // _mongoDB = mongoClient.GetDatabase(ds.Value.dbName);
        // var c = ds.Value.hello();
        // _personalCollection = _mongoDB.GetCollection<personal>("test_collection");
    }



    public async Task<responseData> ValidateUser(requestData reqData)
    {
        responseData resData=new responseData();

        MySqlParameter[] myParams = new MySqlParameter[] { new MySqlParameter("@userid",reqData.addInfo["userid"].ToString()),new MySqlParameter("@guid",reqData.addInfo["guid"].ToString()),new MySqlParameter("@pass",reqData.addInfo["pass"].ToString())};
        var sq = "CALL verifyUser(@userid,@guid,@pass);";

        //string sq="select mobile_no,guid from reg_users where mobile_no=@mobile and guid = @guid and password=@pass;"; // CHECK IF BEN ID IS VALID AND RETURN MOBILE NUMBER ALONG WITH AN OTP ... SAVE THE OTP AND NUMBER IN A FILE WITH TEPMLATE ID
        //MySqlParameter[] myParams = new MySqlParameter[] { new MySqlParameter("@mobile",reqData.addInfo["user"]),new MySqlParameter("@pass",reqData.addInfo["pass"]),new MySqlParameter("@guid",reqData.addInfo["guid"])};
        var dbdata = ds.executeSQL(sq,myParams);
        if(dbdata==null) // error occured
        {
            resData.rStatus=100; // database error this error is caught ar app level
            resData.rData["Error"]=errors.err[100];        
        }
        else
        {
            if(dbdata[0][0][0].ToString()!= "0") // valid user
            {
                resData.rData["rCode"]=dbdata[0][0][0];
                resData.rData["rMessage"]=dbdata[0][0][1];                
            }
            else
            {
                var unm=dbdata[0][0][3].ToString(); // mobile number
                var uid=dbdata[0][0][2].ToString(); // uid
                var guid=dbdata[0][0][5].ToString(); // guid
                var email=dbdata[0][0][4].ToString(); // email

                //GENERATE TOKEN HERE IF USER IS VALID
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier,uid),                    
                    new Claim(ClaimTypes.Name, unm),
                    new Claim(ClaimTypes.SerialNumber, guid),
                    new Claim(ClaimTypes.Email,email)
                };

                var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(appsettings["Jwt:Key"]));
                var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256Signature);
                var tokenDescriptor = new JwtSecurityToken(issuer: appsettings["Jwt:Issuer"], audience: appsettings["Jwt:Audience"], claims : claims,
                    expires: DateTime.Now.AddMinutes(Int16.Parse(appsettings["Jwt:ExpiryDuration"])), signingCredentials: credentials);
                var token = new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);

                resData.rData["rCode"]=dbdata[0][0][0];
                resData.rData["rMessage"]=dbdata[0][0][1]; 
                resData.rData["jwt"]=token; //adding a key/value using the Add() method                
                resData.eventID=reqData.eventID;
            } 
        }
        return resData;
    }

    public async Task<responseData> sendOTP(requestData req)
    {
        //CALL `echs_otp`.`sendOTP`(9777777777, 45, 1, "878787878787");
        responseData resData=new responseData();
        MySqlParameter[]  myParams = new MySqlParameter[] {
            
            new MySqlParameter("@mobile",Int64.Parse(req.addInfo["mobile"].ToString())),
            new MySqlParameter("@guid",req.addInfo["guid"].ToString()),
            //new MySqlParameter("@tid",req.addInfo["msgid"].ToString()),
            new MySqlParameter("@tid",req.addInfo["tid"].ToString())
            
            };
    

            //var sq = "CALL sendOTP(@mobile,@guid);";
            var sq= "CALL echs_otp.sendOTP(@mobile, @tid, 1, @guid);";
            var dbdata = ds.executeSQL(sq,myParams);
            if(dbdata==null) // error occured
                resData.rStatus=101; // second database error
            else
            {
                if(dbdata[0][0][0].ToString()=="1001") // if duplicate registration exists
                {
                    resData.rData["rCode"]=100;
                    resData.rData["rMessage"]="Mobile Number already registered. Try to login using the same";                    
                }
                else
                {
                    resData.rData["rCode"]=0;
                    resData.rData["rMessage"]="OTP Sent";                    
                    //string st= "Dear User, " + Otp + " is your OTP for registering on SehatOPD. The OTP is valid for the next 15 minutes. JAI HIND.";
                    string resp = sms.SendOTP(dbdata[0][0][1].ToString(),dbdata[0][0][0].ToString());

                }                  
            }      
        
        // based on the response from the server log it in the background.
        return resData;
        //Task.FromResult(_contacts.FirstOrDefault(x => x.ContactId == id));
    }
    public async Task<responseData>  validateOTP(requestData req)
    {
        //CALL `echs_otp`.`verifyOTP`(9777777777, 45, 2, "878787878787", 74739);
        responseData resData=new responseData();

        MySqlParameter[] myParams = new MySqlParameter[] { new MySqlParameter("@mobile",Int64.Parse(req.addInfo["mobile"].ToString())),new MySqlParameter("@guid",req.addInfo["guid"].ToString()),new MySqlParameter("@password",req.addInfo["pass"].ToString()),new MySqlParameter("@otp",Int64.Parse(req.addInfo["otp"].ToString()))};
        // now call the que if data insert suceeded .....
        var sq = "CALL verifyOTP(@mobile,@guid,@otp,@password);";
        var dbdata = ds.executeSQL(sq,myParams);
        if(dbdata==null) // error occured
            resData.rStatus=100; // database error
        else
        {
                resData.rData["rCode"]=dbdata[0][0][0].ToString();
                resData.rData["rMessage"]=dbdata[0][0][1].ToString();                                           
        }

        return resData;
    }

    public async Task<responseData> getDetailsMongo(requestData req)
    {
        responseData resData = new responseData();
        resData.rStatus = 0;
        resData.rData["rCode"]=0;
        resData.rData["rMessage"]="Success";
        // Assume that the "_mongoDB" object is already initialized
        IMongoCollection<BsonDocument> testCollection = _mongoDB.GetCollection<BsonDocument>("test_collection");

        // Assume that the "_mongoDB" object is already initialized
        //IMongoCollection<BsonDocument> testCollection = _mongoDB.GetCollection<BsonDocument>("test_collection");

        // Get a cursor to the result set
        var cursor = testCollection.FindSync(new BsonDocument());

        // Loop through the result set
        while (cursor.MoveNext())
        {
            // Get the current batch of documents
            var batch = cursor.Current;

            // Loop through the documents in the current batch
            foreach (var document in batch)
            {
                // Convert the BsonDocument to a JSON string and print it out
                Console.WriteLine(document.ToJson());
            }
        }
        //IMongoCollection<Object> _testCollection  = _mongoDB.GetCollection<Object>("test_collection");
        resData.rData["rMessage"] = testCollection.AsQueryable().ToJson();
        return resData;


        // MySqlParameter[] myParams = new MySqlParameter[] { new MySqlParameter("@mobile", Int64.Parse(req.addInfo["mobile"].ToString())), new MySqlParameter("@guid", req.addInfo["guid"].ToString()), new MySqlParameter("@password", req.addInfo["pass"].ToString()), new MySqlParameter("@otp", Int64.Parse(req.addInfo["otp"].ToString())) };
        // // now call the que if data insert suceeded .....
        // var sq = "CALL verifyOTP(@mobile,@guid,@otp,@password);";
        // var dbdata = ds.executeSQL(sq, myParams);
        // if (dbdata == null) // error occured
        //     resData.rStatus = 100; // database error
        // else
        // {
        //     resData.rData["rCode"] = dbdata[0][0][0].ToString();
        //     resData.rData["rMessage"] = dbdata[0][0][1].ToString();
        // }

        //return resData;
    }





    public async Task<responseData> insertTest(requestData req)
    {
        responseData resData = new responseData();
        resData.rStatus = 0;
        resData.rData["rCode"] = 0;
        resData.rData["rMessage"] = "Success";
        try
        {
            var documents = new[]
            {
                new BsonDocument
                {
                    {"field1", "value1"},
                    {"field2", 1},
                    {"field3", new BsonDocument("subfield", "subvalue")}
                },
                new BsonDocument
                {
                    {"field1", "value2"},
                    {"field2", 2},
                    {"field3", new BsonDocument("subfield", "subvalue")}
                },
                new BsonDocument
                {
                    {"field1", "value3"},
                    {"field2", 3},
                    {"field3", new BsonDocument("subfield", "subvalue")}
                }
            };

            //await _ds.insertData("new_table",documents);
            var mStatements = new List<mongoStatements>();

            mongoStatements ms = new mongoStatements(); // initialize 
            ms.statementType = 0; // insert
            ms.collectionName = "dum_dum"; // table name
            ms.statements = documents;
            mStatements.Add(ms);

            ms = new mongoStatements(); // initialize 
            ms.statementType = 0; // insert
            ms.collectionName = "dum_dum2"; // table name
            ms.statements = documents;
            mStatements.Add(ms);

            await _ds.executeStatements(mStatements);

            //IMongoCollection<Object> _testCollection  = _mongoDB.GetCollection<Object>("test_collection");
            resData.rData["rMessage"] = "Data Inserted";

        }
        catch(Exception ex)
        {
            resData.rStatus = 199;
            resData.rData["rMessage"] = "Error in Inserting Data. Please try again";
        }
        return resData;

    }


    public async Task<responseData> updateTest(requestData req)
    {
        responseData resData = new responseData();
        resData.rStatus = 0;
        resData.rData["rCode"] = 0;
        resData.rData["rMessage"] = "Success";
        try
        {
            // where clause
            BsonDocument filters = new BsonDocument
            {
                {"serialName", "patientSerial"},
            };

            // set details
            BsonDocument updates = new BsonDocument
            {
                {"$inc", new BsonDocument("value", 1)} // increments value by 1
            };


            //await _ds.insertData("new_table",documents);
            var mStatements = new List<mongoStatements>();

            mongoStatements ms = new mongoStatements(); // initialize 
            ms.statementType = 0; // update
            ms.collectionName = "serials"; // table name
            ms.filters=filters;
            ms.updates=updates;
            mStatements.Add(ms);

            // ms = new mongoStatements(); // initialize 
            // ms.statementType = 0; // insert
            // ms.collectionName = "dum_dum2"; // table name
            // ms.statements = documents;
            // mStatements.Add(ms);

            await _ds.executeStatements(mStatements);

            //IMongoCollection<Object> _testCollection  = _mongoDB.GetCollection<Object>("test_collection");
            resData.rData["rMessage"] = "Data Updated";

        }
        catch (Exception ex)
        {
            resData.rStatus = 199;
            resData.rData["rMessage"] = "Error in Inserting Data. Please try again";
        }
        return resData;

    }




    public async Task<responseData> createCollection(requestData req)
    {
        responseData resData = new responseData();
        resData.rStatus = 0;
        resData.rData["rCode"] = 0;
        resData.rData["rMessage"] = "Success";

        var documents = new []
        {
            new BsonDocument
            {
                {"field1", "value1"},
                {"field2", 1},
                {"field3", new BsonDocument("subfield", "subvalue")}
            },
            new BsonDocument
            {
                {"field1", "value2"},
                {"field2", 2},
                {"field3", new BsonDocument("subfield", "subvalue")}
            },
            new BsonDocument
            {
                {"field1", "value3"},
                {"field2", 3},
                {"field3", new BsonDocument("subfield", "subvalue")}
            }
        };

        var document2 = new [] {
            new BsonDocument
            {
                
            }
        };





        //await _ds.insertData("new_table",documents);
        var mStatements = new List<mongoStatements>();

        mongoStatements ms=new mongoStatements(); // initialize 
        ms.statementType=0; // insert
        ms.collectionName="dum_dum"; // table name
        ms.statements=documents;
        mStatements.Add(ms);

        ms = new mongoStatements(); // initialize 
        ms.statementType = 0; // insert
        ms.collectionName = "dum_dum2"; // table name
        ms.statements = documents;
        mStatements.Add(ms);

        await _ds.executeStatements(mStatements);



        // Assume that the "_mongoDB" object is already initialized
        //_mongoDB.create;
        IMongoCollection<BsonDocument> testCollection = _mongoDB.GetCollection<BsonDocument>("auth_collection");
        var jsonObject = new BsonDocument {
            { "title", "The Catcher in the Rye" },
            { "author", "J.D. Salinger" },
            { "year", 1951 },
            { "tags", new BsonArray { "novel", "coming-of-age" } }
        };
        
        testCollection.InsertOne(jsonObject);

        // Assume that the "_mongoDB" object is already initialized
        //IMongoCollection<BsonDocument> testCollection = _mongoDB.GetCollection<BsonDocument>("test_collection");

        // Get a cursor to the result set
        var cursor = testCollection.FindSync(new BsonDocument());

        // Loop through the result set
        while (cursor.MoveNext())
        {
            // Get the current batch of documents
            var batch = cursor.Current;

            // Loop through the documents in the current batch
            foreach (var document in batch)
            {
                // Convert the BsonDocument to a JSON string and print it out
                Console.WriteLine(document.ToJson());
            }
        }
        //IMongoCollection<Object> _testCollection  = _mongoDB.GetCollection<Object>("test_collection");
        resData.rData["rMessage"] = testCollection.AsQueryable().ToJson();
        return resData;


        // MySqlParameter[] myParams = new MySqlParameter[] { new MySqlParameter("@mobile", Int64.Parse(req.addInfo["mobile"].ToString())), new MySqlParameter("@guid", req.addInfo["guid"].ToString()), new MySqlParameter("@password", req.addInfo["pass"].ToString()), new MySqlParameter("@otp", Int64.Parse(req.addInfo["otp"].ToString())) };
        // // now call the que if data insert suceeded .....
        // var sq = "CALL verifyOTP(@mobile,@guid,@otp,@password);";
        // var dbdata = ds.executeSQL(sq, myParams);
        // if (dbdata == null) // error occured
        //     resData.rStatus = 100; // database error
        // else
        // {
        //     resData.rData["rCode"] = dbdata[0][0][0].ToString();
        //     resData.rData["rMessage"] = dbdata[0][0][1].ToString();
        // }

        //return resData;
    }



}

