// See https://aka.ms/new-console-template for more information
using AasCore.Aas3_0;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

Console.WriteLine("AAS Example Client V3");
Console.WriteLine();

// Find full API at https://v3.admin-shell-io.com/swagger
// GET shells, which is with pagenation
//Console.WriteLine("GET https://v3.admin-shell-io.com/shells");

string requestPath = "http://localhost:5001/shells";

var handler = new HttpClientHandler();

if (!requestPath.Contains("localhost"))
{
    handler.DefaultProxyCredentials = CredentialCache.DefaultCredentials;
}

//mqtt
string brokerAddress = "localhost";
int brokerPort = 1883;

var client = new HttpClient(handler);

bool error = false;
HttpResponseMessage response = new HttpResponseMessage();

MqttClient Mclient = new MqttClient(brokerAddress, brokerPort, false, null, null, MqttSslProtocols.None);

Mclient.MqttMsgPublishReceived += (sender, e) =>
{
            string message = System.Text.Encoding.UTF8.GetString(e.Message);
            Console.WriteLine($"Received message: {message}");
};

        // 클라이언트 연결
Mclient.Connect(Guid.NewGuid().ToString());

var task = Task.Run(async () =>
{
    response = await client.GetAsync(requestPath);
});
task.Wait();
// 메시지 수신 이벤트 핸들러

var json = response.Content.ReadAsStringAsync().Result;
if (!string.IsNullOrEmpty(json))
{
    MemoryStream mStrm = new MemoryStream(Encoding.UTF8.GetBytes(json));
    JsonNode node = System.Text.Json.JsonSerializer.DeserializeAsync<JsonNode>(mStrm).Result;
    if (node is JsonObject jo)
    {
        if (jo.ContainsKey("result"))
        {
            node = (JsonNode)jo["result"];
            if (node is JsonArray a)
            {
                // iterate shells
                foreach (JsonNode n in a)
                {
                    if (n != null)
                    {
                        
                        string topic = "AASX";
                        string jsonString = n.ToString();                         
                        byte[] payload = System.Text.Encoding.UTF8.GetBytes(jsonString);
                        Mclient.Publish(topic, payload, MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
                        try
                        {
                            // Deserialize shell
                            var aas = Jsonization.Deserialize.AssetAdministrationShellFrom(n);
                            Console.WriteLine("Received AAS: " + aas.IdShort);

                            // Iterate submodels
                            if (aas.Submodels != null && aas.Submodels.Count > 0)
                            {
                                foreach (var smr in aas.Submodels)
                                {
                                    requestPath = "http://localhost:5001/submodels/" + Base64UrlEncoder.Encode(smr.Keys[0].Value);
                                    Console.WriteLine("GET " + requestPath);

                                    task = Task.Run(async () =>
                                    {
                                        response = await client.GetAsync(requestPath);
                                    });
                                    task.Wait();
                                    json = response.Content.ReadAsStringAsync().Result;
                                    //submodelElement 보내기
                                    string jsonString2 = json.ToString();
                                    byte[] payload2 = System.Text.Encoding.UTF8.GetBytes(jsonString2);
                                    //byte[] Aasxdata = payload.Concat(payload2).ToArray();
                                    Mclient.Publish(topic, payload2, MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE, false);
                                    if (!string.IsNullOrEmpty(json))
                                    {
                                        var mStrm2 = new MemoryStream(Encoding.UTF8.GetBytes(json));
                                        var node2 = System.Text.Json.JsonSerializer.DeserializeAsync<JsonNode>(mStrm2).Result;
                                        var submodel = Jsonization.Deserialize.SubmodelFrom(node2);
                                        Console.WriteLine("Received Submodel: " + submodel.IdShort);
                                        // Iterate submodel here
                                        // See VisitorThrough in AasCore
                                        // See VisitorAASX in entityFW.cs
                                    }
                                }

                            }
                        }
                        catch
                        {
                            string r = "ERROR GET; " + response.StatusCode.ToString();
                            r += " ; " + requestPath;
                            if (response.Content != null)
                                r += " ; " + response.Content.ReadAsStringAsync().Result;
                            Console.WriteLine(r);
                        }
                    }
                    Console.WriteLine();
                }
            }
        }
    }    
}
Mclient.Disconnect();
