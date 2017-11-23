using log4net;
using Quartz;
using SiloWebApp.Tools;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Odbc;
using System.Linq;
using System.Web;

namespace SiloWebApp.Scheduler
{
    public class Scheduler1Year : IJob
    {
        readonly ILog logger = LogManager.GetLogger(typeof(Scheduler1Year));

        public void Execute(IJobExecutionContext context)
        {
            string connectionString = ConfigurationManager.AppSettings["odbcConnection"];

            using(OdbcConnection conn = new OdbcConnection(connectionString))
            {
                OdbcCommand cmd = new OdbcCommand();
                cmd.Connection = conn;

                // 다음해에 필요한 로우테이블, 가공데이터 저장 테이블 생성
                try
                {
                    conn.Open();
                    for(int i=1; i<5; i++)
                    {
                        CRUD.Create_RawTable(cmd, $"P{i}_RAW_{DateTime.Now.Year + 1}");
                    }

                    CRUD.Create_ResultTable(cmd, $"STRAIN_{DateTime.Now.Year + 1}");
                    CRUD.Create_ResultTable(cmd, $"TEMP_{DateTime.Now.Year + 1}");
                    CRUD.Create_ResultTable(cmd, $"DISP_{DateTime.Now.Year + 1}");
                }
                catch(Exception ex)
                {
                    logger.Error("Error Make Next Year table", ex);
                }
                finally
                {
                    if(conn != null)
                    {
                        conn.Close();
                    }
                }
            }
        }

    }
}