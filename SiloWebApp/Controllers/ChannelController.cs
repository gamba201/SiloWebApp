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
    public class ChannelController : ApiController
    {
        string connectionString = ConfigurationManager.AppSettings["odbcConnection"];
        readonly ILog logger = LogManager.GetLogger(typeof(ChannelController));
        // GET: api/Channel
        public IEnumerable<ChannelModel> Get()
        {
            var channel = new List<ChannelModel>();

            using (OdbcConnection conn = new OdbcConnection(connectionString))
            {
                OdbcCommand cmd = new OdbcCommand();
                cmd.Connection = conn;
                conn.Open();

                try
                {
                    cmd.CommandText = "SELECT TAB, COLNAME, COLIDX FROM COLUMNS ORDER BY COLIDX ASC";
                    OdbcDataReader reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        ChannelModel data = new ChannelModel();
                        data.Type = (string)reader[0];
                        data.Channel = (string)reader[1];
                        data.Index = (int)reader[2];

                        channel.Add(data);
                    }
                    reader.Close();

                }
                catch (Exception ex)
                {
                    logger.Error("Error Get Channel", ex);
                }
                finally
                {
                    if (conn != null)
                    {
                        conn.Dispose();
                    }
                }
            }
            return channel;
        }

        // GET: api/Channel/5
        public string Get(int id)
        {
            return "value";
        }

        // POST: api/Channel
        public void Post([FromBody]string value)
        {
        }

        // PUT: api/Channel/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE: api/Channel/5
        public void Delete(int id)
        {
        }
    }
}
