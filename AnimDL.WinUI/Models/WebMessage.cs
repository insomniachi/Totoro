using System.Text.Json.Serialization;

namespace AnimDL.WinUI.Models;

public class WebMessage
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WebMessageType MessageType { get; set; }
    public string Content { get; set; }
}