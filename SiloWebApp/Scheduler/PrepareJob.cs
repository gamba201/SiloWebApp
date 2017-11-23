using log4net;
using SiloWebApp.Tools;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Odbc;
using System.Diagnostics;
using System.Xml;


namespace SiloWebApp.Scheduler
{
    public class PrepareJob
    {
        enum Check
        {
            Exist,
            NoData,
            Error
        }

        readonly ILog logger = LogManager.GetLogger(typeof(PrepareJob));
        public void Execute()
        {
            string connectionString = ConfigurationManager.AppSettings["odbcConnection"];
            using (OdbcConnection conn = new OdbcConnection(connectionString))
            {
                OdbcCommand cmd = new OdbcCommand();
                cmd.Connection = conn;

                // connect db and excute query
                try
                {
                    conn.Open();

                    // 컬럼 셋팅 테이블 생성
                    string query = CRUD.XmlRead(WebApiApplication.PreQueryPath, "statements/statement[@name='COLUMNS']");
                    cmd.CommandText = query;
                    cmd.ExecuteNonQuery();

                    // 측정 데이터에 대한 테이블 컬럼명을 디비에 저장한다.
                    // 최초 실행시에 configuration.xml 파일의 초기값을 셋팅하지만, 이후에 실행할 경우, 실행되지 않는다.
                    if (HasData(cmd, "COLUMNS") == Check.NoData)
                    {
                        char separator = ';';
                        //로우 테이블 컬럼명 디비에 입력
                        for (int siloNo = 1; siloNo < 5; siloNo++)
                        {

                            string[] rawCols = CRUD.XmlRead(WebApiApplication.ConfigPath, $"settings/raw-column/p{siloNo}").Split(separator);
                            for (int colNum = 0; colNum < rawCols.Length; colNum++)             // 0번째 인덱스는 시간이 들어가는 컬럼이므로 1부터 시작
                            {
                                cmd.CommandText = $"INSERT INTO COLUMNS (TAB, COLIDX, COLNAME) VALUES ( 'P{siloNo}_RAW', {colNum + 3}, '{rawCols[colNum]}' )";
                                cmd.ExecuteNonQuery();
                            }
                        }

                        // 변형율 테이블 컬럼명 디비에 입력
                        string[] strainCols = CRUD.XmlRead(WebApiApplication.ConfigPath, "settings/strain-column").Split(separator);
                        for (int colNum = 0; colNum < strainCols.Length; colNum++)
                        {
                            cmd.CommandText = $"INSERT INTO COLUMNS (TAB, COLIDX, COLNAME) VALUES ( 'STRAIN', {colNum + 1}, '{strainCols[colNum]}' )";
                            cmd.ExecuteNonQuery();
                        }

                        // 온도 테이블 컬럼명 디비에 입력
                        string[] tempCols = CRUD.XmlRead(WebApiApplication.ConfigPath, "settings/temp-column").Split(separator);
                        for (int colNum = 0; colNum < tempCols.Length; colNum++)
                        {
                            cmd.CommandText = $"INSERT INTO COLUMNS (TAB, COLIDX, COLNAME) VALUES ( 'TEMP', {colNum + 1}, '{tempCols[colNum]}' )";
                            cmd.ExecuteNonQuery();
                        }

                        // 경사도 테이블 컬럼명 디비에 입력
                        string[] dispCols = CRUD.XmlRead(WebApiApplication.ConfigPath, "settings/disp-column").Split(separator);
                        for (int colNum = 0; colNum < dispCols.Length; colNum++)
                        {
                            cmd.CommandText = $"INSERT INTO COLUMNS (TAB, COLIDX, COLNAME) VALUES ( 'DISP', {colNum + 1}, '{dispCols[colNum]}' )";
                            cmd.ExecuteNonQuery();
                        }
                    }

                    // raw table 생성
                    for (int siloNo = 1; siloNo < 5; siloNo++)
                    {
                        CRUD.Create_RawTable(cmd, $"P{siloNo}_RAW_{DateTime.Now.Year}");
                    }
                    // 변형율 table 생성
                    CRUD.Create_ResultTable(cmd, $"STRAIN_{DateTime.Now.Year}");
                    // 온도 table 생성
                    CRUD.Create_ResultTable(cmd, $"TEMP_{DateTime.Now.Year}");
                    // 변위 table 생성
                    CRUD.Create_ResultTable(cmd, $"DISP_{DateTime.Now.Year}");
                    // 사용자 관리 테이블 생성
                    cmd.CommandText = CRUD.XmlRead(WebApiApplication.PreQueryPath, "statements/statement[@name='MEMBER']");
                    cmd.ExecuteNonQuery();
                    // 접속 히스토리 테이블 생성
                    cmd.CommandText = CRUD.XmlRead(WebApiApplication.PreQueryPath, "statements/statement[@name='HISTORY_SIGN']");
                    cmd.ExecuteNonQuery();
                    // 범위 변경 히스토리 테이블 생성
                    cmd.CommandText = CRUD.XmlRead(WebApiApplication.PreQueryPath, "statements/statement[@name='HISTORY_LEVEL']");
                    cmd.ExecuteNonQuery();
                    // 변형율 초기값 테이블 생성
                    cmd.CommandText = CRUD.XmlRead(WebApiApplication.PreQueryPath, "statements/statement[@name='INIT_STRAIN']");
                    cmd.ExecuteNonQuery();
                    // 변위 초기값 테이블 생성
                    cmd.CommandText = CRUD.XmlRead(WebApiApplication.PreQueryPath, "statements/statement[@name='INIT_DISP']");
                    cmd.ExecuteNonQuery();

                    // 초기값 디비에 셋팅
                    InsertInitialValue(cmd);

                    // 컬럼 데이터 전역 객체에 저장
                    CRUD.Raw_colIdx = GetColumnIdx(cmd);
                    CRUD.Strain_colName = GetColumnName(cmd, "STRAIN");
                    CRUD.Temp_colName = GetColumnName(cmd, "TEMP");
                    CRUD.Disp_colName = GetColumnName(cmd, "DISP");
                }
                catch (Exception ex)
                {
                    logger.Error("Error Opennig DB Connection", ex);
                    return;
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
        }


        /// <summary>
        /// 테이블에 데이터가 한개이상 존재하는 검사
        /// </summary>
        /// <param name="xmlPath"></param>
        /// <param name="cmd"></param>
        /// <returns></returns>
        private Check HasData(OdbcCommand cmd, string tableName)
        {
            try
            {
                cmd.CommandText = $"SELECT COUNT(*) FROM {tableName}";
                if (!cmd.ExecuteScalar().ToString().Equals("0"))
                {
                    return Check.Exist;
                }
                else
                {
                    return Check.NoData;
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error Finding Data in Table", ex);
                return Check.Error;
            }

        }



        /// <summary>
        /// strain과 displacement, 경고레벨 초기값을 DB에 삽입하는 메소드
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="xml"></param>
        private void InsertInitialValue(OdbcCommand cmd)
        {
            try
            {
                XmlDocument xml = new XmlDocument();
                xml.Load(WebApiApplication.ConfigPath);
                XmlNode node;
                char separator = ';';
                string query = null;
                string direction = null;
                string preClause = null;
                Check hasStrainInitValue = HasData(cmd, "INIT_STRAIN");
                Check hasDisplacemnetInitValue = HasData(cmd, "INIT_DISPLACEMENT");
                Check hasLevelValue = HasData(cmd, "HISTORY_LEVEL");

                xml.Load(WebApiApplication.ConfigPath);

                // 테이블에 데이터가 하나도 없다면, 초기값 삽입(strain)
                if (hasStrainInitValue == Check.NoData)
                {
                    for (int siloNo = 1; siloNo < 5; siloNo++)
                    {
                        preClause = "descendant::settings/strain-constant";
                        node = xml.SelectSingleNode($"{preClause}[@name='P{siloNo}']/channel");
                        string[] channel = node.InnerText.ToString().Split(separator);
                        node = xml.SelectSingleNode($"{preClause}[@name='P{siloNo}']/strain");
                        string[] strainInitialValue = node.InnerText.Split(separator);
                        node = xml.SelectSingleNode($"{preClause}[@name='P{siloNo}']/temp");
                        string[] tempInitialValue = node.InnerText.Split(separator);
                        node = xml.SelectSingleNode($"{preClause}[@name='P{siloNo}']/calibration-factor");
                        string[] calibrationFactor = node.InnerText.Split(separator);

                        for (int j = 0; j < channel.Length; j++)
                        {
                            direction = SettingDirection(channel[j]);       // 방위 설정

                            query = "INSERT INTO INIT_STRAIN ( SILO_NO, DIRECTION, CHANNEL, STRAIN_INIT, TEMP, CALIBRATION_FACTOR ) VALUES ( " +
                                $"{siloNo}, '{direction}', '{channel[j]}', {strainInitialValue[j]}, {tempInitialValue[j]}, {calibrationFactor[j]} )";
                            //logger.Debug(query);
                            cmd.CommandText = query;
                            cmd.ExecuteNonQuery();
                        }
                    }
                }


                // displacement 초기값 입력
                if (hasDisplacemnetInitValue == Check.NoData)
                {
                    for (int siloNo = 1; siloNo < 5; siloNo++)
                    {
                        preClause = "descendant::settings/displacement-constant";
                        node = xml.SelectSingleNode($"{preClause}[@name='P{siloNo}']/channel");
                        string[] channel = node.InnerText.Split(separator);
                        node = xml.SelectSingleNode($"{preClause}[@name='P{siloNo}']/scale-factor");
                        string[] scaleFactor = node.InnerText.Split(separator);
                        node = xml.SelectSingleNode($"{preClause}[@name='P{siloNo}']/calibration-temp");
                        string[] tempInitialValue = node.InnerText.Split(separator);
                        node = xml.SelectSingleNode($"{preClause}[@name='P{siloNo}']/angle");
                        string[] angle = node.InnerText.Split(separator);

                        for (int j = 0; j < channel.Length; j++)
                        {
                            direction = SettingDirection(channel[j]);       // 방위 설정

                            query = "INSERT INTO INIT_DISPLACEMENT ( SILO_NO, DIRECTION, CHANNEL, ANGLE, TEMP, SCALE_FACTOR ) VALUES ( " +
                                $"{siloNo}, '{direction}', '{channel[j]}', {angle[j]}, {tempInitialValue[j]}, {scaleFactor[j]} )";

                            cmd.CommandText = query;
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                // 레벨 초기값 hasLevelValue
                if(hasLevelValue == Check.NoData)
                {
                    //logger.Debug("aa");
                    cmd.CommandText = "INSERT INTO HISTORY_LEVEL ( ID, REG_TIME, OVERVIEW, CONCERN, CAUTION, DANGER ) VALUES ( \"admin\", NOW(), \"Strain\", 0, 100, 200 )";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "INSERT INTO HISTORY_LEVEL ( ID, REG_TIME, OVERVIEW, CONCERN, CAUTION, DANGER ) VALUES ( \"admin\", NOW(), \"Temp\", 0, 500, 1000 )";
                    cmd.ExecuteNonQuery();

                    cmd.CommandText = "INSERT INTO HISTORY_LEVEL ( ID, REG_TIME, OVERVIEW, CONCERN, CAUTION, DANGER ) VALUES ( \"admin\", NOW(), \"Disp\", 40, 60, 80 )";
                    cmd.ExecuteNonQuery();
                }

            }
            catch (Exception ex)
            {
                logger.Debug("Error Insert Initial Value.", ex);
            }
        }


        /// <summary>
        /// 방위셋팅
        /// </summary>
        /// <param name="channelString"></param>
        /// <returns></returns>
        private String SettingDirection(string channelString)
        {
            if (channelString.Contains("_E"))
            {
                return "East";
            }
            else if (channelString.Contains("_W"))
            {
                return "West";
            }
            else if (channelString.Contains("_S"))
            {
                return "South";
            }
            else if (channelString.Contains("_N"))
            {
                return "North";
            }
            else
            {
                logger.Error($"Error Displacement Channel(\"{channelString}\") is none.");
            }
            return null;
        } 


        private Dictionary<string, int> GetColumnIdx(OdbcCommand cmd)
        {
            var colIdx = new Dictionary<string, int>();
            try
            {
                cmd.CommandText = $"SELECT COLNAME, COLIDX FROM COLUMNS WHERE TAB LIKE 'P1_RAW' OR TAB LIKE 'P2_RAW' OR "+
                    $"TAB LIKE 'P3_RAW' OR TAB LIKE 'P4_RAW' ORDER BY COLIDX ASC";
                OdbcDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    colIdx.Add(reader[0].ToString(), (int)reader[1]);
                }

                reader.Close();
            }
            catch (Exception ex)
            {
                logger.Error("Error Get Column Index", ex);
            }

            return colIdx;
        }

        private List<string> GetColumnName(OdbcCommand cmd, string tableName)
        {
            var colName = new List<string>();
            try
            {
                cmd.CommandText = $"SELECT COLNAME FROM COLUMNS WHERE TAB LIKE '{tableName}' ORDER BY COLIDX ASC";
                OdbcDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    colName.Add(reader[0].ToString());
                }

                reader.Close();
            }
            catch (Exception ex)
            {
                logger.Error("Error Get Column Name", ex);
            }
            return colName;
        }
    }
}