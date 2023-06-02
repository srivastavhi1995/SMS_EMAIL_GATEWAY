using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http; // added by me
using Microsoft.Net.Http.Headers; // added by me
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using MySql.Data.MySqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.IO;
using System.Collections;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;


IConfiguration appsettings = new ConfigurationBuilder()
                        .AddJsonFile("appsettings.json")
                        .Build();

var builder = WebHost.CreateDefaultBuilder();

WebHost.CreateDefaultBuilder().
ConfigureServices(s =>
{
    //s.Configure<dbSettingsMongo>(appsettings.GetSection(key:"mongodb"));
    s.AddSingleton<apiServiceVerifyOTP>();// this service will validate users and generate java web tokens
    s.AddSingleton<mqSubscribeService>(); 
    // s.AddCors(options =>
    // {
    //     options.AddPolicy(name: MyAllowSpecificOrigins,
    //                     builder =>
    //                     {
    //                         builder.WithOrigins("http://localhost:8000",
    //                                             "http://www.contoso.com");
    //                     });
    // });
    s.AddAuthorization();
    s.AddAuthentication(opt => {
        opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    }).AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new ()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = appsettings["Jwt:Issuer"],
            ValidAudience = appsettings["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(appsettings["Jwt:Key"]))
        };
    });
    s.AddCors();
    s.AddControllers();
}).
Configure(app =>
{
    //app.UseHttpsRedirection();
    app.UseStaticFiles();
    app.UseRouting();


    app.UseAuthentication();
    app.UseAuthorization(); // this line has to apperar between app.UseRouting and app.UseEndPoints
   
    //app.UseCors(MyAllowSpecificOrigins);

    app.UseCors(options => 
       // options.WithOrigins("http://localhost:8000", "https://localhost:8000","https://localhost:5001","http://localhost:5000").AllowAnyHeader().AllowAnyMethod().AllowCredentials());
options.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());


    app.UseEndpoints(e=> 
    {
        try
        {
            var apiSrvVerifyOTP = e.ServiceProvider.GetRequiredService<apiServiceVerifyOTP>();
            var mqSubscribeServ = e.ServiceProvider.GetRequiredService<mqSubscribeService>();

            e.MapPost("/login", 
            [AllowAnonymous] async (HttpContext http) => 
            {
                
                //IConfiguration appsettings = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
                var body = await new StreamReader(http.Request.Body).ReadToEndAsync();
                requestData rData = JsonSerializer.Deserialize<requestData>(body); 
                if(rData.eventID=="1") // VerifyOTPSMS
                    await http.Response.WriteAsJsonAsync(await apiSrvVerifyOTP.VerifyOTP(rData));
                // else if(rData.eventID=="2") // send OTP
                //     await http.Response.WriteAsJsonAsync(await loginSignup.insertTest(rData));
                // else if(rData.eventID=="3") // verify OTP
                //     await http.Response.WriteAsJsonAsync(await loginSignup.insertTest(rData));
                
                // String  a = "Hellotext";
                // //if(ur)
                // var unm="pravinsingh";
                // var uid="001"+Guid.NewGuid().ToString();s

                // //GENERATE TOKEN HERE IF USER IS VALID
                // var claims = new[]
                // {
                //     new Claim(ClaimTypes.Name, unm),
                //     new Claim(ClaimTypes.NameIdentifier,uid)
                // };

                // var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(appsettings["Jwt:Key"]));
                // var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256Signature);
                // var tokenDescriptor = new JwtSecurityToken(issuer: appsettings["Jwt:Issuer"], audience: appsettings["Jwt:Audience"], claims : claims,
                //     expires: DateTime.Now.AddMinutes(Int16.Parse(appsettings["Jwt:ExpiryDuration"])), signingCredentials: credentials);
                // var token = new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);

                // await http.Response.WriteAsJsonAsync(new { token = token });
                // return;
            });


            e.MapGet("/bing",
                //async c => await c.Response.WriteAsJsonAsync(await contactService.GetAll()));
                //async c => await c.Response.WriteAsync("Hello how are you"));
                async c => await c.Response.WriteAsJsonAsync("{'Name':'Pravin','Age':'43'}"));
            //e.MapGet("/contacts/{id:int}",
            //e.MapGet("/bing2",
            //    async c => await c.Response.WriteAsJsonAsync(await loginSignup.insertTest(null)));
            //e.MapGet("/insert",
            //    async c => await c.Response.WriteAsJsonAsync(await loginSignup.insertTest(null)));
            //e.MapGet("/update",
            //    async c => await c.Response.WriteAsJsonAsync(await loginSignup.updateTest(null)));
                //async c => await c.Response.WriteAsJsonAsync(await contactService.GetAll()));
                //async c => await c.Response.WriteAsync("Hello how are you"));
                //async c => await c.Response.WriteAsJsonAsync("{'Name':'Pravin','Age':'43'}"));
                //e.MapGet("/contacts/{id:int}",


            e.MapPost("/bing",
                //async c => await c.Response.WriteAsJsonAsync(await testService.GetAllContacts()));
                //async c => await c.Response.WriteAsync("Hello how are you POST"));
                async c => await c.Response.WriteAsJsonAsync("{'Name':'Pravin POST','Age':'43 POST'}"));

            e.MapDefaultControllerRoute();

        }
        catch(Exception ex)
        {
            Console.Write(ex);
        }

    });
}).Build().Run();

 
public record requestData{ //request data
//SOURCE.srv.fn_CS({ rID: "F000", rData: {encData: encrypted}}, page_OS, $("#progressBarFooter")[0]);
        [Required]
        public string eventID { get; set; } //  request ID this is the ID of entity requesting the API (UTI/CDAC/CAC) this is used to pick up the respective private key for the requesting user
        [Required]
        public IDictionary<string,object> addInfo { get; set; } // request data .. previously addInfo 
}

public record responseData{ //response data
        public responseData() { // set default values here
            eventID="";
            rStatus=0;
            rData=new Dictionary<string, object>();

        }
        [Required]        
        public int rStatus{get;set;} = 0; // this will be defaulted 0 fo success and other numbers for failures
        [Required]
        public string eventID { get; set; } //  response ID this is the ID of entity requesting the
        public IDictionary<string,object> addInfo { get; set; } // request data .. previously addInfo 
        public Dictionary<string,object> rData { get; set;}
        //public ArrayList rData {get;set;}
}

