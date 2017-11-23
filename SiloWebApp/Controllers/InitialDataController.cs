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
    public class InitialDataController : ApiController
    {
        string connectionString = ConfigurationManager.AppSettings["odbcConnection"];
        readonly ILog logger = LogManager.GetLogger(typeof(DataController));

        // GET: api/InitialData
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }



        public IEnumerable<InitialModel> GetStrainInitValue(string type)
        {
            var modelList = new List<InitialModel>();

            using (OdbcConnection conn = new OdbcConnection(connectionString))
            {
                OdbcCommand cmd = new OdbcCommand();
                conn.Open();
                cmd.Connection = conn;

                try
                {
                    if (type.Equals("strain"))
                    {
                        cmd.CommandText = "SELECT SILO_NO, DIRECTION, CHANNEL, STRAIN_INIT, TEMP, CALIBRATION_FACTOR FROM INIT_STRAIN";
                    }
                    else if (type.Equals("disp")) 
                    {
                        cmd.CommandText = "SELECT SILO_NO, DIRECTION, CHANNEL, ANGLE, TEMP, SCALE_FACTOR FROM INIT_DISPLACEMENT";
                    }

                    OdbcDataReader reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        var model = new InitialModel();
                        model.SiloNo = (int)reader[0];
                        model.Direction = (string)reader[1];
                        model.Channel = (string)reader[2];
                        model.Value1 = (float)reader[3];    // strain이면  εc값, displacement이면 Δ값
                        model.Value2 = (float)reader[4];    // 온도보정
                        model.Value3 = (float)reader[5];    // 보정계수
                        modelList.Add(model);
                    }
                    reader.Close();
                }
                catch(Exception ex)
                {
                    logger.Error("Error Get Initial Value", ex);
                }
                finally
                {
                    if(conn != null)
                    {
                        conn.Dispose();
                    }
                }
            }
            return modelList;
        }
        // GET: api/InitialData/5
        public string Get(int id)
        {
            return "value";
        }

        // POST: api/InitialData
        public void Post([FromBody]string value)
        {
        }

        // PUT: api/InitialData/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE: api/InitialData/5
        public void Delete(int id)
        {
        }
    }
}
