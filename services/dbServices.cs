using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
public class dbServices{
    IConfiguration appsettings = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
    //MySqlConnection conn = null; // this will store the connection which will be persistent 
    MySqlConnection connPrimary = null; // this will store the connection which will be persistent 
    MySqlConnection connReadOnly = null;
    
    public  dbServices() // constructor
    {
        //_appsettings=appsettings;
        connectDBPrimary();
        connectDBReadOnly();
    }

    private void connectDBPrimary()
    {   
        
        try
        {
            connPrimary = new MySqlConnection(appsettings["db:connStrPrimary"]);
            connPrimary.Open();
        }
        catch (Exception ex)
        {
            //throw new ErrorEventArgs(ex); // check as this will throw exception error
            Console.WriteLine(ex);
        }
    }
    private void connectDBReadOnly()
    {
        
        try
        {
            connReadOnly = new MySqlConnection(appsettings["db:connStrReadOnly"]);
            connReadOnly.Open();
        }
        catch (Exception ex)
        {
            //throw new ErrorEventArgs(ex); // check as this will throw exception error
            Console.WriteLine(ex);
        }
    }


    public List<List<Object[]>> executeSQL(string sq,MySqlParameter[] prms) // this will return the database response the last partameter is to allow selection of connectio id
    {        
            MySqlTransaction trans=null;
            //ArrayList allTables=new ArrayList();
            List<List<Object[]>> allTables=new List<List<Object[]>>();

             try 
             {
                if (connPrimary == null || connPrimary.State == 0)
                    connectDBPrimary();

                trans = connPrimary.BeginTransaction();
                 
                var cmd = connPrimary.CreateCommand();
                cmd.CommandText = sq;
                if(prms!=null)
                    cmd.Parameters.AddRange(prms);


                using (MySqlDataReader dr = cmd.ExecuteReader())
                {
                    do
                    {
                        //ArrayList tblRows = new ArrayList();
                        //List<Object> tblRows=new List<Object>();
                        List<Object[]> tblRows=new List<Object[]>();
                        while (dr.Read())
                        {
                            //List<Object> tblFields=new List<Object>();
                            object[] values = new object[dr.FieldCount]; // create an array with sixe of field count
                            dr.GetValues(values); // save all values here
                            tblRows.Add(values); // add this to the list array
                        }
                        allTables.Add(tblRows);
                    } while (dr.NextResult());
                }
             }
             catch (Exception ex)
             {
                Console.Write(ex.Message);
                trans.Rollback(); // check these functions
                return null; // if error return null
             }
             Console.Write("Database Operation Completed Successfully");
             trans.Commit(); // check thee functions
             return allTables; // if success return allTables
    }
    
    public List<List<Object[]>>  executeSQLpcmdb(string sq,MySqlParameter[] prms) // this will return the database response the last partameter is to allow selection of connectio id
    {

            MySqlTransaction trans=null;
             List<List<Object[]>> allTables=new List<List<Object[]>>();

             try 
             {
                if (connReadOnly == null)
                    connectDBReadOnly();

                trans = connReadOnly.BeginTransaction();
                 
                var cmd = connReadOnly.CreateCommand();
                cmd.CommandText = sq;
                if(prms!=null)
                    cmd.Parameters.AddRange(prms);

                using (MySqlDataReader dr = cmd.ExecuteReader())
                {
                    do
                    {
                        List<Object[]> tblRows=new List<Object[]>();
                        while (dr.Read())
                        {
                            object[] values = new object[dr.FieldCount]; // create an array with sixe of field count
                            dr.GetValues(values); // save all values here
                            tblRows.Add(values); // add this to the list array
                        }
                        allTables.Add(tblRows);
                    } while (dr.NextResult());
                }
             }
             catch (Exception ex)
             {
                Console.Write(ex.Message);
                trans.Rollback(); // check these functions
                return null; // if error return null
             }
             Console.Write("Database Operation Completed Successfully");
             trans.Commit(); // check thee functions
             return allTables; // if success return allTables
    }


}
