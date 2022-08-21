using System.Text.Json.Serialization;

namespace AnimDL.UI.Core.Models;

public class WebMessage
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WebMessageType MessageType { get; set; }
    public string Content { get; set; }
}