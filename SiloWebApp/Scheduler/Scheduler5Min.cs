using log4net;
using Quartz;
using SiloWebApp.Tools;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Odbc;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml;


/*<<<<<<<<<<<<<<<<<<<<<<<<<<<< 프로세스 >>>>>>>>>>>>>>>>>>>>>>>>>>*/
/* 1. 로우데이터 파일 읽어서 List에 저장 (각 배열의 5번째 줄에 측정 데이터가 존재)
 * 2. 5번째 줄의 데이터만 뽑아서 rawDataList에 저장
 * 3. 네개의 싸일로의 날짜 데이터가 동일한지 검사 (logerNet이 싸일로 중 누락하는 것은 없는지 검사)
 * 4. 로우데이터를 디비 테이블에 입력 (싸일로별로 로우테이터를 저장하는 테이블이 존재)
 * 5. strain 데이터를 계산하여 strain 테이블에 입력 
 * 6. temperature 데이터 temperature 테이블에 입력
 * 7. displacement 데이터 displacement 테이블에 입력
 * 









     */

namespace SiloWebApp.Scheduler
{
    public class Scheduler5Min : IJob
    {
        readonly ILog logger = LogManager.GetLogger(typeof(Scheduler5Min));

        enum Check
        {
            SameData,
            NoData
        }

