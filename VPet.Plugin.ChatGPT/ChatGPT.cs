using ChatGPT.API.Framework;
using LinePutScript;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using VPet_Simulator.Windows.Interface;
using Microsoft.IdentityModel.Tokens;

using Newtonsoft.Json;
using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Sockets;
using System.Net.Http;




namespace VPet.Plugin.ChatGLMPlugin
{
    public class ChatGLMPlugin : MainPlugin
    {
        public ChatGLMPlugin(IMainWindow mainwin) : base(mainwin) { }
        public ChatGPTClient CGPTClient;
        //main obj
        public chatGLMclient GLMc1;
        public override void LoadPlugin()
        {//这里初始化class
            if (File.Exists(AppDomain.CurrentDomain.BaseDirectory + @"\ChatGLMSetting.json"))
            {
                CGPTClient = ChatGPTClient.Load(File.ReadAllText(AppDomain.CurrentDomain.BaseDirectory + @"\ChatGLMSetting.json"));
                //INIT glm obj
                GLMc1 = new chatGLMclient(CGPTClient.APIKey);
            }
                
            MW.TalkAPI.Add(new ChatGLMTalkAPI(this));
        }
        public override void Save()
        {
            if (CGPTClient != null)
                File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + @"\ChatGLMSetting.json", CGPTClient.Save());
        }
        public override void Setting()
        {
            new winSetting(this).ShowDialog();
        }
        public override string PluginName => "ChatGLM";
        /// <summary>
        /// 是否在聊天位置显示Token数量
        /// </summary>
        public bool ShowToken
        {
            get => !MW.Set["CGPT"][(gbol)"noshowtoken"];
            set => MW.Set["CGPT"][(gbol)"noshowtoken"] = !value;
        }
        /// <summary>
        /// 保留的历史数量
        /// </summary>
        public int KeepHistory
        {
            get => MW.Set["CGPT"].GetInt("keephistory", 20);
            set => MW.Set["CGPT"][(gint)"keephistory"] = value;
        }
    }

    //GLM MAIN CLASS
    public class chatGLMclient
    {
        private string apiKey;
        private string apiUrl;
        public string api_token;
        public chatGLMclient(string apiKey, string apiUrl = "https://open.bigmodel.cn/api/paas/v3/model-api")
        {
            this.apiKey = apiKey;
            this.apiUrl = apiUrl;
            this.api_token = GenToken(apiKey);
        }
        public string Ask(string message)
        {
            // 创建一个字典对象
            Dictionary<string, object> prompt = new Dictionary<string, object>
            {
            { "role", "user" },
            { "content", "请使用口语化的、可爱的、女性化的、调皮的语言风格和我交流. " +
                "你是一只桌宠, 你的名字叫萝莉斯, 你喜欢玩 虚拟主播模拟器. " +
                "你需要尝试体会我与你对话中的感情、产生自己的情感并尽可能给出可爱的回答"+message }
        };
            Dictionary<string, object> request_perameters = new Dictionary<string, object>
        {
            { "prompt",prompt}
        };

            // 将字典转换为JSON字符串
            string json_ask = JsonConvert.SerializeObject(request_perameters);

            string result = PostHttp(this.api_token, json_ask, this.apiUrl);
            return result;
        }
        public string json2text(string result)
        {// 解析JSON字符串为content
            var jsonObject = JsonConvert.DeserializeAnonymousType(result, new
            {
                data = new
                {
                    choices = new[]
                {
                    new
                    {
                        role = "",
                        content = ""
                    }
                }
                }
            });
            string content = jsonObject.data.choices[0].content;
            return content;
            // Console.WriteLine($"Content: {content}");
        }

        public string GenToken(string apikey, int exp_seconds = 3600)
        {//verified，令牌生成,使用jwt依据apikey产生令牌



            try
            {
                //get ID,secret
                string[] apiKeyParts = apikey.Split('.');
                if (apiKeyParts.Length != 2)
                {
                    throw new Exception("Invalid apikey format");
                }
                string id = apiKeyParts[0];
                string secret = apiKeyParts[1];
                //GET securityKey from secret
                var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));//Encoding.UTF8.GetBytes(new string('0', 16) + secret)
                var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);//HmacSha256
                                                                                                     //define header,payload
                var header = new JwtHeader
        {
            { "alg", "HS256" },
            { "sign_type", "SIGN" }
        };
                var payload = new JwtPayload
        {
            { "api_key", id },
            { "exp", DateTimeOffset.UtcNow.AddSeconds(exp_seconds).ToUnixTimeMilliseconds() },
            { "timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }
        };


                //ceate token
                var token = new JwtSecurityToken(header, payload);



                //get Signature of token
                var Signature = HS256sign(token.EncodedHeader, token.EncodedPayload, secret);


                //var tokenHandler = new JwtSecurityTokenHandler();
                //var jwtToken = tokenHandler.WriteToken(token);

                //combin Header Payload Signature
                string row_resalt_str = token.EncodedHeader + "." + token.EncodedPayload + "." + Signature;

                return row_resalt_str.Replace("=", "").Replace("/", "_").Replace("+", "-");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
                return null;
            }


        }
        public string PostHttp(string token, string prompt_contant, string Url, string model = "chatglm_pro", string invokeMethod = "invoke")
        {
            //token鉴权token JWT,prompt_contant请求体-主要写prompt， model invokeMethod 定义模型编码和调用方式。verified


            // 构建请求URL
            string apiUrl = $"{Url}/{model}/{invokeMethod}";


            try
            {// 构建请求头
                var client = new HttpClient();
                //client.DefaultRequestHeaders.Add("Content-Type", "application/json");


                client.DefaultRequestHeaders.Add("Authorization", token);

                // 构建请求参数，根据具体模型的要求设置请求参数
                string requestBody = prompt_contant; // 设置JSON格式的请求体,必须要有提示词
                HttpContent content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                // 发送POST请求
                HttpResponseMessage response = client.SendAsync(new HttpRequestMessage(HttpMethod.Post, apiUrl)
                {
                    Content = content
                }).Result;

                // 处理响应
                if (response.IsSuccessStatusCode)
                {
                    string responseContent = response.Content.ReadAsStringAsync().Result;
                    Console.WriteLine("HTTP请求成功，响应内容：");
                    Console.WriteLine(responseContent);
                    return responseContent;
                }
                else
                {
                    Console.WriteLine($"HTTP请求失败，状态码：{response.StatusCode}");
                    return response.StatusCode.ToString();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发生异常：{ex.Message}");
                return ex.Message;
            }
        }
        public string HS256sign(string header, string payload, string secret)
        {//verified,jwt最后一步，依据编码好的header，payload，使用secret，产生签名。因为cs自带库不支持128位密钥


            // 将密钥字符串转换为字节数组
            byte[] keyBytes = Encoding.UTF8.GetBytes(secret);

            // 将 header 和 payload 连接在一起
            string dataToSign = header + "." + payload;

            // 使用 HMAC-SHA256 进行签名
            using (HMACSHA256 hmac = new HMACSHA256(keyBytes))
            {
                byte[] hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataToSign));
                string signature = Convert.ToBase64String(hashBytes);

                Console.WriteLine("HMAC-SHA256 签名结果：");
                Console.WriteLine(signature);
                return signature;
            }
        }
    }

}

