using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;

public class serviceEmail
{
    String _fromSMTPUser;
    String _smtpServer;
    String _smtpPort;
    String _smtpUserPassword;

    public serviceEmail(string settingsKey)
    {
        IConfiguration appsettings = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build(); // this is to read the rabbit settings from settings file 
        _fromSMTPUser = appsettings[settingsKey + ":Mail"];
        _smtpServer = appsettings[settingsKey + ":SMTP"];
        _smtpPort = appsettings[settingsKey + ":Port"];
        _smtpUserPassword = appsettings[settingsKey + ":Password"];

    }
    public async Task sendMail(String emailId, String subject, String body)
    {
        return;

        try
        {
            MailAddress to = new MailAddress(emailId);
            MailAddress from = new MailAddress(_fromSMTPUser);
            MailMessage message = new MailMessage(from, to);
            message.Subject = subject;
            message.Body = body;
            SmtpClient client = new SmtpClient(_smtpServer);
            client.Port = Int32.Parse(_smtpPort);
            client.Credentials = new NetworkCredential(_fromSMTPUser, _smtpUserPassword);
            client.EnableSsl = true;
            // code in brackets above needed if authentication required
            client.Send(message);
        }
        catch(Exception ex)
        {
            throw;
        }

    }

}