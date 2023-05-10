
using System.Collections.Concurrent;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

// this is taking care of two activities 
// 1. one is listining for a message : 
// 2. when this class is initialized the constructor is passed with two parameters 
// 3. parameters "settings" and "messageFunction" to be called when some message in subscribed que is recieved.
// 4. the constructor calls Create Channel whoch is used to listin/subscribe as well as publish messages
// 5. the createChannel also implements methors to acknoledge and save messages in dictionary till acknoledgement is recieved
// 6. if a positive acokneledgement is recieved cleanAckMsg function is called which clears up the message from local dictionary
// 7. if negative acknoledgement is recieved republishMessage function is called whioch will republish the same message again.

public class rabbitSrvc
{

    private IConnection _connection;
    private  IModel _channel;
    private  Dictionary<string, string> _settings; // stores settings in a coma seperated  
    private  ConcurrentDictionary<ulong, Dictionary<string, object>> _unAckQueue; // stores all unacknoledge messages 
    private  Func<object, bool> _onPublishResponse;

    public rabbitSrvc(string settingName) // constructor
    {
        IConfiguration appsettings = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build(); // this is to read the rabbit settings from settings file 
        Dictionary<string, string> settings = new Dictionary<string, string>();
        settings["HostName"] = appsettings[settingName+":HostName"];
        settings["UserName"] = appsettings[settingName+":UserName"];
        settings["VirtualHost"] = appsettings[settingName+":VirtualHost"];
        settings["Password"] = appsettings[settingName+":Password"];
        settings["Port"] = appsettings[settingName+":Port"];

        _settings = settings;
        // BELOW ARE CONTENT OF SETTINGS
        // HostName = "180.180.180.100",
        // UserName ="echs_mobile_subscribe",
        // VirtualHost="echs_api_broker",
        // Password="tech*1978",
        // Port=5672
        createChannel();
    }

    private bool createChannel()
    {
        try
        {
            if (_connection is null || _connection.IsOpen == false)
            {
                var factory = new ConnectionFactory
                {
                    HostName = _settings["HostName"],
                    UserName = _settings["UserName"],
                    VirtualHost = _settings["VirtualHost"],
                    Password = _settings["Password"],
                    Port = 5672
                };
                // *******check if connection is actiuve if active do not create another else create connection
                _connection = factory.CreateConnection();
            }

            //*********check channel if non existant create it else use existing
            if (_channel is null || _channel.IsOpen == false)
            {
                _unAckQueue = new ConcurrentDictionary<ulong, Dictionary<string, object>>(); // this should be initialized only if channel is closed 
                _channel = _connection.CreateModel(); // initialize the global variable                
                _channel.ConfirmSelect();
                _channel.BasicAcks += (sender, ea) =>
                {
                    // code when message is confirmed/acknoledged
                    cleanAckMsg(ea.DeliveryTag, ea.Multiple); // this gets called when message publish suceeds (publishMessage()->_channel.BasicPublish)
                };
                _channel.BasicNacks += (sender, ea) =>
                {
                    //code when message is nack-ed/error/negative acknoledgement 
                    republishMessage(ea.DeliveryTag, ea.Multiple); // this is called when message publish fails (publishMessage()->_channel.BasicPublish)

                };
            }

            return true;
        }
        catch (Exception ex)
        {
            return false;
        }

    }

    public bool subscribeQueue(string queueName, bool autoAcknoledgement, Func<Dictionary<string, object>, bool> onMessageRecieved)
    {
        try
        {
            if (!createChannel()) return false; // if channel is not created or dosent exist
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, eventArgs) =>
            {
                var body = eventArgs.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                Dictionary<string, object> revdData = JsonSerializer.Deserialize<Dictionary<string, object>>(message);
                //Dictionary<string,object> props = new  Dictionary<string,object>();
                //props["headers"]= eventArgs.BasicProperties.Headers;

                if (onMessageRecieved(revdData))
                    _channel.BasicAck(eventArgs.DeliveryTag, false); // send confirmation to rabbit that message was processed successfully  // settog second [atam to true is for bulk messages]
                else
                    _channel.BasicNack(eventArgs.DeliveryTag, false, true); // send message to rabbit that message was not processed and to reque it
                //Console.WriteLine($"Message received: {message}");
            };
            _channel.BasicConsume(queue: queueName, autoAck: autoAcknoledgement, consumer: consumer);
            return true;
        }
        catch (Exception ex)
        {
            return false;
        }



    }

    // publish a message to an exchange with a routing key
    public bool publishMessage(string exchangeName, string routingKey, Dictionary<string, object> Headers, byte[] msgBodyBytes)
    {
        try
        {
            if (!createChannel()) return false; // if channel creation failed
            IBasicProperties props = _channel.CreateBasicProperties();
            props.ContentType = "text/plain";
            props.DeliveryMode = 2;
            props.Headers = Headers;
            Dictionary<string, object> msg = new Dictionary<string, object>();
            msg["Exchange"] = exchangeName;
            msg["RoutingKey"] = routingKey;
            msg["props"] = props;
            msg["msgBodyBytes"] = msgBodyBytes;
            _unAckQueue.TryAdd(_channel.NextPublishSeqNo, msg); // save this under unacknoledged messages                
            _channel.BasicPublish(exchangeName, routingKey, props, msgBodyBytes);
            return true;
        }
        catch
        {
            return false;
        }
    }


    // publish a message to an exchange with a routing key

    // this function is called to remove message from local list if the message was recieved by rabbit server
    void cleanAckMsg(ulong sequenceNumber, bool multiple)
    {
        if (multiple)
        {
            var confirmed = _unAckQueue.Where(k => k.Key <= sequenceNumber);
            foreach (var entry in confirmed)
            {
                _unAckQueue.TryRemove(entry.Key, out _);
            }
        }
        else
        {
            _unAckQueue.TryRemove(sequenceNumber, out _);
        }
    }

    // republish a message if message was not recieved or negative acknoledgement was recieved from rabbit
    private bool republishMessage(ulong sequenceNumber, bool multiple)
    {
        try
        {
            if (!createChannel()) return false; //_onPublishResponse(false);; // if channel creation failed
            if (multiple)
            {
                var confirmed = _unAckQueue.Where(k => k.Key <= sequenceNumber);
                foreach (var entry in confirmed)
                {
                    Dictionary<string, object> msg = _unAckQueue[entry.Key];
                    _channel.BasicPublish((string)msg["Exchange"], (string)msg["RoutingKey"], (IBasicProperties)msg["props"], (byte[])msg["msgBodyBytes"]);
                }
            }
            else
            {
                Dictionary<string, object> msg = _unAckQueue[sequenceNumber];
                _channel.BasicPublish((string)msg["Exchange"], (string)msg["RoutingKey"], (IBasicProperties)msg["props"], (byte[])msg["msgBodyBytes"]);
            }
            return true;
        }
        catch
        {
            return false;
        }


    }

}