using log4net;
using SiloWebApp.Models;
using SiloWebApp.Tools;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Odbc;
using System.Text;
using System.Web.Http;



namespace SiloWebApp.Controllers
{
    public class DataController : ApiController
    {
        string connectionString = ConfigurationManager.AppSettings["odbcConnection"];
        readonly ILog logger = LogManager.GetLogger(typeof(DataController));

        // 30일 전 데이터 가져오기
        // GET: api/data?MeasureType=Strain
        public IEnumerable<MDataModel> Get(string MeasureType)
        {
            var dataList = new List<MDataModel>();

            if (MeasureType.Equals("Strain") || MeasureType.Equals("Temp") || MeasureType.Equals("Disp"))
            {
                using (OdbcConnection conn = new OdbcConnection(connectionString))
                {
                    OdbcCommand cmd = new OdbcCommand();
                    cmd.Connection = conn;
                    conn.Open();

                    try
                    {
                        DateTime now = DateTime.Now;
                        int year_30DaysAgo = now.AddDays(-30).Year;  // 30일 전에 년도

                        for (int year = now.Year; year > year_30DaysAgo - 1; year--)
                        {
                            StringBuilder query = new StringBuilder(700);
                            query.Append("SELECT MEASURE_TIME");
                            int endIdx = 0;

                            if (MeasureType.Equals("Strain"))
                            {
                                endIdx = CRUD.Strain_colName.Count + 1;
                            }
                            else if (MeasureType.Equals("Temp"))
                            {
                                endIdx = CRUD.Temp_colName.Count + 1;
                            }
                            else if (MeasureType.Equals("Disp"))
                            {
                                endIdx = CRUD.Disp_colName.Count + 1;
                            }

                            for (int colNo = 1; colNo < endIdx; colNo++)
                            {
                                query.Append($", RESULT{colNo}");
                            }
                            // 30일전까지 데이터 가져오기
                            query.Append($" FROM {MeasureType}_{year.ToString()} WHERE TO_DAYS(NOW()) - TO_DAYS(MEASURE_TIME) <= 30 ORDER BY MEASURE_TIME ASC");
                            //logger.Debug(query);
                            cmd.CommandText = query.ToString();
                            OdbcDataReader reader = cmd.ExecuteReader();

                            while (reader.Read())
                            {
                                MDataModel data = new MDataModel();
                                StringBuilder mData = new StringBuilder(350);
                                data.MTime = (DateTime)reader[0];
                                int last = reader.FieldCount;
                                for (int i = 1; i < last; i++)
                                {
                                    if (reader[i] == DBNull.Value)
                                    {
                                        mData.Append("NaN,");
                                    }
                                    else
                                    {
                                        mData.Append($"{reader[i]},");
                                    }
                                }
                                data.MData = mData.ToString().TrimEnd(',');
                                dataList.Add(data);
                            }
                            reader.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Error Data Request for 30 days", ex);
                    }
                    finally
                    {
                        if (conn != null)
                        {
                            conn.Dispose();
                        }
                    }
                }
            }
            return dataList;
        }

        // 실시간 데이터 전송
        // GET: api/data
        public IEnumerable<RTimeDataModel> Get()
        {
            var dataList = new List<RTimeDataModel>();
            var dataType = new string[]
            {
                "STRAIN", "TEMP", "DISP"
            };
            var endIdx = new int[]
            {
                CRUD.Strain_colName.Count+1,
                CRUD.Temp_colName.Count+1,
                CRUD.Disp_colName.Count+1
            };

            using (OdbcConnection conn = new OdbcConnection(connectionString))
            {
                OdbcCommand cmd = new OdbcCommand();
                cmd.Connection = conn;
                conn.Open();

                try
                {
                    DateTime now = DateTime.Now;

                    // 5분 전까지 데이터가 하나 가져오기
                    for (int i = 0; i < 3; i++)
                    {
                        StringBuilder query = new StringBuilder(700);
                        query.Append("SELECT MEASURE_TIME");
                        for (int colNo = 1; colNo < endIdx[i]; colNo++)
                        {
                            query.Append($", RESULT{colNo}");
                        }
                        query.Append($" FROM {dataType[i]}_{now.Year} WHERE measure_time > subdate(now(), interval 5 minute)");

                        cmd.CommandText = query.ToString();
                        OdbcDataReader reader = cmd.ExecuteReader();

                        while (reader.Read())
                        {
                            RTimeDataModel data = new RTimeDataModel();
                            StringBuilder mData = new StringBuilder();

                            data.DataType = dataType[i];
                            data.MTime = (DateTime)reader[0];

                            for (int j = 1; j < reader.FieldCount; j++)
                            {
                                if (reader[j] == DBNull.Value)
                                {
                                    mData.Append("NaN,");
                                }
                                else
                                {
                                    mData.Append($"{reader[j]},");
                                }
                            }
                            data.MData = mData.ToString().TrimEnd(',');
                            dataList.Add(data);
                        }
                        reader.Close();
                    }

                }
                catch (Exception ex)
                {
                    logger.Error("Error RunTime Data Request", ex);
                }
                finally
                {
                    if (conn != null)
                    {
                        conn.Dispose();
                    }
                }
            }
            return dataList;
        }

        /// <summary>
        /// 결과 데이터(변형율, 온도, 경사도)값 가져오는 함수
        /// </summary>
        /// <param name="DataType"></param>
        /// <param name="SensorIdx"></param>
        /// <param name="Start"></param>
        /// <param name="End"></param>
        /// <returns></returns>
        public IEnumerable<MDataModel> Get(string DataType, string SensorIdx, DateTime Start, DateTime End)
        {
            var dataList = new List<MDataModel>();
            string[] index = SensorIdx.Split('s');
            int startYear = Start.Year;
            int year = End.Year;

            if (DataType.Equals("strain") || DataType.Equals("disp") || DataType.Equals("temp"))
            {
                using (OdbcConnection conn = new OdbcConnection(connectionString))
                {
                    string query = null;
                    OdbcCommand cmd = new OdbcCommand();
                    cmd.Connection = conn;
                    conn.Open();
                    try
                    {
                        while (year >= startYear)
                        {
                            query = "SELECT MEASURE_TIME";
                            for (int i = 1; i < index.Length; i++)
                            {
                                query += $", RESULT{index[i]}";
                            }
                            query += $" FROM {DataType}_{year.ToString()} WHERE MEASURE_TIME >= '{Start}' AND MEASURE_TIME <= '{End}' ORDER BY MEASURE_TIME ASC";
                            cmd.CommandText = query;

                            OdbcDataReader reader = cmd.ExecuteReader();

                            while (reader.Read())
                            {
                                MDataModel data = new MDataModel();
                                string mData = null;
                                data.MTime = (DateTime)reader[0];
                                for (int i = 1; i < reader.FieldCount; i++)
                                {
                                    if (reader[i] == DBNull.Value)
                                    {
                                        mData += "NaN,";
                                    }
                                    else
                                    {
                                        mData += $"{reader[i]},";
                                    }
                                }
                                data.MData = mData.TrimEnd(',');
                                dataList.Add(data);
                            }
                            reader.Close();
                            year--;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Error Getting Trend Chart Data", ex);
                    }
                    finally
                    {
                        if (conn != null)
                        {
                            conn.Dispose();
                        }
                    }
                }
            }
            else if (DataType.Equals("p1_raw") || DataType.Equals("p2_raw") || DataType.Equals("p3_raw") || DataType.Equals("p4_raw") )
            {
                using (OdbcConnection conn = new OdbcConnection(connectionString))
                {
                    string query = null;
                    OdbcCommand cmd = new OdbcCommand();
                    cmd.Connection = conn;
                    conn.Open();
                    try
                    {
                        while (year >= startYear)
                        {
                            query = "SELECT MEASURE_TIME, BATT_VOLT_MIN, TEMP";
                            for (int i = 1; i < index.Length; i++)
                            {
                                query += $", SENSOR{index[i]}";
                            }
                            query += $" FROM {DataType}_{year.ToString()} WHERE MEASURE_TIME >= '{Start}' AND MEASURE_TIME <= '{End}' ORDER BY MEASURE_TIME ASC";
                            cmd.CommandText = query;

                            OdbcDataReader reader = cmd.ExecuteReader();

                            while (reader.Read())
                            {
                                MDataModel data = new MDataModel();
                                string mData = null;
                                data.MTime = (DateTime)reader[0];
                                for (int i = 1; i < reader.FieldCount; i++)
                                {
                                    if (reader[i] == DBNull.Value)
                                    {
                                        mData += "NaN,";
                                    }
                                    else
                                    {
                                        mData += $"{reader[i]},";
                                    }
                                }
                                data.MData = mData.TrimEnd(',');
                                dataList.Add(data);
                            }
                            reader.Close();
                            year--;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Error Getting Raw Data", ex);
                    }
                    finally
                    {
                        if (conn != null)
                        {
                            conn.Dispose();
                        }
                    }
                }
            }
            return dataList;
        }

        /// <summary>
        /// Raw 데이터 가져오는 메서드
        /// </summary>
        /// <param name="DataType"></param>
        /// <param name="Start"></param>
        /// <param name="End"></param>
        /// <returns></returns>
        public IEnumerable<MDataModel> Get(string DataType, DateTime Start, DateTime End)
        {
            var dataList = new List<MDataModel>();
            int startYear = Start.Year;
            int year = End.Year;
            int idxCnt = 52;   // 사일로 1번,3번의 채널수

            if (DataType.Equals("p1_raw") || DataType.Equals("p2_raw") || DataType.Equals("p3_raw") || DataType.Equals("p4_raw"))
            {
                if( DataType.Equals("p2_raw") || DataType.Equals("p4_raw"))
                {
                    idxCnt = 40;  // 사일로 2번, 4번 채널수
                }
                using (OdbcConnection conn = new OdbcConnection(connectionString))
                {
                    StringBuilder query = new StringBuilder(700);
                    OdbcCommand cmd = new OdbcCommand();
                    cmd.Connection = conn;
                    conn.Open();
                    try
                    {
                        while (year >= startYear)
                        {
                            query.Append("SELECT MEASURE_TIME, BATT_VOLT_MIN, TEMP");
                            for (int i = 1; i <= idxCnt; i++)
                            {
                                query.Append($", SENSOR{i}");
                            }
                            query.Append($" FROM {DataType}_{year.ToString()} WHERE MEASURE_TIME >= '{Start}' AND MEASURE_TIME <= '{End}' ORDER BY MEASURE_TIME ASC");
                            cmd.CommandText = query.ToString();

                            OdbcDataReader reader = cmd.ExecuteReader();

                            while (reader.Read())
                            {
                                MDataModel data = new MDataModel();
                                StringBuilder mData = new StringBuilder();
                                data.MTime = (DateTime)reader[0];
                                for (int i = 1; i < reader.FieldCount; i++)
                                {
                                    if (reader[i] == DBNull.Value)
                                    {
                                        mData.Append("NaN,");
                                    }
                                    else
                                    {
                                        mData.Append($"{reader[i]},");
                                    }
                                }
                                data.MData = mData.ToString().TrimEnd(',');
                                dataList.Add(data);
                            }
                            reader.Close();
                            year--;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error("Error Getting Raw Data", ex);
                    }
                    finally
                    {
                        if (conn != null)
                        {
                            conn.Dispose();
                        }
                    }
                }
            }
            return dataList;
        }



        // POST: api/Test
        public IEnumerable<TestModel> Post([FromBody]string value)
        {
            TestModel[] model = new TestModel[]
           {
               //new Models.TestModel {Time="2014", StringArr=new double[] {123123,23423.3324,21321 } }
           };

            return model;
        }

        // PUT: api/Test/5
        public void Put(int id, [FromBody]string value)
        {
        }

        // DELETE: api/Test/5
        public void Delete(int id)
        {
        }
    }
}

