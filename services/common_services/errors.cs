using System.Collections.Generic;

public static class errors{

        public static Dictionary<int, string> err = new Dictionary<int, string>()
        {
            { 100,"Internal Exception.Please Contact Provider"},
            { 101,"Database Connectivity Error"},
            { 102,"Invalid Login Credentials" },
            { 103,"Invalid OTP"}
        };
        
}