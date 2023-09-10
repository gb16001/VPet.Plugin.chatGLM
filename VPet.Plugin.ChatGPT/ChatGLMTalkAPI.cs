using LinePutScript.Localization.WPF;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using VPet_Simulator.Windows.Interface;

namespace VPet.Plugin.ChatGLMPlugin
{
    public class ChatGLMTalkAPI : TalkBox
    {
        public ChatGLMTalkAPI(ChatGLMPlugin mainPlugin) : base(mainPlugin)
        {
            Plugin = mainPlugin;
        }
        protected ChatGLMPlugin Plugin;
        public override string APIName => "ChatGLM";
        public static string[] like_str = new string[] { "陌生", "普通", "喜欢", "爱" };
        public static int like_ts(int like)
        {
            if (like > 50)
            {
                if (like < 100)
                    return 1;
                else if (like < 200)
                    return 2;
                else
                    return 3;
            }
            return 0;
        }
        public override void Responded(string content)
        {//api CONNECT TO SERVER HERE
            
            Plugin.MW.Main.SayRnd(Test(content));
            return;
            if (string.IsNullOrEmpty(content))
            {
                return;
            }
            if (Plugin.CGPTClient == null)
            {
                Plugin.MW.Main.SayRnd("请先前往设置中设置 ChatGPT API".Translate());
                return;
            }
            Dispatcher.Invoke(() => this.IsEnabled = false);
            try
            {
                if (Plugin.CGPTClient.Completions.TryGetValue("vpet", out var vpetapi))
                {
                    while (vpetapi.messages.Count > Plugin.KeepHistory + 1)
                    {
                        vpetapi.messages.RemoveAt(1);
                    }
                    var last = vpetapi.messages.LastOrDefault();
                    if (last != null)
                    {
                        if (last.role == ChatGPT.API.Framework.Message.RoleType.user)
                        {
                            vpetapi.messages.Remove(last);
                        }
                    }
                }
                content = "[当前状态: {0}, 好感度:{1}({2})]".Translate(Plugin.MW.Core.Save.Mode.ToString().Translate(), like_str[like_ts((int)Plugin.MW.Core.Save.Likability)].Translate(), (int)Plugin.MW.Core.Save.Likability) + content;
                
                var resp = Plugin.CGPTClient.Ask("vpet", content);
                var reply = resp.GetMessageContent();//这里调用API功能得到相应
                if (resp.choices[0].finish_reason == "length")
                {
                    reply += " ...";
                }
                if (Plugin.ShowToken)
                {
                    var showtxt = "当前Token使用".Translate() + ": " + resp.usage.total_tokens;
                    Dispatcher.Invoke(() =>
                    {
                        Plugin.MW.Main.MsgBar.MessageBoxContent.Children.Add(new TextBlock() { Text = showtxt, FontSize = 20, ToolTip = showtxt, HorizontalAlignment = System.Windows.HorizontalAlignment.Right });
                    });
                }
                Plugin.MW.Main.SayRnd(reply);
            }
            catch (Exception exp)
            {
                var e = exp.ToString();
                string str = "请检查设置和网络连接".Translate();
                if (e.Contains("401"))
                {
                    str = "请检查API token设置".Translate();
                }
                Plugin.MW.Main.SayRnd("API调用失败".Translate() + $",{str}\n{e}");//, GraphCore.Helper.SayType.Serious);
            }
            Dispatcher.Invoke(() => this.IsEnabled = true);
        }

        public string Test(string question)
        {
            string result = Plugin.GLMc1.Ask(question);
            string content =Plugin.GLMc1.json2text(result);
            return content;

            /*
            string API = "------------------------.+++++++++++++";//your API key

            chatGLMclient glm1 = new chatGLMclient(API, "https://open.bigmodel.cn/api/paas/v3/model-api");
            string result = glm1.Ask(question);
            string content = glm1.json2text(result);
            return content;
            //Console.WriteLine($"Content: {content}");*/
        }
        public override void Setting() => Plugin.Setting();
    
    
    }

}
