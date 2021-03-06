﻿using DotNet.Utilities;
using HtmlAgilityPack;
using MongoDB.Bson;
using MongoDB.Driver.Builders;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using Yinhe.ProcessingCenter;

using Yinhe.ProcessingCenter.DataRule;
using System.Collections;
using Newtonsoft.Json.Linq;
using LibCurlNet;

namespace SimpleCrawler.Demo
{

    /// <summary>
    /// 门派url
    /// https://menpai.member.fun/api/Activity/GetActivityList
    ///
    /// </summary>
    public class ActivityListCrawler_MenPai : SimpleCrawlerBase
    {

        
#pragma warning disable CS0414 // 字段“ActivityListCrawler_MenPai.isUpdate”已被赋值，但从未使用过它的值
        bool isUpdate = true;
#pragma warning restore CS0414 // 字段“ActivityListCrawler_MenPai.isUpdate”已被赋值，但从未使用过它的值
        const int takeCount = 8;
        /// <summary>
        /// 谁的那个
        /// </summary>
        /// <param name="_Settings"></param>
        /// <param name="filter"></param>
        public ActivityListCrawler_MenPai(CrawlSettings _Settings, BloomFilter<string> _filter, DataOperation _dataop) : base(_Settings, _filter, _dataop)
        {
            DataTableName = "Activity_MenPai";//注销企业
        }
        public void  initialUrl(int nextIndex,int pageSize = 1)
        {
         
            //初始化布隆过滤器
            for (var index = nextIndex; index < pageSize; index++)
            {
                var skipCount = index * takeCount;
                var curUrl = $"https://menpai.member.fun/api/Activity/GetActivityList";
               //  var postData = $"Title=&ActivityType=1&SkipCount={skipCount}&MaxResultCount={takeCount}";
               //var postData = "{\"Title\"=&ActivityType=1&SkipCount={skipCount}&MaxResultCount={takeCount}";
                var postDoc = new BsonDocument();
                postDoc.Add("Title", "");
                postDoc.Add("ActivityType", 1);
                postDoc.Add("SkipCount", skipCount);
                postDoc.Add("MaxResultCount", takeCount);
                var hashCode = (curUrl + postDoc.ToJson()).GetHashCode();
                curUrl += $"?r={hashCode}";
                if (!filter.Contains(hashCode.ToString()))
                {
                    UrlQueue.Instance.EnQueue(new UrlInfo(curUrl) {  PostData= postDoc.ToJson(), UniqueKey= nextIndex.ToString() });
                    filter.Add(curUrl);// 防止执行2次
                }
            }
        }
        override
        public void SettingInit()//进行Settings.SeedsAddress Settings.HrefKeywords urlFilterKeyWord 基础设定
        {
            //种子地址需要加布隆过滤
            //Settings.Depth = 4;
            //代理ip模式
            Settings.IPProxyList = new List<IPProxy>();
            Settings.IgnoreSucceedUrlToDB = true;//不添加地址到数据库
            Settings.ThreadCount = 1;
            Settings.MaxReTryTimes = 10;
            Settings.ContentType = "application/json";
            Settings.Accept = "*/*";
            Settings.UserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 12_3_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Mobile/15E148 MicroMessenger/7.0.5(0x17000523) NetType/WIFI Language/zh_CN";
            Settings.HeadSetDic = new Dictionary<string, string>();
            Settings.HeadSetDic.Add("Authorization", "Bearer MSJesnNshG9_7usm6vJ-IqTpQRMyLXbc84QMsr46_I2Mz-wDWE-qmNFWBtuHPwqy_WDj8BhOkVm-5j29esV1LxarDcy5NKBOtLeGgCe-T_CxTrtDfwanRpEne2VUfR6XiHpazldriKMfVpTSd50_0ZgoyBgad3reMKVI3rlWwseeyBQphAtl7S59U8N3fjviZSZCLVTZDSEHFjBUXwuCzBVCQ8doGYh6Uv6zw7meWd2snPDCm4N0Uv7dFMQNdQKsqxNslmCzW-7mzBPOLR79GuV0UmEyNL-n-DF4Txvivw6UapTEZQ9UVMtACviatigbDd-QVXvuVrTW6i7axPaQYpAcjLWdp2dF6FTN_rR6XY8oRTazoB_CFbLt_GdeW5yCeuHGSKWChO-cDPMv_vXui7CdpogmNwo4TrcpFkW62neeZPDSFC5jBhAR8Ki5oiRF96oedNwbh64uaIYlWXmECqY2GBpIzBKJIMIPc6EyN0MCq3FZGBfNv7HHpLPqFmLS");
            Settings.Referer = "https://servicewechat.com/wx567da96b724db569/16/page-frame.html";
            Console.WriteLine("正在获取已存在的url数据");

         

            Console.WriteLine("初始化url");
            initialUrl(0);
            base.SettingInit();



        }
#pragma warning disable CS0414 // 字段“ActivityListCrawler_MenPai.noCountTimes”已被赋值，但从未使用过它的值
        int noCountTimes = 3;
#pragma warning restore CS0414 // 字段“ActivityListCrawler_MenPai.noCountTimes”已被赋值，但从未使用过它的值
        /// <summary>
        /// 数据接收处理，失败后抛出NullReferenceException异常，主线程会进行捕获
        /// </summary>
        /// <param name="args">url参数</param>
        override
        public void DataReceive(DataReceivedEventArgs args)
        {
            var hmtl = args.Html;
            JObject jsonObj = GetJsonObject(hmtl);
            var result = jsonObj["result"];
            var totalCount = GetJsonValueInt(result, "totalCount");
            var items = result["items"];
            if (items != null)
            {
                foreach (var item in items)
                {
                    var bsonDoc = GetBsonDocument(item);
                    bsonDoc.Set("guid", bsonDoc.Text("id"));
                    PushData(bsonDoc);
                }
            }
            var index = args.urlInfo.UniqueKey;
            if (index == "0")
            {
                var pageSize = totalCount / takeCount;
                if (pageSize == 0)
                {
                    pageSize = 1;
                }
                initialUrl(1, pageSize);
                ShowMessage($"获取{pageSize}页数据并初始化成功");
            }
            ShowStatus();
       
        }

        /// <summary>
        /// IP限定处理，ip被限制 账号被限制跳转处理
        /// </summary>
        /// <param name="args"></param>
        override
        public bool IPLimitProcess(DataReceivedEventArgs args)
        {
            try
            {
                JObject jsonObj = GetJsonObject(args.Html);
                var result = jsonObj["result"];
                var success = jsonObj["success"];
                if (success == null)//需要编写被限定IP的处理
                {
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return true;
            }
        }
     
     
    }

}
