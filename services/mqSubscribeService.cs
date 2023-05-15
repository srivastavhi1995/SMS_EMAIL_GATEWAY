
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
                    DateTime dateTime = DateTime.ParseExact(DateTime.UtcNow.ToString(), "dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                    string formattedDate = dateTime.ToString("yyyy-MM-ddTHH:mm:ss");
                    filterJson = $@"{{
                    'mobile_no': '{recievedData["mobile_no"].ToString()}',
                    'guid': '{recievedData["guid"].ToString()}',
                    'valid_till': {{ '$gt':{{'$date':'{formattedDate}'}}}}
                    }}";
                }
                else if (recievedData["auth_fields"].ToString() == "only_email")
                {
                    DateTime dateTime = DateTime.ParseExact(DateTime.UtcNow.ToString(), "dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                    string formattedDate = dateTime.ToString("yyyy-MM-ddTHH:mm:ss");
                    filterJson = $@"{{
                    'email_id': '{recievedData["email_id"].ToString()}',
                    'guid': '{recievedData["guid"].ToString()}',
                    'valid_till': {{ '$gt':{{'$date':'{formattedDate}'}}}}
                    }}";


                }
                else if (recievedData["auth_fields"].ToString() == "email_and_mobile")
                {
                    DateTime dateTime = DateTime.ParseExact(DateTime.UtcNow.ToString(), "dd-MM-yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                    string formattedDate = dateTime.ToString("yyyy-MM-ddTHH:mm:ss");
                    filterJson = $@"{{
                    'email_id': '{recievedData["email_id"].ToString()}',
                    'mobile_no': '{recievedData["mobile_no"].ToString()}',
                    'guid': '{recievedData["guid"].ToString()}',
                    'valid_till': {{ '$gt':{{'$date':'{formattedDate}'}}}}
                    }}";
                }

                var projectionJson = @"{
                'otp': 1
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
                            {"valid_till",DateTime.UtcNow.AddMinutes(10)}
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
                var msg = "Six Digit OTP is " + otp;

                if (recievedData["auth_fields"].ToString() == "only_mobile")
                {
                    //_ss.SendSMS(mobile_no, msg); 
                    return true;
                }
                else if (recievedData["auth_fields"].ToString() == "only_email")
                {
                    return true;
                    //_ms.sendMail(email_id, "OTP from Source",msg);                    
                }
                else if (recievedData["auth_fields"].ToString() == "mobile_and_email")
                {
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

        _rs.subscribeQueue("generate_otp_and_send_q", false, onMsgRecvdSend);
        bool onMsgRecvdSend(Dictionary<string, object> recievedData)
        {
            try
            {
                //Dictionary<string,object> headers = (Dictionary<string,object>)recievedProps["headers"];
                var mobile_no = recievedData["mobile_no"].ToString();
                var country_code = recievedData["country_code"].ToString();
                var message = recievedData["message"].ToString();
                // var message = "SourceDotCom";
                _ss.SendSMS(mobile_no, message);
                return true;// send acknoledgement 

            }
            catch (SmtpException ex)
            {
                Console.WriteLine(ex.ToString());
                return false;// send negative ack to rabbit

            }
        }

    }

}