        public void Execute(IJobExecutionContext context)
        {
            string connectionString = ConfigurationManager.AppSettings["odbcConnection"];
            string rawFilePath = GetCustomConfig("settings/raw-data-path");
            var rawDataList = new List<List<string>>();
            var strainList = new List<string[]>();
            var tempList = new List<string[]>();

            try
            {
                var fileLastStrList = new List<string>();   // 로우데이터 문자열 저장 리스트(5번째 줄에 위치)
                string[] strainArr = new string[15];
                string[] tempArr = new string[15];
                // 파일에서 로우 데이터 읽어옴
                if (!rawFilePath.Equals(null))
                {
                    for (int siloNum = 1; siloNum < 5; siloNum++)
                    {
                        string path = $"{rawFilePath}silo{siloNum}.dat";
                        using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            int lineNum = 1;
                            StreamReader reader = new StreamReader(fs);
                            while (!reader.EndOfStream)
                            {
                                if (lineNum == 5)  // 5번째 줄에 데이터 
                                {
                                    fileLastStrList.Add(reader.ReadLine());
                                }
                                else
                                {
                                    reader.ReadLine();
                                }
                                lineNum++;
                            }
                            reader.Close();
                        }
                    }
                }
                else
                {
                    logger.Error("Error No Text in Raw File");
                    return;
                }

                foreach (string rawDataString in fileLastStrList)
                {
                    // 한줄의 string으로 되어 있는 데이터를 리스트에 놔눠서 저장(스트링배열 리스트)
                    rawDataList.Add(FixDataArray(rawDataString));
                }

                // 모든 싸일로 데이터의 시간이 동일한지 검증
                if (!rawDataList[0][0].Equals(rawDataList[1][0]) || !rawDataList[0][0].Equals(rawDataList[2][0])
                    || !rawDataList[0][0].Equals(rawDataList[3][0]))
                {
                    logger.Error($"Error Different MeasureTime in Raw File. (P1:{rawDataList[0][0]}, P2:{rawDataList[1][0]}, P3:{rawDataList[2][0]}, P4:{rawDataList[3][0]})");
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error Reading Raw File", ex);
                return;
            }

            using (OdbcConnection conn = new OdbcConnection(connectionString))
            {
                OdbcCommand cmd = new OdbcCommand();
                cmd.Connection = conn;

                try
                {
                    conn.Open();
                    // raw 데이터 테이블에 삽입
                    for (int siloNum = 0; siloNum < 4; siloNum++)
                    {
                        cmd.CommandText = MakeInsQuery_Raw(rawDataList[siloNum], siloNum);
                        cmd.ExecuteNonQuery();
                    }

                    // strain, displacement 초기값 가져오기
                    GetInitialValue(cmd);

                    // strain 데이터 테이블에 삽입
                    cmd.CommandText = MakeInsQuery_Strain(rawDataList);
                    cmd.ExecuteNonQuery();

                    // temparature 데이터 테이블에 삽입
                    cmd.CommandText = MakeInsQuery_Temperature(rawDataList);
                    cmd.ExecuteNonQuery();

                    // displacement 데이터 테이블에 삽입
                    cmd.CommandText = MakeInsQuery_Displacement(rawDataList);
                    cmd.ExecuteNonQuery();

                    CRUD.Strain_initValue.Clear();
                    CRUD.Disp_initValue.Clear();

                }
                catch (Exception ex)
                {
                    logger.Error("Error Inserting DB", ex);
                    if (CRUD.Strain_initValue.Count != 0)
                    {
                        CRUD.Strain_initValue.Clear();
                    }
                    if (CRUD.Disp_initValue.Count != 0)
                    {
                        CRUD.Disp_initValue.Clear();
                    }
                    return;
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


        /// <summary>
        /// 변형율과 경사도 계산시 필요한 초기값 DB에서 가져와 메모리에 저장
        /// </summary>
        /// <param name="cmd"></param>
        private void GetInitialValue(OdbcCommand cmd)
        {

            cmd.CommandText = "SELECT CHANNEL, STRAIN_INIT, TEMP, CALIBRATION_FACTOR FROM INIT_STRAIN ORDER BY NO ASC";
            OdbcDataReader reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                CRUD.Strain_initValue.Add(reader[0].ToString(), new float[3] { (float)reader[1], (float)reader[2], (float)reader[3] });
            }
            reader.Close();

            cmd.CommandText = "SELECT CHANNEL, ANGLE, TEMP, SCALE_FACTOR FROM INIT_DISPLACEMENT ORDER BY NO ASC";
            reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                CRUD.Disp_initValue.Add(reader[0].ToString(), new float[3] { (float)reader[1], (float)reader[2], (float)reader[3] });
            }
            reader.Close();
        }



        /// <summary>
        /// configuration.xml 파일에서 특정 노드의 문자열을 리턴
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private string GetCustomConfig(string node)
        {
            try
            {
                XmlDocument xml = new XmlDocument();
                // getting custom config file  
                xml.Load(WebApiApplication.ConfigPath);
                XmlNode currentNode = xml.SelectSingleNode($"descendant::{node}");
                if (currentNode != null)
                {
                    return currentNode.InnerText;
                }
                else
                {
                    logger.Error("Error Coinfig.xml InnerText of Node is null");
                    return null;
                }
            }
            catch (XmlException ex)
            {
                logger.Error("Error Loading XML.", ex);
                return null;
            }
        }

        /// <summary>
        /// 싸일로 데이터 배열에서 불필요한 데이터 제거 및 데이터 포맷 정리
        /// </summary>
        /// <param name="siloFileData"></param>
        private List<string> FixDataArray(string rawDataString)
        {
            string[] dataSeparators = new string[] { "\",\"", "\",", ",\"", "," };
            var data = new List<string>(rawDataString.Split(dataSeparators, StringSplitOptions.None));  // 수치 데이터를 분리해서 배열에 삽입

            //int cnt = 0;
            //foreach (string a in dataArr)
            //{
            //    logger.Debug("dataArr[" + cnt++ + "]: " + a);
            //}

            // change format of time data to 'YYMMDDhhmm'
            data[0] = data[0].Remove(0, 1);
            //for (int i = 0; i < 3; i++)
            //{
                data.RemoveAt(1);
            //}

            return data;
        }

        /// <summary>
        /// RAW 테이블에 같은 시간의 데이터 존재여부 확인
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="tName"></param>
        /// <param name="timestamp"></param>
        /// <returns></returns>
        private Check HasDataInTable(OdbcCommand cmd, string tName, string mTime)
        {
            cmd.CommandText = $"SELECT COUNT(*) FROM {tName} WHERE MEASURE_TIME = '{mTime}'";
            if (Convert.ToString(cmd.ExecuteScalar()).Equals("1")) // 동일한 시간의 데이터가 있으면,
            {
                return Check.SameData;
            }
            else
            {
                return Check.NoData;
            }
        }

        /// <summary>
        /// 로우 데이터 테이블 삽입 쿼리 작성
        /// </summary>
        /// <param name="siloData"></param>
        /// <param name="tName"></param>
        /// <returns></returns>
        private string MakeInsQuery_Raw(List<string> siloData, int siloNo)
        {
            string tName = $"P{siloNo + 1}_RAW_{DateTime.Now.Year}";
            // make insert query
            StringBuilder query = new StringBuilder(1300);
            query.Append($"INSERT INTO {tName} ( MEASURE_TIME, BATT_VOLT_MIN, TEMP ");

            for (int i = 1; i < siloData.Count-2; i++)
            {
                query.Append($",SENSOR{i} ");
            }

            query.Append($") SELECT '{siloData[0]}'"); //siloData[0] = 측정시간


            for (int i = 1; i < siloData.Count; i++)
            {
                if (siloData[i].Equals("NAN") || siloData[i].Length == 0)
                {
                    query.Append(", null");
                }
                else
                {
                    query.Append(", " + siloData[i]);
                }
            }
            query.Append($" FROM dual WHERE NOT EXISTS ( SELECT * FROM {tName} WHERE MEASURE_TIME = '{siloData[0]}' )");
            //logger.Debug(query);
            return query.ToString();
        }

        /// <summary>
        /// Strain 입력 쿼리 스트링 생성
        /// </summary>
        /// <param name="rawDataList"></param>
        /// <param name="mTime"></param>
        private string MakeInsQuery_Strain(List<List<string>> rawDataList)
        {
            StringBuilder query = new StringBuilder(1000);
            query.Append($"INSERT INTO STRAIN_{DateTime.Now.Year} (MEASURE_TIME");
            float result, voltage, temp, strain_InitValue, temp_InitValue, calibration_Factor;
            int siloNo = 0;
            string voltString = null;
            string tempString = null;

            for (int i = 1; i < CRUD.Strain_colName.Count + 1; i++)
            {
                query.Append($", RESULT{i}");
            }
            query.Append($") SELECT '{rawDataList[0][0]}'");   // 측정 시간

            foreach (string colName in CRUD.Strain_colName)
            {
                // strain 컬럼명의 첫글자는 싸일로 번호
                siloNo = int.Parse(colName.Substring(0, 1));
                // voltage, temparature 값을 string으로 저장
                voltString = rawDataList[siloNo - 1][CRUD.Raw_colIdx[colName]];
                tempString = rawDataList[siloNo - 1][CRUD.Raw_colIdx[colName.Insert(6, "T")]];
                //만약 값이 NaN 이거나 없으면, null 값 입력
                if (voltString.Equals("NAN") || voltString.Length == 0 || tempString.Equals("NAN") || tempString.Length == 0)
                {
                    query.Append(", null");
                    continue;
                }

                voltage = Convert.ToSingle(voltString);
                temp = Convert.ToSingle(tempString);
                strain_InitValue = CRUD.Strain_initValue[colName][0];  // float배열 0번째
                temp_InitValue = CRUD.Strain_initValue[colName][1];
                calibration_Factor = CRUD.Strain_initValue[colName][2];
                // 계산식
                // voltage - strain 초기값 - ( 온도초기값 - 온도 voltage ) * 온도보정계수
                result = voltage - strain_InitValue - (temp_InitValue - temp) * calibration_Factor;

                query.Append($", {result.ToString()}");
            }
            query.Append($" FROM dual  WHERE NOT EXISTS ( SELECT * FROM STRAIN_{DateTime.Now.Year} WHERE MEASURE_TIME = '{rawDataList[0][0]}' )");

            //logger.Debug(query);
            return query.ToString();
        }

        /// <summary>
        /// Temparature 입력 쿼리 스트링 생성
        /// </summary>
        /// <param name="rawDataList"></param>
        /// <param name="mTime"></param>
        /// <returns></returns>
        private string MakeInsQuery_Temperature(List<List<string>> rawDataList)
        {
            int siloNo = 0;
            StringBuilder query = new StringBuilder(1000);
            query.Append($"INSERT INTO TEMP_{DateTime.Now.Year} (MEASURE_TIME");
            string temp = null;

            for (int i = 1; i < CRUD.Temp_colName.Count + 1; i++)
            {
                query.Append($", RESULT{i}");
            }
            query.Append($") SELECT '{rawDataList[0][0]}'"); // 측정시간

            foreach (string colName in CRUD.Temp_colName)
            {
                siloNo = int.Parse(colName.Substring(0, 1));   // 컬럼명의 첫번째 글자가 사일로 번호
                temp = rawDataList[siloNo - 1][CRUD.Raw_colIdx[colName.Insert(6, "T")]];
                //만약 값이 NaN 이거나 없으면, null 값 입력
                if (temp.Equals("NAN") || temp.Length == 0)
                {
                    query.Append(", null");
                    continue;
                }

                query.Append($", {temp}");
            }
            query.Append($" FROM dual WHERE NOT EXISTS ( SELECT * FROM TEMP_{DateTime.Now.Year} WHERE MEASURE_TIME = '{rawDataList[0][0]}' )");

            //logger.Debug(query);
            return query.ToString();
        }

        /// <summary>
        /// Displacement 입력 쿼리 생성
        /// </summary>
        /// <param name="rawDataList"></param>
        /// <param name="mTime"></param>
        /// <returns></returns>
        private string MakeInsQuery_Displacement(List<List<string>> rawDataList)
        {
            int siloNo = 0;
            StringBuilder query = new StringBuilder(1000);
            query.Append($"INSERT INTO DISP_{DateTime.Now.Year} (MEASURE_TIME");

            float result, voltage, temp, scale_Factor, calibration_temp, angle;
            string voltString = null;
            string tempString = null;

            for (int i = 1; i < CRUD.Disp_colName.Count + 1; i++)
            {
                query.Append($", RESULT{i}");
            }
            query.Append($") SELECT '{rawDataList[0][0]}'");  //측정시간

            foreach (string colName in CRUD.Disp_colName)
            {
                siloNo = int.Parse(colName.Substring(0, 1));
                voltString = rawDataList[siloNo - 1][CRUD.Raw_colIdx[colName]];
                tempString = rawDataList[siloNo - 1][CRUD.Raw_colIdx[colName.Insert(7, "T")]];
                //만약 값이 NaN 이거나 없으면, null 값 입력
                if (voltString.Equals("NAN") || voltString.Length == 0 || tempString.Equals("NAN") || tempString.Length == 0)
                {
                    query.Append(", null");
                    continue;
                }

                //logger.Debug("colName : " + colName);

                voltage = Convert.ToSingle(voltString);
                temp = Convert.ToSingle(tempString);
                angle = CRUD.Disp_initValue[colName][0];
                calibration_temp = CRUD.Disp_initValue[colName][1];
                scale_Factor = CRUD.Disp_initValue[colName][2];
                //계산식 : Scale Factor * (1 + 0.0004 * ( temp voltage - Calibration Temp )) * voltage / 1000 - 0.00008594 * 
                // ( temp voltage - Correction Temp) - angle constant
                // * voltage 단위는 mV 이므로 로우데이터 값에서 1000을 나눠줌
                result = (float)(scale_Factor * (1 + 0.0004 * (temp - calibration_temp)) * voltage / 1000 - 0.00008594 *
                    (temp - calibration_temp) - angle);

                query.Append($", {result.ToString()}");
            }
            query.Append($" FROM dual WHERE NOT EXISTS ( SELECT * FROM DISP_{DateTime.Now.Year} WHERE MEASURE_TIME = '{rawDataList[0][0]}' )");

            return query.ToString();
        }
    }
}