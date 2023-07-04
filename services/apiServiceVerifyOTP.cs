
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using MySql.Data.MySqlClient;
using System.Globalization;

public class apiServiceVerifyOTP
{
    private readonly dbServiceMongo _ds; // this can be changed if more connections are required by this service like below
    public apiServiceVerifyOTP()
    {
        _ds = new dbServiceMongo("mongodb");
    }

    public async Task<responseData> VerifyOTP(requestData req)
    {
        responseData resData = new responseData();
        resData.rStatus = 0;
        resData.rData["rCode"] = 0;
        resData.rData["rMessage"] = "Success";

        try
        {

            // var filterJson = $@"{{
            // 'mobile_no': '{req.addInfo["country_code"].ToString().Trim()+req.addInfo["mobile_no"].ToString().Trim()}',
            // 'guid': '{req.addInfo["guid"].ToString()}',
            // 'valid_till': {{'$gt': '{DateTime.UtcNow}'}}
            // }}";
            var filterJson = "";
            var projectionJson = "";

            if (req.addInfo["auth_fields"].ToString() == "only_mobile") // only mobile no
            {
                //DateTime dateTime = DateTime.ParseExact(DateTime.UtcNow.ToString(), "dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                //string formattedDate = dateTime.ToString("yyyy-MM-ddTHH:mm:ss");
                string formattedDate = DateTime.UtcNow.ToString();
                filterJson = $@"{{ 
                    'country_code': '{req.addInfo["country_code"].ToString()}',
                'mobile_no': '{req.addInfo["mobile_no"].ToString().Trim()}',
                'guid': '{req.addInfo["guid"].ToString()}',
                'valid_till': {{ '$gt':{{'$date':'{new BsonDateTime(DateTime.UtcNow)}'}}}},
                'otp':{req.addInfo["otp"].ToString()},
                'otp_type':'1',
                }}";

                projectionJson = @"{
                'mobile_no': 1,
                }";
            }
            else if (req.addInfo["auth_fields"].ToString() == "only_email") // only email id 
            {
                //DateTime dateTime = DateTime.ParseExact(DateTime.UtcNow.ToString(), "dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                //string formattedDate = dateTime.ToString("yyyy-MM-ddTHH:mm:ss");
                string formattedDate = DateTime.UtcNow.ToString();
                filterJson = $@"{{
                'email_id': '{req.addInfo["email_id"].ToString()}',
                'guid': '{req.addInfo["guid"].ToString()}',
                'valid_till': {{ '$gt':{{'$date':'{new BsonDateTime(DateTime.UtcNow)}'}}}},
                 'otp_type':'2',
                'otp':{req.addInfo["otp"].ToString()}
                }}";

                projectionJson = @"{
                'email_id': 1
                }";
            }
            else if (req.addInfo["auth_fields"].ToString() == "email_and_mobile") // email id and mobile
            {
                // DateTime dateTime = DateTime.ParseExact(DateTime.UtcNow.ToString(), "dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                // string formattedDate = dateTime.ToString("yyyy-MM-ddTHH:mm:ss");
                //     string formattedDate = DateTime.UtcNow.ToString();
                filterJson = $@"{{
                'email_id': '{req.addInfo["email_id"].ToString()}',
                'country_code': '{req.addInfo["country_code"].ToString()}',
                'mobile_no': '{req.addInfo["mobile_no"].ToString().Trim()}',
                'guid': '{req.addInfo["guid"].ToString()}',
                 'otp_type':'1',
                'valid_till': {{ '$gt':{{'$date':'{new BsonDateTime(DateTime.UtcNow)}'}}}},
                'otp':{req.addInfo["otp"].ToString()}
                }}";

                projectionJson = @"{
                'email_id': 1,
                'mobile_no': 1
                }";
            }


            //var result = _mongoCollection.Find(filter, new FindOptions<BsonDocument, BsonDocument> { Projection = projection });
            //var result = _ds.findRecords(filterJson, projectionJson, "(email_sms_svc)-(otp)");
            BsonDocument filters = BsonDocument.Parse(filterJson);
            BsonDocument projection = BsonDocument.Parse(projectionJson);

            // var mStatements = new List<mongoStatements>();
            // mongoStatements ms = new mongoStatements(); // initialize 
            // ms.statementType = 0; // insert
            // ms.collectionName = "(email_sms_svc)-(otp)"; // table name
            //                                              //ms.statements = documents;
            // ms.filters = filters;
            // ms.requiredFields = projection;
            // mStatements.Add(ms);

            // // getAwaiter.getResult is used here as this method dosent have async or task 
            // mongoReturn mRet = _ds.executeStatements(mStatements, false).GetAwaiter().GetResult();
            // var result = mRet.mRetStatements[0].selectedResults;

            mongoRequest mRequest = new mongoRequest();
            mRequest.newRequestStatement(0, "(email_sms_svc)-(otp)", filters, projection, null, null);
            mongoResponse mResponse = _ds.executeStatements(mRequest, false).GetAwaiter().GetResult();
            var result = mResponse._resStatements[0]._selectedResults;


            if (result.Count > 0)
            {
                resData.rData["rMessage"] = "Valid OTP";
                // check for email OTP 
                try
                {

                    if (req.addInfo["email_otp"].ToString() != "")
                    {
                        filterJson = $@"{{
                'email_id': '{req.addInfo["email_id"].ToString()}',
                'country_code': '{req.addInfo["country_code"].ToString()}',
                'mobile_no': '{req.addInfo["mobile_no"].ToString().Trim()}',
                'guid': '{req.addInfo["guid"].ToString()}',
                 'otp_type':'2',
                'valid_till': {{ '$gt':{{'$date':'{new BsonDateTime(DateTime.UtcNow)}'}}}},
                'otp':{req.addInfo["email_otp"].ToString()}
                }}";

                        projectionJson = @"{
                'email_id': 1,
                'mobile_no': 1
                }";

                        filters = BsonDocument.Parse(filterJson);
                        projection = BsonDocument.Parse(projectionJson);
                        mRequest = new mongoRequest();
                        mRequest.newRequestStatement(0, "(email_sms_svc)-(otp)", filters, projection, null, null);
                        mResponse = _ds.executeStatements(mRequest, false).GetAwaiter().GetResult();
                        result = mResponse._resStatements[0]._selectedResults;
     if (result.Count > 0)
            {
                 resData.rData["rMessage"] = "Valid OTP";
            }else{

                 resData.rData["rMessage"] = "Invalid Email OTP";
            } 
                    }


                }
                catch (Exception exx)
                {

                }


            }
            else
            {
                resData.rData["rCode"] = 102;
                resData.rData["rMessage"] = "Invalid OTP";
            }

            //IMongoCollection<Object> _testCollection  = _mongoDB.GetCollection<Object>("test_collection");
            //resData.rData["rMessage"] = "Data Updated";

        }
        catch (Exception ex)
        {
            resData.rStatus = 100;
            resData.rData["rCode"] = 102;
            resData.rData["rMessage"] = "Error in Validating OTP. Please try again";
        }
        return resData;

    }



}
