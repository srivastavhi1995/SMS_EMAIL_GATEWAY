
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MySql.Data.MySqlClient;

public class mqSubscribeService
{
    private rabbitSrvc _rs = new rabbitSrvc("rabbit"); // more objects can be created for diffrent rabbit servers and settings
    private dbServiceMongo _ds = new dbServiceMongo("mongodb");
    private serviceEmail _ms = new serviceEmail("mail");
    private serviceSmsCdac _ss = new serviceSmsCdac("sms_cdac");
    private serviceSmsCdac _ssCdac = new serviceSmsCdac("sms_cdac");
    private serviceSmsSource _ss_sdc = new serviceSmsSource("sourceSMS");

    private String _smsGatewayName = "mail";
    private String _emailGatewayName = "sms";

    //private readonly dbSettingsMongo _ds;
    public mqSubscribeService()
    {
        IConfiguration appsettings = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build(); // this is to read the rabbit settings from settings file 
        _smsGatewayName = appsettings["service_config:sms_gateway"];
        _emailGatewayName = appsettings["service_config:email_gateway"];

        _rs.subscribeQueue("generate_otp_and_send_q", false, onMsgRecvdGenOtpSend);

        bool onMsgRecvdGenOtpSend(Dictionary<string, object> recievedData)
        {
            try
            {
                var otp = new Random().Next(100000, 999999);
                var filterJson = "";

                if (recievedData["auth_fields"].ToString() == "only_mobile")
                {
                   // DateTime dateTime = DateTime.ParseExact(DateTime.UtcNow.ToString(), "dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                   // string formattedDate = dateTime.ToString("yyyy-MM-ddTHH:mm:ss");
                    string formattedDate =DateTime.UtcNow.ToString();
                    filterJson = $@"{{
                    'mobile_no': '{recievedData["mobile_no"].ToString()}',
                     'otp_type': '1', 
                    'guid': '{recievedData["guid"].ToString()}',
                    'valid_till': {{ '$gt':{{'$date':'{new BsonDateTime(DateTime.UtcNow)}'}}}}
                    }}";
                }
                else if (recievedData["auth_fields"].ToString() == "only_email")
                {
                    DateTime dateTime = DateTime.ParseExact(DateTime.UtcNow.ToString(), "dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                    string formattedDate = dateTime.ToString("yyyy-MM-ddTHH:mm:ss");
                    filterJson = $@"{{
                    'email_id': '{recievedData["email_id"].ToString()}',
                     'otp_type': '2',
                    'guid': '{recievedData["guid"].ToString()}',
                    'valid_till': {{ '$gt':{{'$date':'{new BsonDateTime(DateTime.UtcNow)}'}}}}
                    }}";


                }
                else if (recievedData["auth_fields"].ToString() == "email_and_mobile")
                {
                    // DateTime dateTime = DateTime.ParseExact(DateTime.UtcNow.ToString(), "dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                    string formattedDate = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss");
                    filterJson = $@"{{
                    'email_id': '{recievedData["email_id"].ToString()}',
                    'otp_type': '1',
                    'mobile_no': '{recievedData["mobile_no"].ToString()}',
                    'guid': '{recievedData["guid"].ToString()}',
                    'valid_till': {{ '$gt':{{'$date':'{new BsonDateTime(DateTime.UtcNow)}'}}}}
                    }}";
                }

                var projectionJson = @"{
                'otp': '1'
                }";

                BsonDocument filters = BsonDocument.Parse(filterJson);
                BsonDocument projection = BsonDocument.Parse(projectionJson);

                mongoRequest mRequest = new mongoRequest();
                mRequest.newRequestStatement(0, "(email_sms_svc)-(otp)", filters, projection, null, null);

                //mStatements._mStatements

                //mongoStatements ms = new mongoStatements(0,"(email_sms_svc)-(otp)",filters,projection,null,null); // initialize 
                //mStatements.Add(new mongoStatements(0, "(email_sms_svc)-(otp)", filters, projection, null, null));

                // getAwaiter.getResult is used here as this method dosent have async or task 
                mongoResponse mResponse = _ds.executeStatements(mRequest, false).GetAwaiter().GetResult();
                var result = mResponse._resStatements[0]._selectedResults;

                if (result.Count == 0) // if record not found
                {
                    //Console.WriteLine("Record not found");
                    var documents = new[]
                    {
                        new BsonDocument
                        {
                            {"email_id", recievedData["email_id"].ToString()},
                            {"mobile_no", recievedData["mobile_no"].ToString()}, // this includes country code and mobile no
                            {"country_code",recievedData["country_code"].ToString()},
                            {"guid",recievedData["guid"].ToString()},
                            {"app_id",recievedData["app_id"].ToString()},
                            {"otp",otp},
                            {"otp_type",recievedData["otp_type"].ToString()},
                            {"valid_till",new BsonDateTime(DateTime.UtcNow.AddMinutes(10))}
                        }
                    };

                    mRequest = new mongoRequest();
                    mRequest.newRequestStatement(1, "(email_sms_svc)-(otp)", null, null, null, documents);
                    _ds.executeStatements(mRequest, false);
                }
                else
                {
                    //var found_docs = result.ToList();
                    //var found_docs = result;
                    otp = Int32.Parse(result[0]["otp"].ToString());
                }

                var mobile_no = recievedData["mobile_no"].ToString();
                var email_id = recievedData["email_id"].ToString();
                //var msg = "Six Digit OTP is " + otp;
               var msg = otp.ToString() + "OTP for MedsKey Registration is "+otp.ToString()+ ".- SOURCEDOTCOM PVT LTD";
                if (recievedData["auth_fields"].ToString() == "only_mobile")
                {
                    _ss_sdc.SendSMS(mobile_no, msg, otp.ToString()); 
                    return true;
                }
                else if (recievedData["auth_fields"].ToString() == "only_email")
                {
                    return true;
                    //_ms.sendMail(email_id, "OTP from Source",msg);                    
                }
                else if (recievedData["auth_fields"].ToString() == "email_and_mobile")
                {

                     _ss_sdc.SendSMS(mobile_no, msg, otp.ToString()); 
                    return true;
                    //_ss.SendSMS(mobile_no, msg);
                    //_ms.sendMail(email_id, "OTP from Source", msg);                  
                }
                return true;// send acknoledgement 

                // }
            }
            catch (SmtpException ex)
            {
                Console.WriteLine(ex.ToString());
                return false;// send negative ack to rabbit

            }
        }

        //For mobile update
        _rs.subscribeQueue("send_medskey_change_mobile_q", false, onMsgRecvdUpdateOtpSend);

        bool onMsgRecvdUpdateOtpSend(Dictionary<string, object> recievedData)
        {
            try
            {
                var otp = new Random().Next(100000, 999999);
               BsonDocument filter=new BsonDocument{
                {"_id",ObjectId.Parse(recievedData["_req_id"].ToString())}

               };
                BsonDocument updates = new BsonDocument
            {
                {"$set", new BsonDocument
                    {
                        {"_otp",otp},
                        {"_status", 1},
                         {"_valid_till",new BsonDateTime(DateTime.UtcNow.AddMinutes(10))}
                    }
                }
            };
               mongoResponse mResponse = new mongoResponse();
                mongoRequest mRequest = new mongoRequest();
                mRequest.newRequestStatement(3, "e_change_mobile_requests", filter, null, updates, null);
                    mResponse =  _ds.executeStatements(mRequest, false).GetAwaiter().GetResult();
                 var res=   mResponse._resStatements[0]._selectedResults;

                var mobile_no = recievedData["_new_mobile_no"].ToString();
                //var msg = "Six Digit OTP is " + otp;
                var msg = otp.ToString() + "OTP for MedsKey Registration is "+otp.ToString()+ ".- SOURCEDOTCOM PVT LTD";
               
                    _ss_sdc.SendSMS(mobile_no, msg, otp.ToString()); 
                    return true;
            }
            catch (SmtpException ex)
            {
                Console.WriteLine(ex.ToString());
                return false;// send negative ack to rabbit

            }
        }

       

    }

}