using Quartz.Impl;
using SiloWebApp.Scheduler;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Web.Http;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace SiloWebApp
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        public static string ConfigPath { get; set; }
        public static string RunQueryPath { get; set; }
        public static string PreQueryPath { get; set; }

        protected void Application_Start()
        {
            //log4net.Config.XmlConfigurator.Configure();
            ConfigPath = Server.MapPath(ConfigurationManager.AppSettings["configPath"]);
            RunQueryPath = Server.MapPath(ConfigurationManager.AppSettings["runtimeQuery"]);
            PreQueryPath = Server.MapPath(ConfigurationManager.AppSettings["prepareQuery"]);


            GlobalConfiguration.Configure(WebApiConfig.Register);

            // 최초 실행 테이블, 프로시저 생성
            PrepareJob preJob = new PrepareJob();
            preJob.Execute();

            // 스케쥴러 시작
            var scheduler = new StdSchedulerFactory().GetScheduler();
            scheduler.Start();

        }
    }
}
