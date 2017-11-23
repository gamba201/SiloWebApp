using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace SiloWebApp.Controllers
{

    public class Tools
    {
        readonly static ILog logger = LogManager.GetLogger(typeof(Tools));

        /// <summary>
        /// 암호화 메소드
        /// </summary>
        /// <param name="pw"></param>
        /// <returns></returns>
        public static string cryptoPW(string pw)
        {
            string hashcode = "{SHA256}";
            try
            {
                SHA256 sha = SHA256Managed.Create();
                byte[] pwByte = Encoding.UTF8.GetBytes(pw);
                byte[] hashByte = sha.ComputeHash(pwByte);
                foreach (byte hash in hashByte)
                {
                    hashcode += String.Format("{0:X2}", hash);
                }
                return hashcode;
            }
            catch (Exception ex)
            {
                logger.Error("Error Changing by SHA256 ", ex);
                return null;
            }
        }

    }
}