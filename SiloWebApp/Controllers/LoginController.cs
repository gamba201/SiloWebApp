using log4net;
using SiloWebApp.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Odbc;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace SiloWebApp.Controllers
{
   
    enum LogInStatus
    {
        OK,
        OFF   
    }

    public class LoginController : ApiController
    {
        readonly static ILog logger = LogManager.GetLogger(typeof(LoginController));
        string connectionString = ConfigurationManager.AppSettings["odbcConnection"];

        // GET: api/Login
        public IEnumerable<UserHistoryModel> Get(int limit)
        {
            var history = new List<UserHistoryModel>();

            using(OdbcConnection conn = new OdbcConnection(connectionString))
            {
                OdbcCommand cmd = new OdbcCommand();
                cmd.Connection = conn;

                try
                {
                    conn.Open();
                    cmd.CommandText = $"SELECT ID, IP, TIME FROM HISTORY_SIGN_ON ORDER BY TIME DESC LIMIT {limit}";

                    OdbcDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        var model = new UserHistoryModel();
                        model.ID = (string)reader[0];
                        model.IP = (string)reader[1];
                        model.Time = (DateTime)reader[2];
                        history.Add(model);
                    }
                    
                    if(reader != null)
                    {
                        reader.Close();
                    }
                }
                catch(Exception ex)
                {
                    logger.Error("Error Get login History", ex);
                }
                finally
                {
                    if(conn != null)
                    {
                        conn.Dispose();
                    }
                }
            }

            return history;
        }

        
        // POST: api/Login
        public int Post([FromBody]UserModel value)
        {
            LogInStatus Flag = LogInStatus.OFF;
            
            int level = 0;  
            using (OdbcConnection conn = new OdbcConnection(connectionString))
            {
                //logger.Debug(value.PW);
                string pwHash = Tools.cryptoPW(value.PW);
                OdbcCommand cmd = new OdbcCommand();
                //logger.Debug(pwHash);

                try
                {
                    conn.Open();
                    cmd.Connection = conn;
                    cmd.CommandText = $"SELECT PW, LEVEL FROM MEMBER WHERE ID LIKE '{value.ID}'";
                    OdbcDataReader reader = cmd.ExecuteReader();
                    
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            if (reader[0].Equals(pwHash)) // 패스워드가 맞으면,, 히스토리 입력후 레벨 응답
                            {
                                level = (int)reader[1];     // level : 1(일반), 2(관리자), 3(슈퍼관리자)
                                Flag = LogInStatus.OK;
                            }
                            else
                            {
                                level = 0;                 //패스워드가 틀린경우
                            }                           
                        }
                    }
                    else
                    {
                        level = -1;         // 사용자가 없는경우
                    }

                    reader.Close();

                    if(Flag == LogInStatus.OK)
                    {
                        cmd.CommandText = $"INSERT INTO HISTORY_SIGN_ON ( ID, IP) VALUES ('{value.ID}', '{value.IP}')";
                        cmd.ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    logger.Error("Error Log In", ex);
                    level = -2;             // 서버오류시
                }
                finally
                {
                    try
                    {
                        if (conn != null)
                        {
                            conn.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Error Closing DB Connection", ex);
                    }
                }
            }
            return level;               // 에러 발생시
        }

        // PUT: api/Login/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE: api/Login/5
        public void Delete(int id)
        {
        }
    }
}
