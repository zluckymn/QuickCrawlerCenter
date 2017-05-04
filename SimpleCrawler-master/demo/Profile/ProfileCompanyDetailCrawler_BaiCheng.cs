﻿using DotNet.Utilities;
using HtmlAgilityPack;
using MongoDB.Bson;
using MongoDB.Driver.Builders;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Yinhe.ProcessingCenter;
using Yinhe.ProcessingCenter.DataRule;

namespace SimpleCrawler.Demo
{
    /// app 土地公告与土地预告
    /// https://appapi.3g.fang.com/LandApp/LandNotice?count=20&page=1&mode=PushLand&wirelesscode=904e5fed8bef6df7c0fb79bc0e897ac4&r=6cFqC2ZmBQA%3D
    /// https://appapi.3g.fang.com/LandApp/LandNotice?count=20&page=1&mode=PushLand&wirelesscode=904e5fed8bef6df7c0fb79bc0e897ac4&r=6cFqC2ZmBQA%3D
    /// https://appapi.3g.fang.com/LandApp/MarketSearch?scity=%E5%8C%97%E4%BA%AC%E5%B8%82&imei=000000000000000&psize=20&ordertype=2&type=1&mode=json&pindex=2&ordername=landstartdate&messagename=search&wirelesscode=e72f6a278fd134dab78adbcde73c8341&r=kNLFpDufcFo%3D(北京)
    /// <summary>
    /// 用于地块地区获取,经过改程序跑完还是没有县市 表示该房子已经变更，或者不存在了,支持用户更新的地块更新
    /// </summary>
    public class ProfileCompanyDetailCrawler_BaiCheng : ISimpleCrawler
    {

        object lock_obj = new object();
        //private   string connStr = "mongodb://MZsa:MZdba@59.61.72.34:37088/WorkPlanManage";
        DataOperation dataop = null;
        private CrawlSettings Settings = null;
        private BloomFilter<string> entFilter;
        /// <summary>
        /// The filter.
        /// 关于使用 Bloom 算法去除重复 URL：http://www.cnblogs.com/heaad/archive/2011/01/02/1924195.html
        /// </summary>
        private BloomFilter<string> filter;

        private const string _DataTableName = "ProfileCompany_BaiCheng";//存储的数据库表明
        private const string cityName = "南京";//存储的数据库表明
        /// <summary>
        /// 返回
        /// </summary>
        public string DataTableName
        {
            get { return _DataTableName; }

        }
        /// <summary>
        /// 返回
        /// </summary>
        public string DataTableNameURL
        {
            get { return _DataTableName; }

        }
        /// <summary>
        /// 返回
        /// </summary>
        public string DataTableNameCity
        {
            get { return _DataTableName + "CityEXURL"; }

        }
        /// <summary>
        /// 需要新增的
        /// </summary>
        public string DataTableNameNeedAdd
        {
            get { return _DataTableName + "NeedAddUrl"; }

        }


        List<BsonDocument> cityUrlList = new List<BsonDocument>();
        List<BsonDocument> landUrlList = new List<BsonDocument>();//没有县市的Url
        List<BsonDocument> allLandUrlList = new List<BsonDocument>();//没有县市的Url
        /// <summary>
        /// 谁的那个
        /// </summary>
        /// <param name="_Settings"></param>
        /// <param name="filter"></param>
        public ProfileCompanyDetailCrawler_BaiCheng(CrawlSettings _Settings, BloomFilter<string> _filter, DataOperation _dataop)
        {
            Settings = _Settings; filter = _filter; dataop = _dataop;
        }
        public WebProxy GetWebProxy()
        {
            // 设置代理服务器
            var proxy = new WebProxy();
            proxy.Address = new Uri(string.Format("{0}:{1}", "http://proxy.abuyun.com", "9010"));
            proxy.Credentials = new NetworkCredential("H1538UM3D6R2133P", "511AF06ABED1E7AE");
            return proxy;
        }
        public void SettingInit()//进行Settings.SeedsAddress Settings.HrefKeywords urlFilterKeyWord 基础设定
        {
            //种子地址需要加布隆过滤

            //Settings.Depth = 4;
            //代理ip模式
            Settings.IPProxyList = new List<IPProxy>();
            Settings.IgnoreSucceedUrlToDB = true;//不添加地址到数据库
            Settings.ThreadCount = 1;
            Console.WriteLine("正在获取已存在的url数据");
            entFilter = new BloomFilter<string>(8000000);
            var allCount = 56164;
            Settings.CurWebProxy = GetWebProxy();//使用代理
            this.Settings.UserAgent = "Mozilla/5.0 (Linux; Android 4.4.2; GT-I9300 Build/KOT49H) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/30.0.0.0 Mobile";

            var headSetDic = new Dictionary<string, string>();
          
            Settings.UseSuperWebClient = true;
            LibCurlNet.HttpInput hi = new LibCurlNet.HttpInput();
            LibCurlNet.HttpManager.Instance.InitWebClient(hi, true, 30, 30);
            hi.HeaderSet("Accept-Encoding", "gzip,deflate");
            hi.HeaderSet("Accept-Language", "zh-CN,en-US;q=0.8");
            Settings.hi = hi;
     

            //Settings.SimulateCookies = "JSESSIONID=1jzs29iilbldmqq0hye30umzj";
            Settings.HeadSetDic = headSetDic;
            var companyUrl = "http://app.bczp.cn//WebPage/EntDetail.aspx?entsn={0}&Token=e166351efbf218118575&sn=3922710&hb=0&sitecode=fosh";
            //布隆url初始化,防止重复读取url
            allLandUrlList = dataop.FindAllByQuery(DataTableName, Query.And(Query.EQ("cityName",cityName),Query.NE("isUpdate", "1"))).SetFields("entSn").ToList();//城市url

            //这里只提取去有县市区域的url 没有县市url的需要手动在执行一次
            //foreach (var cityUrl in allLandUrlList.Distinct())//
            //{
            //    UrlQueue.Instance.EnQueue(new UrlInfo(cityUrl.Text("url")) { Depth = 1 });
            //}
             foreach(var ent in allLandUrlList)
            {
                var hitUrl = string.Format(companyUrl, ent.Text("entSn"));
                UrlQueue.Instance.EnQueue(new UrlInfo(hitUrl) { Depth = 1 });
            }


            Settings.RegularFilterExpressions.Add("XXX");//不添加其他
            if (SimulateLogin())
            {
                //  Console.WriteLine("zluckymn模拟登陆成功");
            }
            else
            {
                Console.WriteLine("zluckymn模拟登陆失败");
            }

            LibCurlNet.HttpManager.Instance.InitWebClient(hi, true, 30, 30);

        }
        LibCurlNet.HttpInput hi = new LibCurlNet.HttpInput();
        Dictionary<string, List<BsonDocument>> cityLandObjectList = new Dictionary<string, List<BsonDocument>>();
        public static Object lockRoom = new System.Object();
       
