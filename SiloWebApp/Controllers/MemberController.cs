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
    public class MemberController : ApiController
    {
        string connectionString = ConfigurationManager.AppSettings["odbcConnection"];
        readonly ILog logger = LogManager.GetLogger(typeof(MemberController));

        // GET: api/Member
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        public IEnumerable<User2Model> Get(int level)
        {
            var model = new List<User2Model>();

            using (OdbcConnection conn = new OdbcConnection(connectionString))
            {
                OdbcCommand cmd = new OdbcCommand(connectionString);
                cmd.Connection = conn;

                try
                {
                    conn.Open();
                    // 현재 로그인 사용자와 같거나 낮은 레벨의 사람만 조회해서 가져옴
                    cmd.CommandText = $"SELECT ID, NAME, ENTRY, MODIFICATION, LEVEL FROM MEMBER WHERE LEVEL <= {level.ToString()}";
                    OdbcDataReader reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        var user = new User2Model();
                        user.Id = (string)reader[0];
                        user.Name = (string)reader[1];
                        user.Entry = (DateTime)reader[2];
                        user.Modify = (DateTime)reader[3];
                        user.Level = (int)reader[4];
                        model.Add(user);
                    }

                    if (reader != null)
                    {
                        reader.Close();
                    }
                }
                catch (Exception ex)
                {
                    logger.Error("Error Read All Member", ex);
                }
                finally
                {
                    if (conn != null)
                    {
                        conn.Dispose();
                    }
                }
            }
            return model;
        }

        /// <summary>
        /// 사용자 삭제
        /// </summary>
        /// <param name="id"></param>
        public void GetDelete(string id)
        {
            using (OdbcConnection conn = new OdbcConnection(connectionString))
            {
                OdbcCommand cmd = new OdbcCommand();
                cmd.Connection = conn;

                try
                {
                    conn.Open();
                    cmd.CommandText = $"DELETE FROM MEMBER WHERE ID LIKE '{id}'";
                    cmd.ExecuteNonQuery();
                }
                catch(Exception ex)
                {
                    logger.Error("Error Delete user", ex);
                }
                finally
                {
                    if(conn != null)
                    {
                        conn.Dispose();
                    }
                }
                
            }
        }

        /// <summary>
        /// 패스워드 변경
        /// </summary>
        /// <param name="id"></param>
        /// <param name="pw"></param>
        /// <returns></returns>
       public int GetChangePW(string id, string pw)
        {
            int result = 0;
            using (OdbcConnection conn = new OdbcConnection())
            {
                OdbcCommand cmd = new OdbcCommand();
                cmd.Connection = conn;
                string password = Tools.cryptoPW(pw);
                try
                {
                    conn.Open();
                    cmd.CommandText = $"UPDATE MEMBER SET PW = '{password}' WHERE ID LIKE '{id}'";
                    cmd.ExecuteNonQuery();
                    result = 1;
                }
                catch(Exception ex)
                {
                    logger.Error("Error Change pw", ex);
                    result = 0;
                }
            }
            return result;
        }


        // POST: api/Member
        public int Post([FromBody]UserAddModel user)
        {
            int result = 0;
            using (OdbcConnection conn = new OdbcConnection(connectionString))
            {
                OdbcCommand cmd = new OdbcCommand();
                cmd.Connection = conn;

                try
                {
                    conn.Open();
                    string pwHash = Tools.cryptoPW(user.PW);

                    cmd.CommandText = $"SELECT COUNT(*) FROM MEMBER WHERE ID LIKE '{user.ID}'";
                    
                    if (cmd.ExecuteScalar().ToString().Equals("1"))   // 기존에 동일한 ID가 존재할 경우
                    {
                        result = -1;
                    }
                    else
                    {
                        cmd.CommandText = $"INSERT INTO MEMBER values ('{user.ID}', '{user.Name}', '{pwHash}', now(), null, '{user.Level}')";
                        cmd.ExecuteNonQuery();
                        result = 1;         // 사용자 추가 성공시
                    }
                }
                catch (Exception ex)
                {
                    logger.Error("Error Add New Member", ex);
                    result = 0;         // 사용자 추가 실패시
                }
                finally
                {
                    if (conn != null)
                    {
                        conn.Dispose();
                    }
                }
            }
            return result;
        }

        // PUT: api/Member/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE: api/Member/5
        public void Delete(int id)
        {
        }
    }
}
