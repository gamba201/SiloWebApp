using log4net;
using SiloWebApp.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Common;
using System.Data.Odbc;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace SiloWebApp.Controllers
{
    public class LevelController : ApiController
    {
        string connectionString = ConfigurationManager.AppSettings["odbcConnection"];
        readonly ILog logger = LogManager.GetLogger(typeof(LevelController));

        /// <summary>
        /// 가장 최근에 수정한 레벨 설정값 가져오기
        /// </summary>
        /// <returns></returns>
        // GET: api/Level
        public IEnumerable<LevelModel> Get()
        {
            var Level = new List<LevelModel>();

            using (OdbcConnection conn = new OdbcConnection(connectionString))
            {
                OdbcCommand cmd = new OdbcCommand();
                conn.Open();
                cmd.Connection = conn;
                var overview = new string[] { "Strain", "Temp", "Disp" };

                try
                {
                    for (int i = 0; i < overview.Length; i++)
                    {
                        cmd.CommandText = $"SELECT OVERVIEW, CONCERN, CAUTION, DANGER FROM HISTORY_LEVEL WHERE OVERVIEW LIKE '{overview[i]}' ORDER BY REG_TIME DESC LIMIT 1";
                        var reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            var lm = new LevelModel();
                            lm.Overview = (string)reader[0];
                            lm.Concern = (int)reader[1];
                            lm.Caution = (int)reader[2];
                            lm.Danger = (int)reader[3];
                            Level.Add(lm);
                        }
                        reader.Close();
                    }
                }
                catch (Exception ex)
                {
                    logger.Error("Error Get Level", ex);
                }
            }
            return Level;
        }

        /// <summary>
        /// 최근 변경한 레벨 값에 대해 요청 카운트 값 만큼 가져오기
        /// </summary>
        /// <param name="HistoryCount"></param>
        /// <returns></returns>
        public IEnumerable<LevelListModel> Get(int HistoryCount)
        {
            var LevelList = new List<LevelListModel>();

            using (OdbcConnection conn = new OdbcConnection(connectionString))
            {
                OdbcCommand cmd = new OdbcCommand();
                conn.Open();
                cmd.Connection = conn;

                try
                {
                    cmd.CommandText = $"SELECT REG_TIME, ID, OVERVIEW, CONCERN, CAUTION, DANGER FROM HISTORY_LEVEL ORDER BY REG_TIME DESC LIMIT {HistoryCount.ToString()}";
                    var reader = cmd.ExecuteReader();
                    string unit = null;
                    while (reader.Read())
                    {
                        var llm = new LevelListModel();
                        llm.Reg_Time = (DateTime)reader[0];
                        llm.Id = (string)reader[1];
                        llm.Overview = (string)reader[2];

                        if (llm.Overview.Equals("Strain"))
                        {
                            unit = "εc";
                        }
                        else if (llm.Overview.Equals("Temp"))
                        {
                            unit = "T";
                        }
                        else if (llm.Overview.Equals("Disp"))
                        {
                            unit = "Δ";
                        }

                        llm.safe = $"{unit} ≤ {reader[3].ToString()}";
                        llm.Concern = $"{reader[3].ToString()} ＜ {unit} ≤ {reader[4].ToString()}";
                        llm.Caution = $"{reader[4].ToString()} ＜ {unit} ≤ {reader[5].ToString()}";
                        llm.Danger = $"{reader[5].ToString()} ＜ {unit}";
                        LevelList.Add(llm);
                    }
                    reader.Close();
                }
                catch (Exception ex)
                {
                    logger.Error("Error Get Level List", ex);
                }
            }
            return LevelList;
        }

        /// <summary>
        /// 최근 변경한 레벨 값에 대해 요청 카운트 값 만큼 가져오기
        /// </summary>
        /// <param name="HistoryCount"></param>
        /// <returns></returns>
        public string Get(string type, string id, string level)
        {
            string[] levelArr = level.Split('_');
            string status = "Fail";

            using(OdbcConnection conn = new OdbcConnection(connectionString))
            {
                OdbcCommand cmd = new OdbcCommand();
                conn.Open();
                cmd.Connection = conn;

                try
                {
                    cmd.CommandText = $"INSERT INTO HISTORY_LEVEL ( REG_TIME, ID, OVERVIEW, CONCERN, CAUTION, DANGER ) VALUES ( NOW(), '{id}', '{type}', {levelArr[0]}, {levelArr[1]}, {levelArr[2]} )";
                    cmd.ExecuteNonQuery();
                    status = "OK";
                }
                catch(Exception ex)
                {
                    logger.Error("Error Inserting Level", ex);
                }
                finally
                {
                    if(conn != null)
                    {
                        conn.Dispose();
                    }
                }
            }

            return status;
        }


        // POST: api/Level
        public void Post([FromBody]string value)
        {
        }

        // PUT: api/Level/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE: api/Level/5
        public void Delete(int id)
        {
        }
    }
}
