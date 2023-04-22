using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Microsoft.Extensions.Configuration;

public class smsService{
    IConfiguration appsettings = new ConfigurationBuilder().AddJsonFile("Properties/appsettings.json").Build();
 /// <summary>
        /// Method to encrypt password
        /// </summary>
        /// <param name="password"></param>
        /// <returns></returns>
        public String encryptedPasswod(String password)
        {
            byte[] encPwd = Encoding.UTF8.GetBytes(password);
            //static byte[] pwd = new byte[encPwd.Length];
            HashAlgorithm sha1 = HashAlgorithm.Create("SHA1");
            byte[] pp = sha1.ComputeHash(encPwd);
            // static string result = System.Text.Encoding.UTF8.GetString(pp);
            StringBuilder sb = new StringBuilder();
            foreach (byte b in pp)
            {

                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Method to save keys as hash
        /// </summary>
        /// <param name="Username"></param>
        /// <param name="sender_id"></param>
        /// <param name="message"></param>
        /// <param name="secure_key"></param>
        /// <returns></returns>
        public String HashGenerator(String Username, String sender_id, String message, String secure_key)
        {

            StringBuilder sb = new StringBuilder();
            sb.Append(Username).Append(sender_id).Append(message).Append(secure_key);
            byte[] genkey = Encoding.UTF8.GetBytes(sb.ToString());
            //static byte[] pwd = new byte[encPwd.Length];
            HashAlgorithm sha1 = HashAlgorithm.Create("SHA512");
            byte[] sec_key = sha1.ComputeHash(genkey);           
            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < sec_key.Length; i++)
            {
                stringBuilder.Append(sec_key[i].ToString("x2"));
            }
            return stringBuilder.ToString();
        }

        /// <summary>
        /// Generate OTP
        /// </summary>
        /// <param name="phoneNumber"></param>
        /// <returns></returns>
        public string SendOTP(string phoneNumber, string message)
        {
            // appsettings["cdac:SmsServerUrl"]
            // appsettings["cdac:Password"]
            // appsettings["cdac:UserName"]
            // appsettings["cdac:SenderId"]
            // appsettings["cdac:SecureKey"]
            // appsettings["cdac:TemplateId"]

            try
            {
                Stream dataStream;

                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; //forcing .Net framework to use TLSv1.2

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(appsettings["cdac:SmsServerUrl"]);
                request.ProtocolVersion = HttpVersion.Version10;
                request.KeepAlive = false;
                request.ServicePoint.ConnectionLimit = 1;

                ((HttpWebRequest)request).UserAgent = "Mozilla/4.0 (compatible; MSIE 5.0; Windows 98; DigExt)";

                request.Method = "POST";

                String encryptedPassword = this.encryptedPasswod(appsettings["cdac:Password"]);
                String key = this.HashGenerator(appsettings["cdac:UserName"].Trim(), appsettings["cdac:SenderId"].Trim(), message.Trim(), appsettings["cdac:SecureKey"].Trim());

                String smsservicetype = "otpmsg"; //For OTP message.

                String query = "username=" + HttpUtility.UrlEncode(appsettings["cdac:UserName"].Trim()) +

                                "&password=" + HttpUtility.UrlEncode(encryptedPassword) +

                                "&smsservicetype=" + HttpUtility.UrlEncode(smsservicetype) +

                                "&content=" + HttpUtility.UrlEncode(message.Trim()) +

                                "&mobileno=" + HttpUtility.UrlEncode(phoneNumber) +

                                "&senderid=" + HttpUtility.UrlEncode(appsettings["cdac:SenderId"].Trim()) +

                                "&key=" + HttpUtility.UrlEncode(key.Trim()) +

                                "&templateid=" + HttpUtility.UrlEncode(appsettings["cdac:TemplateId"].Trim());

                byte[] byteArray = Encoding.ASCII.GetBytes(query);

                request.ContentType = "application/x-www-form-urlencoded";

                request.ContentLength = byteArray.Length;


                dataStream = request.GetRequestStream();

                dataStream.Write(byteArray, 0, byteArray.Length);

                dataStream.Close();

                WebResponse response = request.GetResponse();


                String Status = ((HttpWebResponse)response).StatusDescription;

                dataStream = response.GetResponseStream();

                StreamReader reader = new StreamReader(dataStream);

                String responseFromServer = reader.ReadToEnd();

                reader.Close();

                dataStream.Close();

                response.Close();

                return responseFromServer;
            }
            catch (Exception ex)
            {
                var msg = ex.Message;
            }
            return "Exception";

        }    
}
