
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MySql.Data.MySqlClient;

public class homeSrv
{
    smsService sms =new smsService();
    dbServices ds=new dbServices();
    IConfiguration appsettings = new ConfigurationBuilder().AddJsonFile("Properties/appsettings.json").Build();
    public homeSrv(){}


    public async Task<responseData> getHomeDetails(requestData req)
    {
        responseData resData=new responseData();
        resData.rStatus=0; // database error this error is caught ar app level
        resData.rData["rCode"]=0;
        resData.rData["rMessage"]="Token Valid"; 
        


        MySqlParameter[] myParams = new MySqlParameter[] {new MySqlParameter("@last_updated_on","2000-01-01")};
        var sq = "CALL getMasters_2022('2000-01-01');";
        var dbdata = ds.executeSQLpcmdb(sq,myParams);
        if(dbdata==null) // error occured
            resData.rStatus=100; // database error
        else
        {
                resData.rData["rCode"]=0;
                resData.rData["rValue"]=dbdata;                                           
        }

        return resData;
    }


}