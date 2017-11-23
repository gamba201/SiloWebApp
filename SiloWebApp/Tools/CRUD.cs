using log4net;
using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.Linq;
using System.Web;
using System.Xml;

namespace SiloWebApp.Tools
{
    public class CRUD
    {
        readonly static ILog logger = LogManager.GetLogger(typeof(CRUD));

        enum Check
        {
            Exist,
            NoData,
            Error
        }

        public static Dictionary<string, int> Raw_colIdx = new Dictionary<string, int> ();
        public static Dictionary<string, float[]> Strain_initValue = new Dictionary<string, float[]>();
        public static Dictionary<string, float[]> Disp_initValue = new Dictionary<string, float[]>();
        public static List<string> Strain_colName = new List<string>();
        public static List<string> Temp_colName = new List<string>();
        public static List<string> Disp_colName = new List<string>();

        /// <summary> 
        /// 로우데이터 저장 테이블
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="tableName"></param>
        public static void Create_RawTable(OdbcCommand cmd, string tableName)
        {
            string query = $"CREATE TABLE IF NOT EXISTS {tableName} ( MEASURE_TIME DATETIME NOT NULL, BATT_VOLT_MIN FLOAT(6,2) NOT NULL, TEMP FLOAT(6,2) NOT NULL, ";

            for (int columnCount = 1; columnCount < 81; columnCount++)
            {
                query += $"SENSOR{columnCount} FLOAT(9,5), ";
            }
            query += "PRIMARY KEY (MEASURE_TIME) ) ENGINE = InnoDB";

            try
            {
                cmd.CommandText = query;
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                logger.Error($"Error Create Table \"{tableName}\"", ex);
            }
        }

        /// <summary>
        /// 가공 데이터 저장 테이블 생성
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="tableName"></param>
        public static void Create_ResultTable(OdbcCommand cmd, string tableName)
        {
            string query = $"CREATE TABLE IF NOT EXISTS {tableName} ( MEASURE_TIME DATETIME NOT NULL, ";

            for (int columnCount = 1; columnCount < 51; columnCount++)
            {
                query += $"RESULT{columnCount} FLOAT(7,2), ";
            }
            query += "PRIMARY KEY (MEASURE_TIME) ) ENGINE = InnoDB";

            try
            {
                cmd.CommandText = query;
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                logger.Error($"Error Create Table \"{tableName}\"", ex);
            }
        }

        /// <summary>
        /// xml 파일 읽기
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="nodeName"></param>
        /// <returns></returns>
        public static string XmlRead(string filePath, string nodeName)
        {
            try
            {
                XmlDocument xml = new XmlDocument();
                xml.Load(filePath);
                XmlNode node = xml.SelectSingleNode($"descendant::{nodeName}");
                return node.InnerText;
            }
            catch (Exception ex)
            {
                logger.Error("Error Read Xml", ex);
                return null;
            }

        }


        

    }
}