        /// <summary>
        /// 数据接收处理，失败后抛出NullReferenceException异常，主线程会进行捕获
        /// </summary>
        /// <param name="args">url参数</param>
        public void DataReceive(DataReceivedEventArgs args)
        {
            var hmtl = args.Html;
            HtmlDocument htmlDoc = new HtmlDocument();
            var curAddBsonDocument = new BsonDocument();
            htmlDoc.LoadHtml(args.Html);//提取文章
            var entSn = GetUrlParam(args.Url, "entsn");
            if (string.IsNullOrEmpty(entSn))
            {
                Console.WriteLine("{0}不存在entsn",args.Url);
                return;
            }
            var contractDiv = htmlDoc.GetElementbyId("divEntContact");
            if (contractDiv == null)
            {
                Console.WriteLine("{0}不存在联系div",args.Url);
                return;
            }
            foreach (var divNode in contractDiv.ChildNodes)
            {
                var txt = divNode.InnerText;

                if (txt.Contains("：") )
                {
                    var txtArr = txt.Split(new string[] { "：" }, StringSplitOptions.RemoveEmptyEntries);
                    if (txtArr.Length == 2)
                    {
                        curAddBsonDocument.Set(txtArr[0].Trim(), txtArr[1].Trim());
                    }
                }
            }
            DBChangeQueue.Instance.EnQueue(new StorageData() { Document = curAddBsonDocument, Name = DataTableName, Query = Query.EQ("entSn", entSn), Type = StorageType.Update });
            Console.WriteLine("{0}成功", entSn);
         }



        /// <summary>
        /// 获取url对应查询参数
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private static string GetUrlParam(string url, string name)
        {
            var queryStr = GetQueryString(url);
            var dic = HttpUtility.ParseQueryString(queryStr);
            var industryCode = dic[name] != null ? dic[name].ToString() : string.Empty;//行业代码
            return industryCode;
        }


        /// <summary>
        /// 获取url对应查询参数
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        private static string GetQueryString(string url)
        {
            var queryStrIndex = url.IndexOf("?");
            if (queryStrIndex != -1)
            {
                var queryStr = url.Substring(queryStrIndex + 1, url.Length - queryStrIndex - 1);
                return queryStr;
            }
            return string.Empty;
        }

        /// <summary>
        /// IP限定处理，ip被限制 账号被限制跳转处理
        /// </summary>
        /// <param name="args"></param>
        public bool IPLimitProcess(DataReceivedEventArgs args)
        {
            if (args.Html.Contains("很抱歉，您要访问的页面不存在"))//需要编写被限定IP的处理
            {
                return true;
            }
            return false;
        }
        /// <summary>
        /// url处理,是否可添加
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public bool CanAddUrl(AddUrlEventArgs args)
        {

            return true;
        }

        /// <summary>
        /// void错误处理
        /// </summary>
        /// <param name="args"></param>
        public void ErrorReceive(CrawlErrorEventArgs args)
        {


        }

        /// <summary>
        /// 模拟登陆，ip代理可能需要用到
        /// </summary>
        /// <returns></returns>
        public bool SimulateLogin()
        {
            return true;

        }



        /// <summary>
        /// ip无效处理
        /// </summary>
        private void IPInvalidProcess(IPProxy ipproxy)
        {
            Settings.SetUnviableIP(ipproxy);//设置为无效代理
            if (ipproxy != null)
            {
                DBChangeQueue.Instance.EnQueue(new StorageData()
                {
                    Name = "IPProxy",
                    Document = new BsonDocument().Add("status", "1"),
                    Query = Query.EQ("ip", ipproxy.IP)
                });
                StartDBChangeProcess();
            }

        }

        /// <summary>
        /// 对需要更新的队列数据更新操作进行批量处理,可考虑异步执行
        /// </summary>
        private void StartDBChangeProcess()
        {

            List<StorageData> updateList = new List<StorageData>();
            while (DBChangeQueue.Instance.Count > 0)
            {
                var curStorage = DBChangeQueue.Instance.DeQueue();
                if (curStorage != null)
                {
                    updateList.Add(curStorage);
                }
            }
            if (updateList.Count() > 0)
            {
                var result = dataop.BatchSaveStorageData(updateList);
                if (result.Status != Status.Successful)//出错进行重新添加处理
                {
                    foreach (var storageData in updateList)
                    {
                        DBChangeQueue.Instance.EnQueue(storageData);
                    }
                }
            }

        }
    }

}